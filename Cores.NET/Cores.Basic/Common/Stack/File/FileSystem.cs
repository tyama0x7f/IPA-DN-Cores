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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Diagnostics;
using Microsoft.Extensions.FileProviders;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using System.Text;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace IPA.Cores.Basic
{
    public static partial class CoresConfig
    {
        public static partial class FileSystemSettings
        {
            public static readonly Copenhagen<int> PooledHandleLifetime = 60 * 1000;
            public static readonly Copenhagen<int> MaxPooledHandleCount = 256;
            public static readonly Copenhagen<int> DefaultMicroOperationSize = 8 * 1024 * 1024; // 8MB
        }

        public static partial class FileSystemEventWatcherSettings
        {
            public static readonly Copenhagen<int> DefaultPollingInterval = 5 * 1000;
        }

        public static partial class FileUtilSettings
        {
            public static readonly Copenhagen<int> FileCopyBufferSize = 1 * 1024 * 1024; // 1MB
            public static readonly Copenhagen<int> DefaultSectorSize = 4096;
        }
    }

    public class RandomAccessFileObject : FileObject
    {
        readonly IRandomAccess<byte> Access;

        public RandomAccessFileObject(FileSystem fileSystem, FileParameters fileParams, IRandomAccess<byte> baseRandomAccess) : base(fileSystem, fileParams)
        {
            this.Access = baseRandomAccess;
        }

        Once Once;
        protected override Task CloseImplAsync()
        {
            if (Once.IsFirstCall())
            {
                this.Access._DisposeSafe();
            }

            return Task.CompletedTask;
        }

        protected override Task FlushImplAsync(CancellationToken cancel = default)
            => Access.FlushAsync(cancel);

        protected override Task<long> GetFileSizeImplAsync(CancellationToken cancel = default)
            => Access.GetFileSizeAsync(cancel: cancel);

        protected override Task<int> ReadRandomImplAsync(long position, Memory<byte> data, CancellationToken cancel = default)
            => Access.ReadRandomAsync(position, data, cancel);

        protected override Task WriteRandomImplAsync(long position, ReadOnlyMemory<byte> data, CancellationToken cancel = default)
            => Access.WriteRandomAsync(position, data, cancel);

        protected override Task SetFileSizeImplAsync(long size, CancellationToken cancel = default)
            => Access.SetFileSizeAsync(size, cancel);
    }

    public class FileSystemException : Exception
    {
        public FileSystemException(string message) : base(message) { }
    }

    public abstract class FileObject : FileBase
    {
        public FileSystem FileSystem { get; }
        public sealed override bool IsOpened => !this.ClosedFlag.IsSet;
        public sealed override Exception LastError { get; protected set; } = null;

        public int MicroOperationSize { get; set; } = CoresConfig.FileSystemSettings.DefaultMicroOperationSize.Value;

        long InternalPosition = 0;
        long InternalFileSize = 0;
        CancellationTokenSource CancelSource = new CancellationTokenSource();
        CancellationToken CancelToken => CancelSource.Token;

        AsyncLock AsyncLockObj = new AsyncLock();

        protected FileObject(FileSystem fileSystem, FileParameters fileParams) : base(fileParams)
        {
            this.FileSystem = fileSystem;

            Con.WriteTrace($"CreateFile ({FileSystem.ToString()}): '{fileParams.Path}'");
        }

        public override string ToString() => $"FileObject('{FileParams.Path}')";

        protected void InitAndCheckFileSizeAndPosition(long initialPosition, long initialFileSize, CancellationToken cancel = default)
        {
            using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
            {
                this.InternalPosition = initialPosition;
                this.InternalFileSize = initialFileSize;

                if (this.InternalPosition > this.InternalFileSize)
                    throw new FileException(this.FileParams.Path, $"Current position is out of range. Current position: {this.InternalPosition}, File size: {this.InternalFileSize}.");

                if (this.InternalPosition < 0)
                    throw new FileException(this.FileParams.Path, $"Current position is invalid. Current position: {this.InternalPosition}.");

                if (this.InternalFileSize < 0)
                    throw new FileException(this.FileParams.Path, $"Current filesize is invalid. Current filesize: {this.InternalFileSize}.");
            }
        }

        protected abstract Task<int> ReadRandomImplAsync(long position, Memory<byte> data, CancellationToken cancel = default);
        protected abstract Task WriteRandomImplAsync(long position, ReadOnlyMemory<byte> data, CancellationToken cancel = default);
        protected abstract Task<long> GetFileSizeImplAsync(CancellationToken cancel = default);
        protected abstract Task SetFileSizeImplAsync(long size, CancellationToken cancel = default);
        protected abstract Task FlushImplAsync(CancellationToken cancel = default);
        protected abstract Task CloseImplAsync();

        Once ClosedFlag;

        public sealed override async Task<int> ReadAsync(Memory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    CheckSequentialAccessProhibited();

                    EventListeners.Fire(this, FileObjectEventType.Read);

                    if (data.IsEmpty) return 0;

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            CheckAccessBit(FileAccess.Read);

                            if (this.InternalFileSize < this.InternalPosition)
                            {
                                await GetFileSizeInternalAsync(true, operationCancel);
                                if (this.InternalFileSize < this.InternalPosition)
                                {
                                    await GetFileSizeInternalAsync(true, operationCancel);
                                    throw new FileException(this.FileParams.Path, $"Current position is out of range. Current position: {this.InternalPosition}, File size: {this.InternalFileSize}.");
                                }
                            }

                            long newPosition = this.InternalPosition + data.Length;
                            if (this.FileParams.Flags.Bit(FileFlags.NoPartialRead))
                            {
                                if (this.InternalFileSize < newPosition)
                                {
                                    await GetFileSizeInternalAsync(true, operationCancel);
                                    if (this.InternalFileSize < newPosition)
                                        throw new FileException(this.FileParams.Path, $"New position is out of range. New position: {newPosition}, File size: {this.InternalFileSize}.");
                                }
                            }

                            long originalPosition = this.InternalPosition;

                            operationCancel.ThrowIfCancellationRequested();

                            try
                            {
                                int r = await TaskUtil.DoMicroReadOperations(async (target, pos, c) =>
                                {
                                    return await ReadRandomImplAsync(pos, target, c);
                                },
                                data, MicroOperationSize, this.InternalPosition, operationCancel);

                                if (r < 0) throw new FileException(this.FileParams.Path, $"ReadImplAsync returned {r}.");

                                if (this.FileParams.Flags.Bit(FileFlags.NoPartialRead))
                                    if (r != data.Length)
                                        throw new FileException(this.FileParams.Path, $"ReadImplAsync returned {r} while {data.Length} requested.");

                                this.InternalPosition += r;

                                return r;
                            }
                            catch
                            {
                                this.InternalPosition = originalPosition;
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task<int> ReadRandomAsync(long position, Memory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    if (position < 0) throw new ArgumentOutOfRangeException("position < 0");
                    EventListeners.Fire(this, FileObjectEventType.ReadRandom);

                    if (data.IsEmpty) return 0;

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            CheckAccessBit(FileAccess.Read);

                            if (this.InternalFileSize < position)
                            {
                                await GetFileSizeInternalAsync(true, operationCancel);
                                if (this.InternalFileSize < position)
                                    throw new FileException(this.FileParams.Path, $"The random position is out of range. Position: {position}, File size: {this.InternalFileSize}.");
                            }

                            long newPosition = position + data.Length;
                            if (this.FileParams.Flags.Bit(FileFlags.NoPartialRead))
                            {
                                if (this.InternalFileSize < newPosition)
                                {
                                    await GetFileSizeInternalAsync(true, operationCancel);
                                    if (this.InternalFileSize < newPosition)
                                        throw new FileException(this.FileParams.Path, $"New position is out of range. New position: {newPosition}, File size: {this.InternalFileSize}.");
                                }
                            }

                            operationCancel.ThrowIfCancellationRequested();

                            try
                            {
                                int r = await TaskUtil.DoMicroReadOperations(async (target, pos, c) =>
                                {
                                    return await ReadRandomImplAsync(pos, target, c);
                                },
                                data, MicroOperationSize, position, operationCancel);

                                if (r < 0) throw new FileException(this.FileParams.Path, $"ReadImplAsync returned {r}.");

                                if (this.FileParams.Flags.Bit(FileFlags.NoPartialRead))
                                    if (r != data.Length)
                                        throw new FileException(this.FileParams.Path, $"ReadImplAsync returned {r} while {data.Length} requested.");

                                return r;
                            }
                            catch
                            {
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    CheckSequentialAccessProhibited();

                    EventListeners.Fire(this, FileObjectEventType.Write);

                    if (data.IsEmpty) return;

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            CheckAccessBit(FileAccess.Write);

                            if (this.InternalFileSize < this.InternalPosition)
                            {
                                await GetFileSizeInternalAsync(true, operationCancel);
                                if (this.InternalFileSize < this.InternalPosition)
                                    throw new FileException(this.FileParams.Path, $"Current position is out of range. Current position: {this.InternalPosition}, File size: {this.InternalFileSize}.");
                            }

                            operationCancel.ThrowIfCancellationRequested();

                            await TaskUtil.DoMicroWriteOperations(async (target, pos, c) =>
                            {
                                await WriteRandomImplAsync(pos, target, c);
                            },
                            data, MicroOperationSize, this.InternalPosition, operationCancel);

                            this.InternalPosition += data.Length;

                            if (this.InternalFileSize < this.InternalPosition)
                            {
                                this.InternalFileSize = this.InternalPosition;
                            }

                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task WriteRandomAsync(long position, ReadOnlyMemory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    EventListeners.Fire(this, FileObjectEventType.WriteRandom);

                    if (data.IsEmpty) return;

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            CheckAccessBit(FileAccess.Write);

                            if (position < 0)
                            {
                                // Append mode
                                position = this.InternalFileSize;
                            }

                            if (this.InternalFileSize < position)
                            {
                                await GetFileSizeInternalAsync(true, operationCancel);

                                if (this.InternalFileSize < position)
                                {
                                    await SetFileSizeImplAsync(position, operationCancel);
                                }
                            }

                            operationCancel.ThrowIfCancellationRequested();

                            await TaskUtil.DoMicroWriteOperations(async (target, pos, c) =>
                            {
                                await WriteRandomImplAsync(pos, target, c);

                                if (this.InternalFileSize < (pos + target.Length))
                                    this.InternalFileSize = (pos + target.Length);
                            },
                            data, MicroOperationSize, position, operationCancel);

                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task<long> SeekAsync(long offset, SeekOrigin origin, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    CheckSequentialAccessProhibited();

                    EventListeners.Fire(this, FileObjectEventType.Seek);

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            long newPosition = 0;

                            if (origin == SeekOrigin.Begin)
                                newPosition = offset;
                            else if (origin == SeekOrigin.Current)
                                newPosition = this.InternalPosition + offset;
                            else if (origin == SeekOrigin.End)
                                newPosition = (await GetFileSizeInternalAsync(true, operationCancel)) + offset;
                            else
                                throw new FileException(this.FileParams.Path, $"Invalid origin value: {(int)origin}");

                            if (newPosition < 0)
                                throw new FileException(this.FileParams.Path, $"newPosition < 0");

                            if (this.InternalFileSize < newPosition)
                            {
                                await GetFileSizeInternalAsync(true, operationCancel);
                                if (this.InternalFileSize < newPosition)
                                {
                                    if (this.FileParams.Access.Bit(FileAccess.Write) == false)
                                        throw new FileException(this.FileParams.Path, $"New position is out of range. New position: {newPosition}, File size: {this.InternalFileSize}.");
                                }
                            }

                            if (this.InternalPosition != newPosition)
                            {
                                this.InternalPosition = newPosition;
                            }

                            return this.InternalPosition;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override Task<long> GetCurrentPositionAsync(CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            CheckSequentialAccessProhibited();

            return Task.FromResult(this.InternalPosition);
        }

        async Task<long> GetFileSizeInternalAsync(bool refresh, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    if (refresh == false)
                        return this.InternalFileSize;

                    cancel.ThrowIfCancellationRequested();

                    long r = await GetFileSizeImplAsync(cancel);

                    if (r < 0)
                        throw new FileException(this.FileParams.Path, $"GetFileSizeImplAsync returned {r}.");

                    this.InternalFileSize = r;

                    return r;
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task<long> GetFileSizeAsync(bool refresh = false, CancellationToken cancel = default)
        {
            checked
            {
                try
                {
                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();

                            return await GetFileSizeInternalAsync(refresh, cancel);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task SetFileSizeAsync(long size, CancellationToken cancel = default)
        {
            checked
            {
                if (size < 0)
                    throw new ArgumentOutOfRangeException("size < 0");

                try
                {
                    EventListeners.Fire(this, FileObjectEventType.SetFileSize);

                    using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                    {
                        using (await AsyncLockObj.LockWithAwait(operationCancel))
                        {
                            CheckIsOpened();
                            CheckAccessBit(FileAccess.Write);

                            operationCancel.ThrowIfCancellationRequested();

                            await SetFileSizeImplAsync(size, operationCancel);

                            this.InternalFileSize = size;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.LastError = ex;
                    throw;
                }
            }
        }

        public sealed override async Task FlushAsync(CancellationToken cancel = default)
        {
            try
            {
                EventListeners.Fire(this, FileObjectEventType.Flush);

                using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
                {
                    using (await AsyncLockObj.LockWithAwait(operationCancel))
                    {
                        CheckIsOpened();

                        operationCancel.ThrowIfCancellationRequested();

                        await FlushImplAsync(operationCancel);
                    }
                }
            }
            catch (Exception ex)
            {
                this.LastError = ex;
                throw;
            }
        }

        public sealed override async Task CloseAsync()
        {
            Con.WriteTrace($"CloseAsync({this.FileSystem}) '{FileParams.Path}'");

            CancelSource._TryCancelNoBlock();

            if (ClosedFlag.IsSet) return;

            using (await AsyncLockObj.LockWithAwait())
            {
                if (ClosedFlag.IsFirstCall())
                {
                    this.LastError = new ApplicationException($"File '{this.FileParams.Path}' is closed.");

                    try
                    {
                        await CloseImplAsync();
                    }
                    finally
                    {
                        EventListeners.Fire(this, FileObjectEventType.Closed);
                    }
                }
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                CancelSource._TryCancelNoBlock();
            }
            finally { base.Dispose(disposing); }
        }
    }

    public class FileSystemEntity
    {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public bool IsDirectory => Attributes.Bit(FileAttributes.Directory);
        public bool IsFile => !IsDirectory;
        public bool IsSymbolicLink => Attributes.Bit(FileAttributes.ReparsePoint);
        public bool IsCurrentDirectory => (Name == ".");
        public long Size { get; set; }
        public long PhysicalSize { get; set; }
        public string SymbolicLinkTarget { get; set; }
        public FileAttributes Attributes { get; set; }
        public DateTimeOffset CreationTime { get; set; }
        public DateTimeOffset LastWriteTime { get; set; }
        public DateTimeOffset LastAccessTime { get; set; }
    }

    [Flags]
    public enum SpecialFileNameKind
    {
        Normal = 0,
        CurrentDirectory = 1,
        ParentDirectory = 2,
    }

    public class FileSystemObjectPool : ObjectPoolBase<FileBase, FileFlags>
    {
        public FileSystem FileSystem { get; }
        public FileFlags DefaultFileOperationFlags { get; }
        public bool IsWriteMode { get; }

        public FileSystemObjectPool(FileSystem FileSystem, bool writeMode, int lifeTime, int maxObjects, FileFlags defaultFileOperationFlags = FileFlags.None)
            : base(lifeTime, maxObjects, new StrComparer(FileSystem.PathParser.PathStringComparison))
        {
            this.FileSystem = FileSystem;
            this.IsWriteMode = writeMode;

            this.DefaultFileOperationFlags = defaultFileOperationFlags;
            this.DefaultFileOperationFlags |= FileFlags.AutoCreateDirectory | FileFlags.RandomAccessOnly;
        }

        protected override async Task<FileBase> OpenImplAsync(string name, FileFlags flags, CancellationToken cancel)
        {
            if (this.IsWriteMode == false)
            {
                string path = await FileSystem.NormalizePathAsync(name, cancel: cancel);

                return await FileSystem.OpenAsync(path, cancel: cancel, flags: this.DefaultFileOperationFlags | flags);
            }
            else
            {
                string path = await FileSystem.NormalizePathAsync(name, cancel: cancel);

                return await FileSystem.OpenOrCreateAsync(path, cancel: cancel, flags: this.DefaultFileOperationFlags | flags);
            }
        }
    }

    [Flags]
    public enum FileSystemStyle
    {
        Windows = 0,
        Linux = 1,
        Mac = 2,
        // append above

        LocalSystem = 31,
    }

    public class PathParser
    {
        public static FileSystemStyle LocalSystemStyle { get; } = Env.IsWindows ? FileSystemStyle.Windows : (Env.IsMac ? FileSystemStyle.Mac : FileSystemStyle.Linux);

        readonly static PathParser[] Cached = new PathParser[(int)Util.GetMaxEnumValue<FileSystemStyle>() + 1];

        public FileSystemStyle Style { get; }
        public char DirectorySeparator { get; }
        public char[] PossibleDirectorySeparators { get; }
        public StringComparison PathStringComparison { get; }
        public StrComparer PathStringComparer { get; }

        readonly char[] InvalidPathChars;
        readonly char[] InvalidFileNameChars;

        public static PathParser Local { get => PathParser.GetInstance(FileSystemStyle.LocalSystem); }
        public static PathParser Windows { get => PathParser.GetInstance(FileSystemStyle.Windows); }
        public static PathParser Linux { get => PathParser.GetInstance(FileSystemStyle.Linux); }
        public static PathParser Mac { get => PathParser.GetInstance(FileSystemStyle.Mac); }

        public static PathParser GetInstance(FileSystemStyle style = FileSystemStyle.LocalSystem)
        {
            if (style == FileSystemStyle.LocalSystem)
                style = LocalSystemStyle;

            if (Cached[(int)style] == null)
            {
                PathParser newObj = new PathParser(style);
                Cached[(int)style] = newObj;
                return newObj;
            }
            else
            {
                return Cached[(int)style];
            }
        }

        private PathParser(FileSystemStyle style)
        {
            Debug.Assert(style != FileSystemStyle.LocalSystem);

            this.Style = style;

            switch (this.Style)
            {
                case FileSystemStyle.Windows:
                    this.DirectorySeparator = '\\';
                    this.PathStringComparison = StringComparison.OrdinalIgnoreCase;
                    this.PossibleDirectorySeparators = new char[] { '\\', '/'};
                    break;

                case FileSystemStyle.Mac:
                    this.DirectorySeparator = '/';
                    this.PathStringComparison = StringComparison.OrdinalIgnoreCase;
                    this.PossibleDirectorySeparators = new char[] { '/' };
                    break;

                default:
                    this.DirectorySeparator = '/';
                    this.PathStringComparison = StringComparison.Ordinal;
                    this.PossibleDirectorySeparators = new char[] { '/' };
                    break;
            }

            this.PathStringComparer = new StrComparer(this.PathStringComparison);

            this.InvalidPathChars = GetInvalidPathChars();
            this.InvalidFileNameChars = GetInvalidFileNameChars();
        }

        static readonly char[] PossibleDirectorySeparatorsForAllPlatform = new char[] { '\\', '/'};
        public string RemoveDangerousDirectoryTraversal(string path)
        {
            path = path._NonNull();

            string[] tokens = path.Split(PossibleDirectorySeparatorsForAllPlatform, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; i++)
            {
                string s = tokens[i]._NonNullTrim();

                if (s == "." || s == "..")
                    tokens[i] = "";
            }

            return BuildAbsolutePathStringFromElements(tokens);
        }

        public string NormalizeUnixStylePathWithRemovingRelativeDirectoryElements(string path)
        {
            Debug.Assert(this.Style != FileSystemStyle.Windows);

            return BuildAbsolutePathStringFromElements(SplitAbsolutePathToElementsUnixStyle(path));
        }

        public string[] SplitAbsolutePathToElementsUnixStyle(string path)
        {
            Debug.Assert(this.Style != FileSystemStyle.Windows);
            path = path._NonNull();

            if (path.StartsWith("/") == false)
                throw new ArgumentException($"The speficied path \"{path}\" is not an absolute path.");

            List<string> pathStack = new List<string>();

            string[] tokens = path.Split(this.PossibleDirectorySeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (string s in tokens)
            {
                string trimmed = s.Trim();
                if (trimmed == ".") { }
                else if (trimmed == "..")
                {
                    if (pathStack.Count >= 1)
                        pathStack.RemoveAt(pathStack.Count - 1);
                }
                else
                {
                    pathStack.Add(s);
                }
            }

            return pathStack.ToArray();
        }

        public string BuildAbsolutePathStringFromElements(IEnumerable<string> elements)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string name in elements)
            {
                if (name != null && name.Length >= 1)
                {
                    if (this.Style != FileSystemStyle.Windows || sb.Length >= 1)
                        sb.Append(this.DirectorySeparator);

                    sb.Append(name);
                }
            }

            if (sb.Length == 0)
                sb.Append(this.DirectorySeparator);

            return sb.ToString();
        }

        public bool IsLastCharDirectoryDirectorySeparator(string path)
        {
            path = path._NonNull();

            if (path.Length >= 1)
            {
                char c = path.Last();
                return this.PossibleDirectorySeparators.Where(x => x == c).Any();
            }

            return false;
        }

        public string NormalizeDirectorySeparatorIncludeWindowsBackslash(string srcPath)
        {
            srcPath = srcPath._NonNull();

            StringBuilder sb = new StringBuilder();
            foreach (char c in srcPath)
            {
                char d = c;
                if (d == '\\' || d == '/')
                {
                    d = this.DirectorySeparator;
                }
                sb.Append(d);
            }
            return sb.ToString();
        }

        public string NormalizeDirectorySeparator(string srcPath)
        {
            srcPath = srcPath._NonNull();

            if (this.Style == FileSystemStyle.Windows)
                srcPath = srcPath.TrimStart();

            int mode = -1;

            StringBuilder sb = new StringBuilder();
            foreach (char c in srcPath)
            {
                bool isDirectorySeparatorChar = false;
                foreach (char pos in PossibleDirectorySeparators)
                    if (pos == c) isDirectorySeparatorChar = true;

                char d = c;

                if (isDirectorySeparatorChar)
                    d = this.DirectorySeparator;

                if (isDirectorySeparatorChar == false)
                {
                    sb.Append(d);
                    mode = 0;
                }
                else
                {
                    if (mode != 1)
                    {
                        sb.Append(d);
                        mode = 1;
                    }
                    else
                    {
                        if (this.Style == FileSystemStyle.Windows)
                        {
                            if (sb.Length == 1)
                            {
                                sb.Append(d);
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public bool IsAbsolutePath(string path, bool normalizeDirectorySeparator = false)
        {
            if (normalizeDirectorySeparator)
                path = NormalizeDirectorySeparator(path);

            if (this.Style == FileSystemStyle.Windows)
            {
                // C:\path
                if (path.Length >= 2 && ((path[0] >= 'a' && path[0] <= 'z') || (path[0] >= 'A' && path[0] <= 'Z')) && path[1] == ':')
                    return true;

                // \\server\name
                if (path.Length >= 3 && path[0] == '\\' && path[1] == '\\' && path[2] != '\\')
                    return true;

                return false;
            }
            else
            {
                if (path.Length >= 1 && path[0] == '/')
                    return true;

                return false;
            }
        }

        public string NormalizeDirectorySeparatorAndCheckIfAbsolutePath(string srcPath)
        {
            srcPath = NormalizeDirectorySeparator(srcPath);

            if (IsAbsolutePath(srcPath) == false)
            {
                throw new ArgumentException($"The specified path \"{srcPath}\" is not an absolute path.");
            }

            return srcPath;
        }

        public string ConvertDirectorySeparatorToOtherSystem(string srcPath, PathParser destPathParser)
        {
            srcPath = srcPath._NonNull();

            StringBuilder sb = new StringBuilder();
            foreach (char c in srcPath)
            {
                char d = c;
                foreach (char sep in this.PossibleDirectorySeparators)
                {
                    if (sep == c)
                    {
                        d = destPathParser.DirectorySeparator;
                        break;
                    }
                }
                sb.Append(d);
            }
            return sb.ToString();
        }

        public void ValidateFileOrDirectoryName(string name)
        {
            if (name == null || name == "")
                throw new ArgumentNullException("The entity name is null or empty.");
            if (IsValidFileOrDirectoryName(name) == false)
                throw new ArgumentException($"The entity name \"{name}\" is invalid to this file system.");
        }

        public bool IsValidFileOrDirectoryName(string name)
        {
            if (name == null || name == "") return false;
            string trimmed = name.Trim();
            if (trimmed == "." || trimmed == "..") return false;

            foreach (char c in name)
                foreach (char sep in this.PossibleDirectorySeparators)
                    if (c == sep)
                        return false;

            return true;
        }

        public string RemoveLastSeparatorChar(string path)
        {
            path = path._NonNull();

            if (path.All(c => PossibleDirectorySeparators.Where(x => x == c).Any()))
            {
                return path;
            }

            if (path.Length == 3 &&
                ((path[0] >= 'a' && path[0] <= 'z') || (path[0] >= 'A' && path[0] <= 'Z')) &&
                path[1] == ':' &&
                PossibleDirectorySeparators.Where(x => x == path[2]).Any())
            {
                return path;
            }

            while (path.Length >= 1)
            {
                char c = path[path.Length - 1];
                if (PossibleDirectorySeparators.Where(x => x == c).Any())
                {
                    path = path.Substring(0, path.Length - 1);
                }
                else
                {
                    break;
                }
            }

            return path;
        }

        public bool IsRootDirectory(string path)
        {
            if (path == null) return false;

            SepareteDirectoryAndFileName(path, out string dirPath, out string fileName);

            return fileName._IsEmpty();
        }

        public string GetDirectoryName(string path)
        {
            if (path == null) return null;
            SepareteDirectoryAndFileName(path, out string dirPath, out _);
            return dirPath;
        }

        public string GetFileName(string path)
        {
            if (path == null) return null;
            SepareteDirectoryAndFileName(path, out _, out string fileName);
            return fileName;
        }

        public string GetRelativeFileName(string fileName, string baseDirName)
        {
            fileName = fileName._TrimNonNull();
            baseDirName = baseDirName._TrimNonNull();

            baseDirName = this.NormalizeDirectorySeparator(baseDirName);
            baseDirName = this.RemoveLastSeparatorChar(baseDirName);

            if (baseDirName.Length == 0) throw new ArgumentException("baseDirName is empty.");

            fileName = this.NormalizeDirectorySeparator(fileName);
            fileName = this.RemoveLastSeparatorChar(fileName);

            if (fileName.Length < baseDirName.Length)
            {
                throw new ArgumentException("fileName.Length < baseDirName.Length");
            }

            if (fileName.StartsWith(baseDirName, this.PathStringComparison) == false)
            {
                throw new ArgumentException($"The fileName \"{fileName}\" does not include the baseDirName \"{baseDirName}\".");
            }

            string ret = fileName.Substring(baseDirName.Length);

            if (ret.Length >= 1 && this.PossibleDirectorySeparators.Where(x => x == ret[0]).Any())
            {
                ret = ret.Substring(1);
            }

            return ret;
        }

        public string Combine(string path1, string path2)
            => Combine(path1, path2, false);
        public string Combine(string path1, string path2, bool path2NeverAbsolutePath = false)
        {
            if (path1 == null && path2 == null) return null;

            path1 = path1._NonNull();
            path2 = path2._NonNull();

            if (path1._IsEmpty())
            {
                if (path2NeverAbsolutePath == false)
                    return path2;
                else
                    return "";
            }

            if (path2._IsEmpty())
                return path1;

            if (path2.Length >= 1)
            {
                if (path2NeverAbsolutePath == false)
                {
                    if (PossibleDirectorySeparators.Where(x => x == path2[0]).Any())
                        return path2;
                }
            }

            path1 = RemoveLastSeparatorChar(path1);

            string sepStr = "" + this.DirectorySeparator;
            if (path1.Length >= 1 && PossibleDirectorySeparators.Where(x => x == path1[path1.Length - 1]).Any())
            {
                sepStr = "";
            }

            return path1 + sepStr + path2;
        }

        public string Combine(params string[] pathList)
        {
            if (pathList == null || pathList.Length == 0) return null;
            if (pathList.Length == 1) return pathList[0];

            string path1 = pathList[0];

            for (int i = 0; i < pathList.Length - 1; i++)
            {
                string path2 = pathList[i + 1];

                path1 = Combine(path1, path2);
            }

            return path1;
        }

        public string GetFileNameWithoutExtension(string path, bool longExtension = false)
        {
            if (path == null) return null;
            if (path._IsEmpty()) return "";
            path = GetFileName(path);
            int[] dots = path._FindStringIndexes(".", true);
            if (dots.Length == 0)
                return path;

            int i = longExtension ? dots.First() : dots.Last();
            return path.Substring(0, i);
        }

        public string GetExtension(string path, bool longExtension = false)
        {
            if (path == null) return null;
            if (path._IsEmpty()) return "";
            path = GetFileName(path);
            int[] dots = path._FindStringIndexes(".", true);
            if (dots.Length == 0)
                return path;

            int i = longExtension ? dots.First() : dots.Last();
            return path.Substring(i);
        }

        public void Abc()
        {
        }

        public void SepareteDirectoryAndFileName(string path, out string dirPath, out string fileName)
        {
            if (path._IsEmpty())
                throw new ArgumentNullException("path");

            path = path._NonNull();

            int i = 0;

            // Skip head separators (e.g. /usr/local/ or \\server\share\)
            for (int j = 0; j < path.Length; j++)
            {
                char c = path[j];

                if (PossibleDirectorySeparators.Where(x => x == c).Any())
                {
                    i = j;
                }
                else
                {
                    break;
                }
            }

            if (path.StartsWith(@"\\") || path.StartsWith(@"//"))
            {
                // Windows UNC Path
                for (int j = 2; j < path.Length; j++)
                {
                    char c = path[j];

                    if (PossibleDirectorySeparators.Where(x => x == c).Any())
                    {
                        break;
                    }
                    else
                    {
                        i = j;
                    }
                }
            }

            int lastMatch = -1;
            while (true)
            {
                i = path.IndexOfAny(this.PossibleDirectorySeparators, i);
                if (i == -1)
                {
                    break;
                }
                else
                {
                    lastMatch = i;
                    i++;
                }
            }

            if (lastMatch == -1)
            {
                if (path.Any(c => PossibleDirectorySeparators.Where(x => x == c).Any()))
                {
                    dirPath = RemoveLastSeparatorChar(path);
                    fileName = "";
                }
                else
                {
                    dirPath = "";
                    fileName = path;
                }
            }
            else
            {
                dirPath = RemoveLastSeparatorChar(path.Substring(0, lastMatch + 1));
                fileName = path.Substring(lastMatch + 1);
            }
        }

        public bool IsValidPathChars(string path)
        {
            if (this.Style == FileSystemStyle.Windows)
                return Win32PathInternal.IsValidPathChars(path);
            else
                return UnixPathInternal.IsValidPathChars(path);
        }

        public bool IsValidFileNameChars(string fileName)
        {
            if (this.Style == FileSystemStyle.Windows)
                return Win32PathInternal.IsValidFileNameChars(fileName);
            else
                return UnixPathInternal.IsValidFileNameChars(fileName);
        }

        public char[] GetInvalidFileNameChars()
        {
            if (this.Style == FileSystemStyle.Windows)
                return Win32PathInternal.GetInvalidFileNameChars();
            else
                return UnixPathInternal.GetInvalidFileNameChars();
        }

        public char[] GetInvalidPathChars()
        {
            if (this.Style == FileSystemStyle.Windows)
                return Win32PathInternal.GetInvalidPathChars();
            else
                return UnixPathInternal.GetInvalidPathChars();
        }

        public string MakeSafePathName(string name)
        {
            char[] a = name.ToCharArray();
            StringBuilder sb = new StringBuilder();

            int i;
            for (i = 0; i < a.Length; i++)
            {
                int j;
                bool ok = true;

                for (j = 0; j < InvalidPathChars.Length; j++)
                {
                    if (InvalidPathChars[j] == a[i])
                    {
                        ok = false;
                        break;
                    }
                }

                if (a[i] == '\\' || a[i] == '/')
                {
                    ok = true;
                    a[i] = this.DirectorySeparator;
                }

                if (i == 1 && a[i] == ':')
                {
                    ok = true;
                }

                string s;

                if (ok == false)
                {
                    s = "_" + ((int)a[i]).ToString() + "_";
                }
                else
                {
                    s = "" + a[i];
                }

                sb.Append(s);
            }

            return sb.ToString();
        }

        public string MakeSafeFileName(string name)
        {
            char[] a = name.ToCharArray();
            StringBuilder sb = new StringBuilder();

            int i;
            for (i = 0; i < a.Length; i++)
            {
                int j;
                bool ok = true;

                for (j = 0; j < InvalidFileNameChars.Length; j++)
                {
                    if (InvalidFileNameChars[j] == a[i])
                    {
                        ok = false;
                        break;
                    }
                }

                string s;

                if (ok == false)
                {
                    s = "_" + ((int)a[i]).ToString() + "_";
                }
                else
                {
                    s = "" + a[i];
                }

                sb.Append(s);
            }

            string ret = sb.ToString();

            string trim = ret.Trim();
            if (trim == "." || trim == "..") ret = "_";

            return ret;
        }
    }

    [Flags]
    public enum EnumDirectoryFlags
    {
        None = 0,
        NoGetPhysicalSize = 1,
    }

    [Flags]
    public enum EasyAccessPathFindMode
    {
        NotSupported,
        ExactFullPath,
        MostMatch,
        MostMatchExact,
    }

    [Flags]
    public enum FileSystemMode
    {
        ReadOnly = 0,
        Writeable = 1,

        Default = Writeable,
    }

    public class FileSystemParams
    {
        public PathParser PathParser { get; }
        public Copenhagen<EasyAccessPathFindMode> EasyAccessPathFindMode { get; } = new Copenhagen<EasyAccessPathFindMode>(Basic.EasyAccessPathFindMode.NotSupported);
        public FileSystemMode Mode { get; }

        public FileSystemParams(PathParser pathParser, FileSystemMode mode = FileSystemMode.Default)
        {
            this.PathParser = pathParser;
            this.Mode = mode;
        }
    }

    public abstract partial class FileSystem : AsyncService
    {
        public DirectoryWalker DirectoryWalker { get; }
        public PathParser PathParser { get; }
        protected FileSystemParams Params { get; }
        public bool CanWrite => Params.Mode.Bit(FileSystemMode.Writeable);

        CriticalSection LockObj = new CriticalSection();
        HashSet<FileBase> OpenedHandleList = new HashSet<FileBase>();

        public Singleton<FileSystemObjectPool> ObjectPoolForRead { get; }
        public Singleton<FileSystemObjectPool> ObjectPoolForWrite { get; }

        public LargeFileSystem LargeFileSystem { get; }

        public FileSystem(FileSystemParams param) : base()
        {
            this.Params = param;
            this.PathParser = this.Params.PathParser;
            DirectoryWalker = new DirectoryWalker(this);

            ObjectPoolForRead = new Singleton<FileSystemObjectPool>(() => new FileSystemObjectPool(this, false, CoresConfig.FileSystemSettings.PooledHandleLifetime, CoresConfig.FileSystemSettings.MaxPooledHandleCount));
            ObjectPoolForWrite = new Singleton<FileSystemObjectPool>(() => new FileSystemObjectPool(this, true, CoresConfig.FileSystemSettings.PooledHandleLifetime, CoresConfig.FileSystemSettings.MaxPooledHandleCount));

            try
            {
                InitEasyFileAccessSingleton();
            }
            catch { }
        }

        internal DisposableFileProvider _CreateFileProviderForWatchInternal(EnsureInternal yes, string root, bool noDispose = false)
        {
            IFileProvider p = this.CreateFileProviderForWatchImpl(root);

            return new DisposableFileProvider(p, noDispose);
        }

        protected void CheckWriteable(string path)
        {
            if (this.CanWrite == false)
                throw new FileException(path, "This file system is read-only mode.");
        }

        public async Task<RandomAccessHandle> GetRandomAccessHandleAsync(string fileName, bool writeMode, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
        {
            CheckNotCanceled();

            if (writeMode) CheckWriteable(fileName);

            FileSystemObjectPool pool = writeMode ? ObjectPoolForWrite : ObjectPoolForRead;

            RefCounterObjectHandle<FileBase> refFileBase = await pool.OpenOrGetAsync(fileName, flags, cancel);

            return new RandomAccessHandle(refFileBase);
        }
        public RandomAccessHandle GetRandomAccessHandle(string fileName, bool writeMode, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => GetRandomAccessHandleAsync(fileName, writeMode, flags, cancel)._GetResult();

        protected override void CancelImpl(Exception ex) { }

        protected override async Task CleanupImplAsync(Exception ex)
        {
            FileBase[] fileHandles;

            lock (LockObj)
            {
                fileHandles = OpenedHandleList.ToArray();
                OpenedHandleList.Clear();
            }

            foreach (var fileHandle in fileHandles)
            {
                await fileHandle.CloseAsync();
            }
        }

        protected override void DisposeImpl(Exception ex)
        {
            ObjectPoolForRead._DisposeSafe();
            ObjectPoolForWrite._DisposeSafe();
        }

        protected abstract Task<string> NormalizePathImplAsync(string path, CancellationToken cancel = default);

        protected abstract Task<FileObject> CreateFileImplAsync(FileParameters option, CancellationToken cancel = default);
        protected abstract Task DeleteFileImplAsync(string path, FileFlags flags = FileFlags.None, CancellationToken cancel = default);

        protected abstract Task CreateDirectoryImplAsync(string directoryPath, FileFlags flags = FileFlags.None, CancellationToken cancel = default);
        protected abstract Task DeleteDirectoryImplAsync(string directoryPath, bool recursive, CancellationToken cancel = default);
        protected abstract Task<FileSystemEntity[]> EnumDirectoryImplAsync(string directoryPath, EnumDirectoryFlags flags, CancellationToken cancel = default);

        protected abstract Task<FileMetadata> GetFileMetadataImplAsync(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default);
        protected abstract Task SetFileMetadataImplAsync(string path, FileMetadata metadata, CancellationToken cancel = default);

        protected abstract Task<FileMetadata> GetDirectoryMetadataImplAsync(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default);
        protected abstract Task SetDirectoryMetadataImplAsync(string path, FileMetadata metadata, CancellationToken cancel = default);

        protected abstract Task MoveFileImplAsync(string srcPath, string destPath, CancellationToken cancel = default);
        protected abstract Task MoveDirectoryImplAsync(string srcPath, string destPath, CancellationToken cancel = default);

        protected abstract Task<bool> IsFileExistsImplAsync(string path, CancellationToken cancel = default);
        protected abstract Task<bool> IsDirectoryExistsImplAsync(string path, CancellationToken cancel = default);

        protected abstract IFileProvider CreateFileProviderForWatchImpl(string root);

        protected IFileProvider CreateDefaultNullFileProvider() => new NullFileProvider();

        readonly ConcurrentDictionary<string, string> CaseCorrectionCache = new ConcurrentDictionary<string, string>(StrComparer.IgnoreCaseComparer);

        public void FlushNormalizedCaseCorrectionCache()
        {
            CaseCorrectionCache.Clear();
        }

        async Task<string> NormalizePathWithCaseCorrectionInternalAsync(string path, bool forDirectory, CancellationToken cancel = default)
        {
            path = PathParser.NormalizeDirectorySeparatorAndCheckIfAbsolutePath(path);

            if (PathParser.Style == FileSystemStyle.Windows) return path;

            string[] elements = PathParser.SplitAbsolutePathToElementsUnixStyle(path);

            string cacheKey = PathParser.Combine(elements);

            if (CaseCorrectionCache.TryGetValue(cacheKey, out string cachedValue))
            {
                return cachedValue;
            }

            string currentFullPath = "/";

            for (int i = 0; i < elements.Length;i++)
            {
                string element = elements[i];

                bool isThisElementDirectory = forDirectory || (i != (elements.Length - 1));

                string originalFullPath = PathParser.Combine(currentFullPath, element);
                string element2 = null;

                try
                {
                    if ((forDirectory == false && await this.IsFileExistsImplAsync(originalFullPath, cancel)) ||
                        (forDirectory == true && await this.IsDirectoryExistsImplAsync(originalFullPath, cancel)))
                    {
                        element2 = element;
                    }
                }
                catch { }

                if (element2._IsEmpty())
                {
                    try
                    {
                        FileSystemEntity[] dirItems = await this.EnumDirectoryImplAsync(currentFullPath, EnumDirectoryFlags.NoGetPhysicalSize, cancel);
                        element2 = dirItems.Where(x => x.IsDirectory == isThisElementDirectory && x.IsCurrentDirectory == false).Select(x => x.Name).Where(x => x._IsSamei(element)).FirstOrDefault();
                    }
                    catch
                    {
                    }
                }

                if (element2._IsEmpty())
                {
                    element2 = element;
                }

                currentFullPath = PathParser.Combine(currentFullPath, element2);
            }

            CaseCorrectionCache[cacheKey] = currentFullPath;

            return currentFullPath;
        }

        public async Task<string> NormalizePathAsync(string path, NormalizePathOption options = NormalizePathOption.None, CancellationToken cancel = default)
        {
            path = path._NonNull();

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    if (options == NormalizePathOption.NormalizeCaseDirectory)
                        path = await NormalizePathWithCaseCorrectionInternalAsync(path, true, cancel);
                    else if (options == NormalizePathOption.NormalizeCaseFileName)
                        path = await NormalizePathWithCaseCorrectionInternalAsync(path, false, cancel);

                    string ret = await NormalizePathImplAsync(path, cancel);

                    return ret;
                }
            }
        }
        public string NormalizePath(string path, NormalizePathOption options = NormalizePathOption.None, CancellationToken cancel = default)
            => NormalizePathAsync(path, options, cancel)._GetResult();

        public async Task<FileObject> CreateFileAsync(FileParameters option, CancellationToken cancel = default)
        {
            if (option.Mode == FileMode.Append || option.Mode == FileMode.Create || option.Mode == FileMode.CreateNew || option.Mode == FileMode.OpenOrCreate || option.Mode == FileMode.Truncate)
                CheckWriteable(option.Path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    await option.NormalizePathAsync(this, opCancel);

                    if (option.Mode == FileMode.Append || option.Mode == FileMode.Create || option.Mode == FileMode.CreateNew ||
                        option.Mode == FileMode.OpenOrCreate || option.Mode == FileMode.Truncate)
                    {
                        if (option.Access.Bit(FileAccess.Write) == false)
                        {
                            throw new ArgumentException("The Access member must contain the FileAccess.Write bit when opening a file with create mode.");
                        }

                        if (option.Flags.Bit(FileFlags.AutoCreateDirectory))
                        {
                            string dirName = this.PathParser.GetDirectoryName(option.Path);
                            if (dirName._IsFilled())
                            {
                                await CreateDirectoryImplAsync(dirName, option.Flags, opCancel);
                            }
                        }
                    }

                    FileObject f = await CreateFileImplAsync(option, opCancel);

                    lock (LockObj)
                    {
                        OpenedHandleList.Add(f);
                    }

                    f.EventListeners.RegisterCallback(FileEventListenerCallback);

                    return f;
                }
            }
        }

        void FileEventListenerCallback(FileBase obj, FileObjectEventType eventType, object userState)
        {
            switch (eventType)
            {
                case FileObjectEventType.Closed:
                    lock (LockObj)
                    {
                        OpenedHandleList.Remove(obj as FileObject);
                    }
                    break;
            }
        }

        public FileObject CreateFile(FileParameters option, CancellationToken cancel = default)
            => CreateFileAsync(option, cancel)._GetResult();

        public Task<FileObject> CreateAsync(string path, bool noShare = false, FileFlags flags = FileFlags.None, bool doNotOverwrite = false, CancellationToken cancel = default)
            => CreateFileAsync(new FileParameters(path, doNotOverwrite ? FileMode.CreateNew : FileMode.Create, FileAccess.ReadWrite, noShare ? FileShare.None : FileShare.Read, flags), cancel);

        public FileObject Create(string path, bool noShare = false, FileFlags flags = FileFlags.None, bool doNotOverwrite = false, CancellationToken cancel = default)
            => CreateAsync(path, noShare, flags, doNotOverwrite, cancel)._GetResult();

        public Task<FileObject> OpenAsync(string path, bool writeMode = false, bool noShare = false, bool readLock = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => CreateFileAsync(new FileParameters(path, FileMode.Open, (writeMode ? FileAccess.ReadWrite : FileAccess.Read),
                (noShare ? FileShare.None : ((writeMode || readLock) ? FileShare.Read : (FileShare.ReadWrite | FileShare.Delete))), flags), cancel);

        public FileObject Open(string path, bool writeMode = false, bool noShare = false, bool readLock = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => OpenAsync(path, writeMode, noShare, readLock, flags, cancel)._GetResult();

        public Task<FileObject> OpenOrCreateAsync(string path, bool noShare = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => CreateFileAsync(new FileParameters(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, noShare ? FileShare.None : FileShare.Read, flags), cancel);

        public FileObject OpenOrCreate(string path, bool noShare = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => OpenOrCreateAsync(path, noShare, flags, cancel)._GetResult();

        public Task<FileObject> OpenOrCreateAppendAsync(string path, bool noShare = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => CreateFileAsync(new FileParameters(path, FileMode.Append, FileAccess.Write, noShare ? FileShare.None : FileShare.Read, flags), cancel);

        public FileObject OpenOrCreateAppend(string path, bool noShare = false, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => OpenOrCreateAppendAsync(path, noShare, flags, cancel)._GetResult();

        public async Task CreateDirectoryAsync(string path, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
        {
            CheckWriteable(path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    path = await NormalizePathAsync(path, cancel: opCancel);

                    opCancel.ThrowIfCancellationRequested();

                    await CreateDirectoryImplAsync(path, flags, opCancel);
                }
            }
        }

        public void CreateDirectory(string path, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => CreateDirectoryAsync(path, flags, cancel)._GetResult();

        public async Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancel = default)
        {
            CheckWriteable(path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    path = await NormalizePathAsync(path, cancel: opCancel);

                    opCancel.ThrowIfCancellationRequested();

                    await DeleteDirectoryImplAsync(path, recursive, opCancel);
                }
            }
        }
        public void DeleteDirectory(string path, bool recursive = false, CancellationToken cancel = default)
            => DeleteDirectoryAsync(path, recursive, cancel)._GetResult();

        async Task<FileSystemEntity[]> EnumDirectoryInternalAsync(string directoryPath, EnumDirectoryFlags flags, CancellationToken opCancel)
        {
            using (EnterCriticalCounter())
            {
                opCancel.ThrowIfCancellationRequested();

                FileSystemEntity[] list = await EnumDirectoryImplAsync(directoryPath, flags, opCancel);

                if (list.Select(x => x.Name).Distinct().Count() != list.Count())
                {
                    throw new ApplicationException("There are duplicated entities returned by EnumDirectoryImplAsync().");
                }

                if (list.First().IsCurrentDirectory == false || list.First().IsDirectory == false)
                {
                    throw new ApplicationException("The first entry returned by EnumDirectoryImplAsync() is not a current directory.");
                }

                return list.Skip(1).Where(x => GetSpecialFileNameKind(x.Name) == SpecialFileNameKind.Normal).Prepend(list[0]).ToArray();
            }
        }

        async Task<bool> EnumDirectoryRecursiveInternalAsync(int depth, List<FileSystemEntity> currentList, string directoryPath, bool recursive, EnumDirectoryFlags flags, CancellationToken opCancel)
        {
            opCancel.ThrowIfCancellationRequested();

            FileSystemEntity[] entityList = await EnumDirectoryInternalAsync(directoryPath, flags, opCancel);

            foreach (FileSystemEntity entity in entityList)
            {
                if (depth == 0 || entity.IsCurrentDirectory == false)
                {
                    currentList.Add(entity);
                }

                if (recursive)
                {
                    if (entity.IsDirectory && entity.IsCurrentDirectory == false)
                    {
                        if (await EnumDirectoryRecursiveInternalAsync(depth + 1, currentList, entity.FullPath, true, flags, opCancel) == false)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public async Task<FileSystemEntity[]> EnumDirectoryAsync(string directoryPath, bool recursive = false, EnumDirectoryFlags flags = EnumDirectoryFlags.None, CancellationToken cancel = default)
        {
            CheckNotCanceled();

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                opCancel.ThrowIfCancellationRequested();

                directoryPath = await NormalizePathAsync(directoryPath, cancel: opCancel);

                List<FileSystemEntity> currentList = new List<FileSystemEntity>();

                if (await EnumDirectoryRecursiveInternalAsync(0, currentList, directoryPath, recursive, flags, opCancel) == false)
                {
                    throw new OperationCanceledException();
                }

                return currentList.ToArray();
            }
        }

        public FileSystemEntity[] EnumDirectory(string directoryPath, bool recursive = false, EnumDirectoryFlags flags = EnumDirectoryFlags.None, CancellationToken cancel = default)
            => EnumDirectoryAsync(directoryPath, recursive, flags, cancel)._GetResult();

        public async Task<FileMetadata> GetFileMetadataAsync(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default)
        {
            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    path = await NormalizePathImplAsync(path, opCancel);

                    return await GetFileMetadataImplAsync(path, flags, cancel);
                }
            }
        }
        public FileMetadata GetFileMetadata(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default)
            => GetFileMetadataAsync(path, flags, cancel)._GetResult();

        public async Task<FileMetadata> GetDirectoryMetadataAsync(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default)
        {
            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    path = await NormalizePathImplAsync(path, opCancel);

                    return await GetDirectoryMetadataImplAsync(path, flags, cancel);
                }
            }
        }
        public FileMetadata GetDirectoryMetadata(string path, FileMetadataGetFlags flags = FileMetadataGetFlags.DefaultAll, CancellationToken cancel = default)
            => GetDirectoryMetadataAsync(path, flags, cancel)._GetResult();

        public async Task<bool> IsFileExistsAsync(string path, CancellationToken cancel = default)
        {
            CheckNotCanceled();

            try
            {
                return await IsFileExistsImplAsync(path, cancel);
            }
            catch { }

            return false;
        }
        public bool IsFileExists(string path, CancellationToken cancel = default)
            => IsFileExistsAsync(path, cancel)._GetResult();

        public async Task<bool> IsDirectoryExistsAsync(string path, CancellationToken cancel = default)
        {
            CheckNotCanceled();

            try
            {
                return await IsDirectoryExistsImplAsync(path, cancel);
            }
            catch { }

            return false;
        }
        public bool IsDirectoryExists(string path, CancellationToken cancel = default)
            => IsDirectoryExistsAsync(path, cancel)._GetResult();

        public async Task SetFileMetadataAsync(string path, FileMetadata metadata, CancellationToken cancel = default)
        {
            CheckWriteable(path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    path = await NormalizePathImplAsync(path, opCancel);

                    await SetFileMetadataImplAsync(path, metadata, opCancel);
                }
            }
        }
        public void SetFileMetadata(string path, FileMetadata metadata, CancellationToken cancel = default)
            => SetFileMetadataAsync(path, metadata, cancel)._GetResult();

        public async Task SetDirectoryMetadataAsync(string path, FileMetadata metadata, CancellationToken cancel = default)
        {
            CheckWriteable(path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    path = await NormalizePathImplAsync(path, opCancel);

                    await SetDirectoryMetadataImplAsync(path, metadata, opCancel);
                }
            }
        }
        public void SetDirectoryMetadata(string path, FileMetadata metadata, CancellationToken cancel = default)
            => SetDirectoryMetadataAsync(path, metadata, cancel)._GetResult();

        public async Task DeleteFileAsync(string path, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
        {
            CheckWriteable(path);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    path = await NormalizePathImplAsync(path, opCancel);

                    await DeleteFileImplAsync(path, flags, opCancel);
                }
            }
        }

        public void DeleteFile(string path, FileFlags flags = FileFlags.None, CancellationToken cancel = default)
            => DeleteFileAsync(path, flags, cancel)._GetResult();

        public async Task MoveFileAsync(string srcPath, string destPath, CancellationToken cancel = default)
        {
            CheckWriteable(srcPath);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    srcPath = await NormalizePathImplAsync(srcPath, opCancel);
                    destPath = await NormalizePathImplAsync(destPath, opCancel);

                    await MoveFileImplAsync(srcPath, destPath, cancel);
                }
            }
        }
        public void MoveFile(string srcPath, string destPath, CancellationToken cancel = default)
            => MoveFileAsync(srcPath, destPath, cancel)._GetResult();

        public async Task MoveDirectoryAsync(string srcPath, string destPath, CancellationToken cancel = default)
        {
            CheckWriteable(srcPath);

            using (CreatePerTaskCancellationToken(out CancellationToken opCancel, cancel))
            {
                using (EnterCriticalCounter())
                {
                    cancel.ThrowIfCancellationRequested();

                    srcPath = await NormalizePathImplAsync(srcPath, opCancel);
                    destPath = await NormalizePathImplAsync(destPath, opCancel);

                    await MoveDirectoryImplAsync(srcPath, destPath, cancel);
                }
            }
        }
        public void MoveDirectory(string srcPath, string destPath, CancellationToken cancel = default)
            => MoveDirectoryAsync(srcPath, destPath, cancel)._GetResult();

        public static SpecialFileNameKind GetSpecialFileNameKind(string fileName)
        {
            SpecialFileNameKind ret = SpecialFileNameKind.Normal;

            if (fileName == ".") ret |= SpecialFileNameKind.CurrentDirectory;
            if (fileName == "..") ret |= SpecialFileNameKind.ParentDirectory;

            return ret;
        }

        public FileSystemEventWatcher CreateFileSystemEventWatcher(string root, string filter = "**/*", object state = null, bool enforcePolling = false, int? pollingInterval = null)
        {
            DisposableFileProvider p = this._CreateFileProviderForWatchInternal(EnsureInternal.Yes, root);

            try
            {
                return new FileSystemEventWatcher(p, filter, state, enforcePolling, pollingInterval);
            }
            catch
            {
                p._DisposeSafe();
                throw;
            }
        }

        public FileSystemBasedProvider CreateFileProvider(string rootDirectory)
        {
            return new FileSystemBasedProvider(EnsureInternal.Yes, this, rootDirectory);
        }
    }

    public class DisposableFileProvider : IFileProvider, IDisposable
    {
        readonly IFileProvider Provider;
        readonly bool NoDispose;
        readonly IHolder LeakHolder;

        public DisposableFileProvider(IFileProvider baseInstance, bool noDispose = false)
        {
            this.Provider = baseInstance;
            this.NoDispose = noDispose;
            this.LeakHolder = LeakChecker.Enter();
        }
        public IDirectoryContents GetDirectoryContents(string subpath) => Provider.GetDirectoryContents(subpath);
        public IFileInfo GetFileInfo(string subpath) => Provider.GetFileInfo(subpath);
        public IChangeToken Watch(string filter) => Provider.Watch(filter);
        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;

            if (NoDispose == false)
            {
                if (Provider is IDisposable target)
                {
                    target._DisposeSafe();
                }
            }

            LeakHolder._DisposeSafe();
        }
    }

    public class FileSystemEventWatcher : AsyncService
    {
        readonly DisposableFileProvider Provider;
        public string Filter { get; }
        public object State { get; }
        public bool EnforcePolling { get; }
        public bool IsPollingMode { get; private set; }
        public int PollingInterval { get; }

        IChangeToken ChangeToken = null;
        IDisposable CallbackDisposable = null;

        Task CurrentPollingTask = null;

        readonly CriticalSection LockObj = new CriticalSection();

        public FastEventListenerList<FileSystemEventWatcher, NonsenseEventType> EventListeners { get; } = new FastEventListenerList<FileSystemEventWatcher, NonsenseEventType>();

        public FileSystemEventWatcher(DisposableFileProvider provider, string filter = "**/*", object state = null, bool enforcePolling = false, int? pollingInterval = null)
        {
            this.Provider = provider;
            this.Filter = filter;
            this.State = state;
            this.EnforcePolling = enforcePolling;
            this.PollingInterval = pollingInterval ?? CoresConfig.FileSystemEventWatcherSettings.DefaultPollingInterval;

            this.PollingInterval = Math.Max(this.PollingInterval, 100);

            try
            {
                ChangeToken = this.Provider.Watch(this.Filter);

                if (EnforcePolling || ChangeToken.ActiveChangeCallbacks == false)
                {
                    IsPollingMode = true;

                    CurrentPollingTask = PollDelayAsync();
                }
                else
                {
                    IsPollingMode = false;

                    CallbackDisposable = ChangeToken.RegisterChangeCallback(Poll, null);
                }
            }
            catch
            {
                this._DisposeSafe();
                throw;
            }
        }

        void Poll(object internalState)
        {
            lock (LockObj)
            {
                if (ChangeToken.HasChanged)
                {
                    CallbackDisposable._DisposeSafe();

                    try
                    {
                        ChangeToken = this.Provider.Watch(this.Filter);

                        try
                        {
                            this.EventListeners.FireSoftly(this, NonsenseEventType.Nonsense);
                        }
                        catch { }

                        if (EnforcePolling || ChangeToken.ActiveChangeCallbacks == false)
                        {
                            IsPollingMode = true;

                            CurrentPollingTask = PollDelayAsync();
                        }
                        else
                        {
                            IsPollingMode = false;

                            CallbackDisposable = ChangeToken.RegisterChangeCallback(Poll, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        ex._Debug();
                    }
                }
                else
                {
                    if (EnforcePolling)
                    {
                        IsPollingMode = true;

                        CurrentPollingTask = PollDelayAsync();
                    }
                }
            }
        }

        async Task PollDelayAsync()
        {
            await Task.Yield();

            this.GrandCancel.ThrowIfCancellationRequested();

            await this.GrandCancel._WaitUntilCanceledAsync(this.PollingInterval);

            this.GrandCancel.ThrowIfCancellationRequested();

            Poll(null);
        }

        protected override void DisposeImpl(Exception ex)
        {
            try
            {
                lock (LockObj)
                {
                    this.CallbackDisposable._DisposeSafe();

                    CurrentPollingTask._TryWait(noDebugMessage: true);
                }

                this.Provider._DisposeSafe();
            }
            finally
            {
                base.DisposeImpl(ex);
            }
        }
    }

    [Flags]
    public enum NormalizePathOption
    {
        None = 0,
        NormalizeCaseFileName,
        NormalizeCaseDirectory,
    }
}

