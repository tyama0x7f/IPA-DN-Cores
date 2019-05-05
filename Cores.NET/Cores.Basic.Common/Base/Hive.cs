﻿// IPA Cores.NET
// 
// Copyright (c) 2018-2019 IPA CyberLab.
// Copyright (c) 2003-2018 Daiyuu Nobori.
// Copyright (c) 2013-2018 SoftEther VPN Project, University of Tsukuba, Japan.
// All Rights Reserved.
// 
// License: The Apache License, Version 2.0
// https://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
// THIS SOFTWARE IS DEVELOPED IN JAPAN, AND DISTRIBUTED FROM JAPAN, UNDER
// JAPANESE LAWS. YOU MUST AGREE IN ADVANCE TO USE, COPY, MODIFY, MERGE, PUBLISH,
// DISTRIBUTE, SUBLICENSE, AND/OR SELL COPIES OF THIS SOFTWARE, THAT ANY
// JURIDICAL DISPUTES WHICH ARE CONCERNED TO THIS SOFTWARE OR ITS CONTENTS,
// AGAINST US (IPA CYBERLAB, DAIYUU NOBORI, SOFTETHER VPN PROJECT OR OTHER
// SUPPLIERS), OR ANY JURIDICAL DISPUTES AGAINST US WHICH ARE CAUSED BY ANY KIND
// OF USING, COPYING, MODIFYING, MERGING, PUBLISHING, DISTRIBUTING, SUBLICENSING,
// AND/OR SELLING COPIES OF THIS SOFTWARE SHALL BE REGARDED AS BE CONSTRUED AND
// CONTROLLED BY JAPANESE LAWS, AND YOU MUST FURTHER CONSENT TO EXCLUSIVE
// JURISDICTION AND VENUE IN THE COURTS SITTING IN TOKYO, JAPAN. YOU MUST WAIVE
// ALL DEFENSES OF LACK OF PERSONAL JURISDICTION AND FORUM NON CONVENIENS.
// PROCESS MAY BE SERVED ON EITHER PARTY IN THE MANNER AUTHORIZED BY APPLICABLE
// LAW OR COURT RULE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

namespace IPA.Cores.Basic
{
    abstract class HiveSerializerOptions { }

    abstract class HiveSerializer
    {
        public HiveSerializerOptions Options { get; }

        public HiveSerializer(HiveSerializerOptions options)
        {
            this.Options = options;
        }

        abstract protected Memory<byte> SerializeImpl<T>(T obj);
        abstract protected T DeserializeImpl<T>(ReadOnlyMemory<byte> memory);

        public Memory<byte> Serialize<T>(T obj) => SerializeImpl(obj);
        public T Deserialize<T>(ReadOnlyMemory<byte> memory) => DeserializeImpl<T>(memory);
    }

    class JsonHiveSerializerOptions : HiveSerializerOptions
    {
        public DataContractJsonSerializerSettings JsonSettings { get; }

        public JsonHiveSerializerOptions(DataContractJsonSerializerSettings jsonSettings = null)
        {
            this.JsonSettings = jsonSettings;
        }
    }

    class JsonHiveSerializer : HiveSerializer
    {
        public new JsonHiveSerializerOptions Options => (JsonHiveSerializerOptions)base.Options;

        public JsonHiveSerializer(JsonHiveSerializerOptions options = null) : base(options == null ? new JsonHiveSerializerOptions() : options)
        {
        }

        protected override Memory<byte> SerializeImpl<T>(T obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");

            MemoryBuffer<byte> ret = new MemoryBuffer<byte>();

            ret.Write(Str.NewLine_Bytes_Local);

            obj.ObjectToRuntimeJson(ret, Options.JsonSettings);

            ret.Write(Str.NewLine_Bytes_Local);
            ret.Write(Str.NewLine_Bytes_Local);

            return ret.Memory;
        }

        protected override T DeserializeImpl<T>(ReadOnlyMemory<byte> memory)
        {
            return memory.ToArray().RuntimeJsonToObject<T>(Options.JsonSettings);
        }
    }

    abstract class HiveStorageOptionsBase
    {
        public int MaxDataSize { get; }

        public HiveStorageOptionsBase(int maxDataSize = int.MaxValue)
        {
            this.MaxDataSize = maxDataSize;
        }
    }

    class FileHiveStorageOptions : HiveStorageOptionsBase
    {
        public FileSystem FileSystem { get; }
        public Copenhagen<string> RootDirectoryName { get; }
        public Copenhagen<FileOperationFlags> OperationFlags { get; }
        public Copenhagen<string> FileExtension { get; } = ".json";
        public Copenhagen<string> ErrorFileExtension { get; } = ".error.log";
        public Copenhagen<string> TmpFileExtension { get; } = ".tmp";
        public Copenhagen<string> DefaultDataName { get; } = "default";

        public FileHiveStorageOptions(FileSystem fileSystem, string rootDirectoryName, FileOperationFlags operationFlags = FileOperationFlags.WriteOnlyIfChanged, int maxDataSize = int.MaxValue)
            : base(maxDataSize)
        {
            this.FileSystem = fileSystem;
            this.RootDirectoryName = rootDirectoryName;
            this.OperationFlags = operationFlags;
        }
    }

    abstract class HiveStorageProvider : AsyncService
    {
        public HiveStorageOptionsBase Options;

        public HiveStorageProvider(HiveStorageOptionsBase options)
        {
            this.Options = options;
        }

        protected abstract Task SaveImplAsync(string dataName, ReadOnlyMemory<byte> data, bool doNotOverwrite, CancellationToken cancel = default);
        protected abstract Task ReportErrorImplAsync(string dataName, string error, CancellationToken cancel = default);
        protected abstract Task<Memory<byte>> LoadImplAsync(string dataName, CancellationToken cancel = default);

        public Task SaveAsync(string dataName, ReadOnlyMemory<byte> data, bool doNotOverwrite, CancellationToken cancel = default)
            => this.RunCriticalProcessAsync(true, cancel, c => SaveImplAsync(dataName, data, doNotOverwrite, c));
        public Task ReportErrorAsync(string dataName, string error, CancellationToken cancel = default)
            => this.RunCriticalProcessAsync(true, cancel, c => ReportErrorImplAsync(dataName, error, c));
        public Task<Memory<byte>> LoadAsync(string dataName, CancellationToken cancel = default)
            => this.RunCriticalProcessAsync(true, cancel, c => LoadImplAsync(dataName, c));
    }

    class FileHiveStorageProvider : HiveStorageProvider
    {
        public new FileHiveStorageOptions Options => (FileHiveStorageOptions)base.Options;

        FileSystem FileSystem => Options.FileSystem;
        FileSystemPathParser PathParser => FileSystem.PathParser;
        FileSystemPathParser SafePathParser = FileSystemPathParser.GetInstance(FileSystemStyle.Windows);

        AsyncLock LockObj = new AsyncLock();

        public FileHiveStorageProvider(FileHiveStorageOptions options) : base(options)
        {
        }

        string MakeFileName(string dataName, string extension)
        {
            dataName = Hive.NormalizeDataName(dataName);

            if (dataName.IsEmpty())
                dataName = Options.DefaultDataName;

            string ret = PathParser.Combine(Options.RootDirectoryName, SafePathParser.MakeSafePathName(dataName), true);

            ret = PathParser.RemoveDangerousDirectoryTraversal(ret);

            ret = PathParser.NormalizeDirectorySeparatorIncludeWindowsBackslash(ret);

            ret += extension;

            return ret;
        }

        protected override async Task SaveImplAsync(string dataName, ReadOnlyMemory<byte> data, bool doNotOverwrite, CancellationToken cancel = default)
        {
            string filename = MakeFileName(dataName, Options.FileExtension);
            string newFilename = filename + Options.TmpFileExtension;
            string directoryName = PathParser.GetDirectoryName(filename);

            if (data.IsEmpty == false)
            {
                if (doNotOverwrite)
                {
                    if (await FileSystem.IsFileExistsAsync(filename, cancel))
                    {
                        throw new ApplicationException($"The file \"{filename}\" exists while doNotOverwrite flag is set.");
                    }
                }

                try
                {
                    await FileSystem.CreateDirectoryAsync(directoryName, Options.OperationFlags, cancel);

                    await FileSystem.WriteDataToFileAsync(newFilename, data, Options.OperationFlags | FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed, false, cancel);
                    
                    try
                    {
                        await FileSystem.DeleteFileAsync(filename, Options.OperationFlags | FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed, cancel);
                    }
                    catch { }

                    await FileSystem.MoveFileAsync(newFilename, filename, cancel);
                }
                finally
                {
                    await FileSystem.DeleteFileAsync(newFilename, Options.OperationFlags | FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed, cancel);
                }
            }
            else
            {
                if (doNotOverwrite)
                    throw new ApplicationException($"The file {filename} exists while doNotOverwrite flag is set.");

                try
                {
                    await FileSystem.DeleteFileAsync(filename, FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed, cancel);
                }
                catch { }

                try
                {
                    await FileSystem.DeleteFileAsync(newFilename, FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed, cancel);
                }
                catch { }
            }
        }

        protected override async Task ReportErrorImplAsync(string dataName, string error, CancellationToken cancel = default)
        {
            string errFilename = MakeFileName(dataName, Options.ErrorFileExtension);
            string realFilename = MakeFileName(dataName, Options.FileExtension);

            StringWriter w = new StringWriter();
            w.WriteLine($"--- The hive file \"{realFilename}\" load error log ---");
            w.WriteLine($"Process ID: {Env.ProcessId}");
            w.WriteLine($"Process Name: {Env.ExeFileName}");
            w.WriteLine($"Timestamp: {DateTimeOffset.Now.ToDtStr(true)}");
            w.WriteLine($"Path: {realFilename}");
            w.WriteLine($"DataName: {dataName}");
            w.WriteLine($"Error:");
            w.WriteLine($"{error}");
            w.WriteLine();
            w.WriteLine($"Note: This log file \"{errFilename}\" is for only your reference. You may delete this file anytime.");
            w.WriteLine();
            w.WriteLine();

            await FileSystem.AppendDataToFileAsync(errFilename, w.ToString().GetBytes_UTF8(),
                Options.OperationFlags | FileOperationFlags.ForceClearReadOnlyOrHiddenBitsOnNeed | FileOperationFlags.AutoCreateDirectory | FileOperationFlags.OnCreateSetCompressionFlag,
                cancel);
        }

        protected override async Task<Memory<byte>> LoadImplAsync(string dataName, CancellationToken cancel = default)
        {
            string filename = MakeFileName(dataName, Options.FileExtension);
            string newFilename = filename + Options.TmpFileExtension;

            try
            {
                try
                {
                    await FileSystem.MoveFileAsync(newFilename, filename);
                }
                catch { }

                return await FileSystem.ReadDataFromFileAsync(filename, Options.MaxDataSize, Options.OperationFlags, cancel);
            }
            catch
            {
                throw;
            }
        }
    }

    static partial class CoresConfig
    {
        public static partial class ConfigHiveOptions
        {
            public static readonly Copenhagen<int> SyncIntervalMsec = 2 * 1000;
        }

        public static partial class DefaultHiveOptions
        {
            public static readonly Copenhagen<int> SyncIntervalMsec = 2 * 1000;
        }
    }

    class HiveOptions
    {
        public const int MinSyncIntervalMsec = 2 * 1000;

        public HiveSerializer Serializer { get; }
        public HiveStorageProvider StorageProvider { get; }
        public bool IsPollingEnabled { get; }

        public Copenhagen<int> SyncIntervalMsec { get; } = CoresConfig.DefaultHiveOptions.SyncIntervalMsec;

        public HiveOptions(string rootDirectoryName, bool enableManagedSync = false, int? syncInterval = null, HiveSerializer serializer = null)
            : this(new FileHiveStorageProvider(new FileHiveStorageOptions(LfsUtf8, rootDirectoryName)), enableManagedSync, syncInterval, serializer) { }

        public HiveOptions(HiveStorageProvider provider, bool enablePolling = false, int? syncInterval = null, HiveSerializer serializer = null)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (serializer == null) serializer = new JsonHiveSerializer();

            this.Serializer = serializer;
            this.StorageProvider = provider;

            if (enablePolling == false)
            {
                if (syncInterval.HasValue)
                {
                    throw new ApplicationException("enablePolling is false while syncInterval is specified.");
                }
            }

            if (syncInterval != null)
                this.SyncIntervalMsec.SetValue(syncInterval.Value);

            this.IsPollingEnabled = enablePolling;
        }
    }

    class Hive : AsyncServiceWithMainLoop
    {
        // Static states and methods
        public static readonly StaticModule Module = new StaticModule(InitModule, FreeModule);

        static readonly HashSet<Hive> RunningHivesList = new HashSet<Hive>();
        static readonly CriticalSection RunningHivesListLockObj = new CriticalSection();

        public static Hive SharedConfigHive { get; private set; } = null;
        const string ConfigHiveDirName = "Config";

        static void InitModule()
        {
            Module.AddAfterInitAction(() =>
            {
                // Create shared config hive
                SharedConfigHive = new Hive(new HiveOptions(Lfs.PathParser.Combine(Env.AppRootDir, ConfigHiveDirName), true,
                    CoresConfig.ConfigHiveOptions.SyncIntervalMsec,
                    null));
            });
        }

        static void FreeModule()
        {
            Hive[] runningHives;
            lock (RunningHivesListLockObj)
            {
                runningHives = RunningHivesList.ToArrayList();
            }
            foreach (Hive hive in runningHives)
            {
                hive.DisposeSafe(new CoresLibraryShutdowningException());
            }
            lock (RunningHivesListLockObj)
            {
                RunningHivesList.Clear();
            }
        }

        public static string NormalizeDataName(string name) => name.NonNullTrim().ToLower();

        // Instance states and methods
        public HiveOptions Options { get; }

        public HiveSerializer Serializer => Options.Serializer;
        public HiveStorageProvider StorageProvider => Options.StorageProvider;

        readonly Dictionary<string, IHiveData> RegisteredHiveData = new Dictionary<string, IHiveData>();
        readonly CriticalSection LockObj = new CriticalSection();

        readonly AsyncManualResetEvent EventWhenFirstHiveDataRegistered = new AsyncManualResetEvent();

        public Hive(HiveOptions options)
        {
            try
            {
                this.Options = options;

                lock (RunningHivesListLockObj)
                {
                    Module.CheckInitalized();
                    RunningHivesList.Add(this);
                }

                if (this.Options.IsPollingEnabled)
                {
                    this.StartMainLoop(MainLoopProcAsync);
                }
            }
            catch
            {
                this.DisposeSafe();
                throw;
            }
        }

        async Task MainLoopProcAsync(CancellationToken cancel)
        {
            int syncInterval = Math.Max(HiveOptions.MinSyncIntervalMsec, Options.SyncIntervalMsec);

            AsyncLocalTimer timer = new AsyncLocalTimer();

            long nextReadTick = 0;

            await EventWhenFirstHiveDataRegistered.WaitAsync(Timeout.Infinite, cancel);

            while (cancel.IsCancellationRequested == false)
            {
                if (timer.RepeatIntervalTimer(syncInterval, ref nextReadTick))
                {
                    try
                    {
                        await SyncAllHiveDataAsync(cancel);
                    }
                    catch { }
                }

                await timer.WaitUntilNextTickAsync(cancel);
            }
        }

        async Task SyncAllHiveDataAsync(CancellationToken cancel)
        {
            IHiveData[] hiveArray;

            lock (LockObj)
            {
                hiveArray = RegisteredHiveData.Values.ToArrayList();
            }

            foreach (var hive in hiveArray)
            {
                cancel.ThrowIfCancellationRequested();

                HiveSyncFlags flags = HiveSyncFlags.LoadFromFile;

                if (hive.IsReadOnly == false)
                    flags |= HiveSyncFlags.SaveToFile;

                try
                {
                    await hive.SyncWithStorageAsync(flags, cancel);
                }
                catch { }
            }
        }

        internal void CheckPreRegisterInterval(IHiveData hiveData)
        {
            if (hiveData.IsManaged == false)
                throw new ArgumentException("hiveData.IsManaged == false");

            lock (LockObj)
            {
                CheckNotCanceled();

                if (RegisteredHiveData.ContainsKey(hiveData.DataName))
                {
                    throw new ApplicationException($"The hive name \"{hiveData.DataName}\" is already registered on the same hive.");
                }
            }
        }

        internal void RegisterInternal(IHiveData hiveData)
        {
            if (hiveData.IsManaged == false)
                throw new ArgumentException("hiveData.IsManaged == false");

            lock (LockObj)
            {
                CheckNotCanceled();

                if (RegisteredHiveData.TryAdd(hiveData.DataName, hiveData) == false)
                {
                    throw new ApplicationException($"The hive name \"{hiveData.DataName}\" is already registered on the same hive.");
                }
            }

            this.EventWhenFirstHiveDataRegistered.Set(true);
        }

        internal void UnregisterInternal(IHiveData hiveData)
        {
            lock (LockObj)
            {
                RegisteredHiveData.Remove(hiveData.DataName);
            }
        }

        protected override async Task CleanupImplAsync(Exception ex)
        {
            // Flush all managed hives
            try
            {
                await SyncAllHiveDataAsync(default);
            }
            finally
            {
                await base.CleanupImplAsync(ex);
            }
        }

        protected override void DisposeImpl(Exception ex)
        {
            try
            {
                lock (RunningHivesListLockObj)
                {
                    RunningHivesList.Remove(this);
                }

                this.Options.StorageProvider.DisposeSafe();
            }
            finally
            {
                base.DisposeImpl(ex);
            }
        }

        public HiveData<T> CreateHive<T>(string dataName, Func<T> getDefaultDataFunc, HiveSyncPolicy policy = HiveSyncPolicy.None) where T : class, new()
            => new HiveData<T>(this, dataName, getDefaultDataFunc, policy);

        public HiveData<T> CreateReadOnlyHive<T>(string dataName, Func<T> getDefaultDataFunc) where T : class, new()
            => CreateHive(dataName, getDefaultDataFunc, HiveSyncPolicy.ReadOnly);

        public HiveData<T> CreateAutoReadHive<T>(string dataName, Func<T> getDefaultDataFunc) where T : class, new()
            => CreateHive(dataName, getDefaultDataFunc, HiveSyncPolicy.AutoReadFromFile | HiveSyncPolicy.ReadOnly);

        public HiveData<T> CreateAutoSyncHive<T>(string dataName, Func<T> getDefaultDataFunc) where T : class, new()
            => CreateHive(dataName, getDefaultDataFunc, HiveSyncPolicy.AutoReadFromFile | HiveSyncPolicy.AutoWriteToFile);
    }

    [Flags]
    enum HiveSyncFlags
    {
        None = 0,
        LoadFromFile = 1,
        SaveToFile = 2,
    }

    [Flags]
    enum HiveSyncPolicy
    {
        None = 0,
        ReadOnly = 1,
        AutoReadFromFile = 2,
        AutoWriteToFile = 4,
    }

    interface IHiveData
    {
        HiveSyncPolicy Policy { get; }
        string DataName { get; }
        Hive Hive { get; }
        bool IsManaged { get; }
        bool IsReadOnly { get; }
        Task SyncWithStorageAsync(HiveSyncFlags flag, CancellationToken cancel = default);
    }

    class HiveData<T> : IHiveData where T: class, new()
    {
        public HiveSyncPolicy Policy { get; private set; }
        public string DataName { get; }
        public Hive Hive { get; }
        public bool IsManaged { get; } = false;
        public bool IsReadOnly => this.Policy.Bit(HiveSyncPolicy.ReadOnly);
        public CriticalSection ReaderWriterLockObj { get; } = new CriticalSection();

        T DataInternal = null;
        long StorageHash = 0;

        public CriticalSection DataLock { get; } = new CriticalSection();

        readonly AsyncLock StorageAsyncLock = new AsyncLock();

        readonly Func<T> GetDefaultDataFunc;

        class HiveDataState
        {
            public T Data;
            public Memory<byte> SerializedData;
            public long Hash;
        }

        public HiveData(Hive hive, string dataName, Func<T> getDefaultDataFunc, HiveSyncPolicy policy = HiveSyncPolicy.None)
        {
            if (getDefaultDataFunc == null) throw new ArgumentNullException("getDefaultDataFunc");

            this.Hive = hive;
            this.Policy = policy;
            this.DataName = Hive.NormalizeDataName(dataName);
            this.GetDefaultDataFunc = getDefaultDataFunc;

            if (policy.BitAny(HiveSyncPolicy.AutoReadFromFile | HiveSyncPolicy.AutoWriteToFile))
            {
                this.IsManaged = true;
            }

            if (policy.Bit(HiveSyncPolicy.ReadOnly) && policy.Bit(HiveSyncPolicy.AutoWriteToFile))
            {
                throw new ArgumentException("Invalid flags: ReadOnly is set while AutoWriteToFile is set.");
            }

            if (this.IsManaged)
            {
                if (hive.Options.IsPollingEnabled == false)
                    throw new ArgumentException($"policy = {policy.ToString()} while the Hive object doesn't support polling.");

                this.Hive.CheckPreRegisterInterval(this);
            }

            // Ensure to load the initial data from the storage (or create empty one)
            GetData();

            // Initializing the managed hive
            if (this.IsManaged)
            {
                this.Hive.RegisterInternal(this);
            }
        }

        public T Data { get => GetData(); }

        public T GetData()
        {
            lock (DataLock)
            {
                if (this.DataInternal == null)
                {
                    HiveDataState result;
                    try
                    {
                        // If the data is empty, first try loading from the storage.
                        result = LoadDataCoreAsync().GetResult();
                    }
                    catch
                    {
                        // If the loading failed, then try create an empty one.
                        result = GetDefaultDataState();

                        // Save the initial data to the storage, however prevent to overwrite if the file exists on the storage.
                        try
                        {
                            SaveDataCoreAsync(result.SerializedData, true).GetResult();
                        }
                        catch
                        {
                            // Save to the storage. Perhaps there is a file on the storage, and must not be overwritten to prevent data loss.
                            this.Policy |= HiveSyncPolicy.ReadOnly;
                        }
                    }

                    this.DataInternal = result.Data;
                    this.StorageHash = result.Hash;
                }

                return this.DataInternal;
            }
        }

        public T GetDataSnapshot()
        {
            lock (DataLock)
            {
                T currentData = GetData();

                return currentData.CloneDeep();
            }
        }

        public void SyncWithStorage(HiveSyncFlags flag, CancellationToken cancel = default)
            => SyncWithStorageAsync(flag, cancel).GetResult();

        public async Task SyncWithStorageAsync(HiveSyncFlags flag, CancellationToken cancel = default)
        {
            if (this.IsReadOnly && flag.Bit(HiveSyncFlags.SaveToFile))
                throw new ApplicationException("IsReadOnly is set.");

            T dataSnapshot = GetDataSnapshot();
            HiveDataState dataSnapshotState = GetDataState(dataSnapshot);

            using (await StorageAsyncLock.LockWithAwait(cancel))
            {
                bool skipLoadFromFile = false;

                if (flag.Bit(HiveSyncFlags.SaveToFile))
                {
                    if (this.StorageHash != dataSnapshotState.Hash)
                    {
                        await SaveDataCoreAsync(dataSnapshotState.SerializedData, false, cancel);

                        this.StorageHash = dataSnapshotState.Hash;

                        skipLoadFromFile = true;
                    }
                }

                if (flag.Bit(HiveSyncFlags.LoadFromFile) && skipLoadFromFile == false)
                {
                    HiveDataState loadDataState = await LoadDataCoreAsync(cancel);

                    if (loadDataState.Hash != dataSnapshotState.Hash)
                    {
                        lock (DataLock)
                        {
                            this.DataInternal = loadDataState.Data;
                        }

                        this.StorageHash = loadDataState.Hash;
                    }
                }
            }
        }

        HiveDataState GetDataState(T data)
        {
            HiveDataState ret = new HiveDataState();

            ret.SerializedData = Hive.Serializer.Serialize(data);
            ret.Hash = Secure.HashSHA1AsLong(ret.SerializedData.Span);
            ret.Data = data;

            return ret;
        }

        HiveDataState GetDefaultDataState()
        {
            HiveDataState ret = new HiveDataState();

            T data = this.GetDefaultDataFunc();

            ret.SerializedData = Hive.Serializer.Serialize(data);
            ret.Hash = Secure.HashSHA1AsLong(ret.SerializedData.Span);
            ret.Data = data;

            return ret;
        }

        string cacheForSupressSameError = null;

        async Task<HiveDataState> LoadDataCoreAsync(CancellationToken cancel = default)
        {
            HiveDataState ret = new HiveDataState();

            Memory<byte> loadBytes = await Hive.StorageProvider.LoadAsync(this.DataName, cancel);
            try
            {
                T data = Hive.Serializer.Deserialize<T>(loadBytes);

                ret.SerializedData = Hive.Serializer.Serialize(data);
                ret.Hash = Secure.HashSHA1AsLong(ret.SerializedData.Span);
                ret.Data = data;

                cacheForSupressSameError = null;

                return ret;
            }
            catch (Exception ex)
            {
                if (cacheForSupressSameError.IsSame(ex.Message) == false)
                {
                    cacheForSupressSameError = ex.Message;
                    await Hive.StorageProvider.ReportErrorAsync(this.DataName, ex.ToString(), cancel);
                }
                throw;
            }
        }

        async Task SaveDataCoreAsync(ReadOnlyMemory<byte> serializedData, bool doNotOverwrite, CancellationToken cancel = default)
        {
            await Hive.StorageProvider.SaveAsync(this.DataName, serializedData, doNotOverwrite, cancel);
        }
    }
}

