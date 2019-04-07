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
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Diagnostics;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.GlobalFunctions.Basic;

#pragma warning disable CS0162

namespace IPA.Cores.Basic
{
    class LargeFileObject : FileObjectBase
    {
        public class Cursor
        {
            public long LogicalPosition { get; }
            public int PhysicalFileNumber { get; }
            public long PhysicalPosition { get; }
            public long PhysicalRemainingLength { get; }
            public long PhysicalDataLength { get; }

            readonly LargeFileObject Lfo;

            public Cursor(LargeFileObject o, long logicalPosision, long dataLength = 0)
            {
                checked
                {
                    Lfo = o;

                    if (logicalPosision < 0)
                        throw new ArgumentOutOfRangeException("logicalPosision < 0");

                    if (dataLength < 0)
                        throw new ArgumentOutOfRangeException("dataLength < 0");

                    var p = Lfo.LargeFileSystem.Params;
                    if (logicalPosision > p.MaxLogicalFileSize)
                        throw new ArgumentOutOfRangeException("logicalPosision > MaxLogicalFileSize");

                    this.LogicalPosition = logicalPosision;
                    this.PhysicalFileNumber = (int)(this.LogicalPosition / p.MaxSinglePhysicalFileSize);

                    if (this.PhysicalFileNumber > p.MaxFileNumber)
                        throw new ArgumentOutOfRangeException($"this.PhysicalFileNumber ({this.PhysicalFileNumber})> p.MaxFileNumber ({p.MaxFileNumber})");

                    this.PhysicalPosition = this.LogicalPosition % p.MaxSinglePhysicalFileSize;
                    this.PhysicalRemainingLength = p.MaxSinglePhysicalFileSize - this.PhysicalPosition;
                    this.PhysicalDataLength = Math.Min(this.PhysicalRemainingLength, dataLength);
                }
            }

            public LargeFileSystem.ParsedPath GetParsedPath()
            {
                return new LargeFileSystem.ParsedPath(Lfo.LargeFileSystem, Lfo.FileParams.Path, this.PhysicalFileNumber);
            }
        }

        readonly LargeFileSystem LargeFileSystem;
        readonly FileSystemBase UnderlayFileSystem;
        readonly LargeFileSystem.ParsedPath[] InitialRelatedFiles;

        long CurrentFileSize;
        long CurrentPosition;

        protected LargeFileObject(FileSystemBase fileSystem, FileParameters fileParams, LargeFileSystem.ParsedPath[] relatedFiles) : base(fileSystem, fileParams)
        {
            this.LargeFileSystem = (LargeFileSystem)fileSystem;
            this.UnderlayFileSystem = this.LargeFileSystem.UnderlayFileSystem;
            this.InitialRelatedFiles = relatedFiles;
        }

        public static async Task<FileObjectBase> CreateFileAsync(LargeFileSystem fileSystem, FileParameters fileParams, LargeFileSystem.ParsedPath[] relatedFiles, CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            LargeFileObject f = new LargeFileObject(fileSystem, fileParams, relatedFiles);

            await f.CreateAsync(cancel);

            return f;
        }

        protected override async Task CreateAsync(CancellationToken cancel = default)
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                var lastFileParsed = InitialRelatedFiles.OrderBy(x => x.FileNumber).LastOrDefault();

                if (lastFileParsed == null)
                {
                    // New file
                    CurrentFileSize = 0;

                    if (FileParams.Mode == FileMode.Open || FileParams.Mode == FileMode.Truncate)
                    {
                        throw new IOException($"The file '{FileParams.Path}' not found.");
                    }
                }
                else
                {
                    // File exists
                    if (FileParams.Mode == FileMode.CreateNew)
                    {
                        throw new IOException($"The file '{FileParams.Path}' already exists.");
                    }

                    checked
                    {
                        long sizeOfLastFile = (await UnderlayFileSystem.GetFileMetadataAsync(lastFileParsed.PhysicalFilePath, cancel)).Size;
                        sizeOfLastFile = Math.Min(sizeOfLastFile, LargeFileSystem.Params.MaxSinglePhysicalFileSize);
                        CurrentFileSize = lastFileParsed.FileNumber * LargeFileSystem.Params.MaxSinglePhysicalFileSize + sizeOfLastFile;
                    }
                }

                if (FileParams.Mode == FileMode.Create || FileParams.Mode == FileMode.CreateNew || FileParams.Mode == FileMode.Truncate)
                {
                    if (lastFileParsed != null)
                    {
                        // Delete the files first
                        await LargeFileSystem.DeleteFileAsync(FileParams.Path, cancel);

                        lastFileParsed = null;
                        CurrentFileSize = 0;
                    }
                }

                // Try to open or create the physical file which contains the tail
                using (await GetUnderleyRandomAccessHandle(this.CurrentFileSize, cancel))
                {
                }

                this.CurrentPosition = 0;
                if (FileParams.Mode == FileMode.Append)
                {
                    this.CurrentPosition = this.CurrentFileSize;
                }

                await base.CreateAsync(cancel);
            }
            catch
            {
                throw;
            }
        }

        readonly Cache<string, LargeFileSystem.ParsedPath> ParsedPathCache = new Cache<string, LargeFileSystem.ParsedPath>(TimeSpan.FromMilliseconds(1000), CacheType.UpdateExpiresWhenAccess);

        async Task<RandomAccessHandle> GetUnderleyRandomAccessHandle(long logicalPosition, CancellationToken cancel)
        {
            var cursor = new Cursor(this, logicalPosition);

            string cacheKey = $"{this.FileParams.Path}:{cursor.PhysicalFileNumber}";

            var parsed = ParsedPathCache.GetOrCreate(cacheKey, x => new LargeFileSystem.ParsedPath(LargeFileSystem, this.FileParams.Path, cursor.PhysicalFileNumber));

            return await LargeFileSystem.UnderlayFileSystem.GetRandomAccessHandleAsync(parsed.PhysicalFilePath, FileParams.Access.Bit(FileAccess.Write), this.FileParams.OperationFlags, cancel);
        }

        protected override async Task CloseImplAsync()
        {
            if (FileParams.Access.Bit(FileAccess.Write))
            {
                await LargeFileSystem.UnderlayFileSystemPoolForWrite.EnumAndCloseHandlesAsync((key, file) =>
                {
                    var parsed = new LargeFileSystem.ParsedPath(LargeFileSystem, key);
                    if (parsed.LogicalFilePath.IsSame(this.FileParams.Path, LargeFileSystem.PathInterpreter.PathStringComparison))
                    {
                        return true;
                    }
                    return false;
                });
            }
        }

        protected override async Task FlushImplAsync(CancellationToken cancel = default)
        {
            using (var handle = await GetUnderleyRandomAccessHandle(this.CurrentFileSize, cancel))
            {
                await handle.FlushAsync(cancel);
            }
        }

        protected override async Task<long> GetCurrentPositionImplAsync(CancellationToken cancel = default)
        {
            await Task.CompletedTask;

            return this.CurrentPosition;
        }

        protected override async Task<long> GetFileSizeImplAsync(CancellationToken cancel = default)
        {
            await Task.CompletedTask;

            return this.CurrentFileSize;
        }

        List<Cursor> GenerateCursorList(long position, long length, bool writeMode)
        {
            checked
            {
                if (position < 0) throw new ArgumentOutOfRangeException("position");
                if (length < 0) throw new ArgumentOutOfRangeException("length");

                if (writeMode == false)
                {
                    if (position > this.CurrentFileSize)
                        throw new ApplicationException("position > this.CurrentFileSize");

                    if (position + length > this.CurrentFileSize)
                    {
                        length = this.CurrentFileSize - position;
                    }
                }

                if (length == 0)
                {
                    return new List<Cursor>();
                }

                List<Cursor> ret = new List<Cursor>();

                long eof = position + length;

                while (position < eof)
                {
                    Cursor cursor = new Cursor(this, position, eof - position);
                    ret.Add(cursor);

                    position += cursor.PhysicalDataLength;
                }

                return ret;
            }
        }

        protected override async Task<int> ReadImplAsync(long position, Memory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                if (data.Length == 0) return 0;

                int totalLength = 0;

                List<Cursor> cursorList = GenerateCursorList(position, data.Length, false);

                foreach (Cursor cursor in cursorList)
                {
                    bool isLast = (cursor == cursorList.Last());

                    RandomAccessHandle handle = await TryIfErrorRetDefaultAsync(async () => await GetUnderleyRandomAccessHandle(cursor.LogicalPosition, cancel), noDebugMessage: true);

                    var subMemory = data.Slice((int)(cursor.LogicalPosition - position), (int)cursor.PhysicalDataLength);

                    if (handle != null)
                    {
                        using (handle)
                        {
                            int r = await handle.ReadRandomAsync(cursor.PhysicalPosition, subMemory, cancel);

                            Debug.Assert(r <= (int)cursor.PhysicalDataLength);

                            if (isLast && r == 0)
                            {
                                throw new ApplicationException($"Unable to read {cursor.PhysicalDataLength} bytes from offset {cursor.PhysicalPosition} of the physical file '{cursor.GetParsedPath().PhysicalFilePath}'.");
                            }

                            if (r < (int)cursor.PhysicalDataLength)
                            {
                                var zeroClearMemory = subMemory.Slice(r);
                                zeroClearMemory.Span.Fill(0);
                            }

                            totalLength += (int)cursor.PhysicalDataLength;
                        }
                    }
                    else
                    {
                        if (isLast)
                        {
                            throw new ApplicationException($"Unable to read {cursor.PhysicalDataLength} bytes from offset {cursor.PhysicalPosition} of the physical file '{cursor.GetParsedPath().PhysicalFilePath}'.");
                        }

                        subMemory.Span.Fill(0);

                        totalLength += (int)cursor.PhysicalDataLength;
                    }
                }

                return totalLength;
            }
        }

        protected override async Task WriteImplAsync(long position, ReadOnlyMemory<byte> data, CancellationToken cancel = default)
        {
            checked
            {
                if (data.Length == 0) return;

                List<Cursor> cursorList = GenerateCursorList(position, data.Length, true);

                foreach (Cursor cursor in cursorList)
                {
                    using (var handle = await GetUnderleyRandomAccessHandle(cursor.LogicalPosition, cancel))
                    {
                        await handle.WriteRandomAsync(cursor.PhysicalPosition, data.Slice((int)(cursor.LogicalPosition - position), (int)cursor.PhysicalDataLength), cancel);
                    }
                }

                this.CurrentFileSize = Math.Max(this.CurrentFileSize, position + data.Length);
            }
        }

        protected override async Task SetFileSizeImplAsync(long size, CancellationToken cancel = default)
        {
            List<Cursor> cursorList = GenerateCursorList(size, 1, true);

            Cursor cursor = cursorList.Single();

            bool shrink = (this.CurrentFileSize > size);

            if (shrink)
            {
                // Delete oversized files
                LargeFileSystem.ParsedPath[] physicalFiles = await LargeFileSystem.GetPhysicalFileStateInternal(this.FileParams.Path, cancel);
                List<LargeFileSystem.ParsedPath> filesToDelete = physicalFiles.Where(x => x.FileNumber > cursor.PhysicalFileNumber).ToList();

                FileSystemObjectPool pool = LargeFileSystem.UnderlayFileSystemPoolForWrite;

                await pool.EnumAndCloseHandlesAsync((key, file) =>
                {
                    if (filesToDelete.Where(x => x.PhysicalFilePath.IsSame(file.FileParams.Path, LargeFileSystem.PathInterpreter.PathStringComparison)).Any())
                    {
                        return true;
                    }
                    return false;
                },
                () =>
                {
                    foreach (LargeFileSystem.ParsedPath deleteFile in filesToDelete.OrderByDescending(x => x.PhysicalFilePath))
                    {
                        UnderlayFileSystem.DeleteFile(deleteFile.PhysicalFilePath, cancel);
                    }
                },
                (x, y) =>
                {
                    return -(x.FileParams.Path.CompareTo(y.FileParams.Path));
                },
                cancel);
            }

            using (var handle = await GetUnderleyRandomAccessHandle(cursor.LogicalPosition, cancel))
            {
                await handle.SetFileSizeAsync(cursor.PhysicalPosition, cancel);

                this.CurrentFileSize = size;
            }

        }
    }

    class LargeFileSystemParams
    {
        public const long DefaultMaxSinglePhysicalFileSize = 1000000;
        public const long DefaultMaxLogicalFileSize = 100000000000000; // 100TB
        public const int DefaultPooledFileCloseDelay = 1000;

        public long MaxSinglePhysicalFileSize { get; }
        public long MaxLogicalFileSize { get; }
        public int NumDigits { get; }
        public string SplitStr { get; }
        public int PooledFileCloseDelay { get; }
        public int MaxFileNumber { get; }

        public LargeFileSystemParams(long maxSingleFileSize = DefaultMaxSinglePhysicalFileSize, long logicalMaxSize = DefaultMaxLogicalFileSize, string splitStr = "~",
            int pooledFileCloseDelay = DefaultPooledFileCloseDelay)
        {
            checked
            {
                this.SplitStr = splitStr.NonNullTrim().Default("~");
                this.MaxSinglePhysicalFileSize = Math.Max(maxSingleFileSize, 1);
                this.MaxLogicalFileSize = logicalMaxSize;
                this.PooledFileCloseDelay = Math.Max(pooledFileCloseDelay, 1000);

                long i = (int)(MaxLogicalFileSize / this.MaxSinglePhysicalFileSize);
                i = Math.Max(i, 1);
                i = (int)Math.Log10(i);
                i++;
                i = Math.Max(Math.Min(i, 9), 1);
                NumDigits = (int)i;

                this.MaxFileNumber = Str.MakeCharArray('9', NumDigits).ToInt();
                this.MaxLogicalFileSize = MaxSinglePhysicalFileSize * this.MaxFileNumber;
            }
        }
    }

    class LargeFileSystem : FileSystemBase
    {
        public class ParsedPath
        {
            public string DirectoryPath { get; }
            public string OriginalFileNameWithoutExtension { get; }
            public int FileNumber { get; }
            public string Extension { get; }
            public string PhysicalFilePath { get; }
            public string LogicalFilePath { get; }
            public FileSystemEntity PhysicalEntity { get; }

            readonly LargeFileSystem LargeFileSystem;

            public ParsedPath(LargeFileSystem fs, string logicalFilePath, int fileNumber)
            {
                this.LargeFileSystem = fs;

                this.LogicalFilePath = logicalFilePath;

                string fileName = fs.PathInterpreter.GetFileName(logicalFilePath);
                if (fileName.IndexOf(fs.Params.SplitStr) != -1)
                    throw new ApplicationException($"The original filename '{fileName}' contains '{fs.Params.SplitStr}'.");

                string dir = fs.PathInterpreter.GetDirectoryName(logicalFilePath);
                string filename = fs.PathInterpreter.GetFileName(logicalFilePath);
                string extension;
                int dotIndex = fileName.IndexOf('.');
                string filenameWithoutExtension;
                if (dotIndex != -1)
                {
                    extension = fileName.Substring(dotIndex);
                    filenameWithoutExtension = fileName.Substring(0, dotIndex);
                }
                else
                {
                    extension = "";
                    filenameWithoutExtension = fileName;
                }

                this.DirectoryPath = dir;
                this.OriginalFileNameWithoutExtension = filenameWithoutExtension;
                this.FileNumber = fileNumber;
                this.Extension = extension;
                this.PhysicalFilePath = GeneratePhysicalPath();
            }

            public ParsedPath(LargeFileSystem fs, string physicalFilePath, FileSystemEntity physicalEntity = null)
            {
                this.LargeFileSystem = fs;

                this.PhysicalEntity = physicalEntity;
                this.PhysicalFilePath = physicalFilePath;

                string dir = fs.PathInterpreter.GetDirectoryName(physicalFilePath);
                string fn = fs.PathInterpreter.GetFileName(physicalFilePath);

                int[] indexes = fn.FindStringIndexes(fs.Params.SplitStr);
                if (indexes.Length != 1)
                    throw new ArgumentException($"Filename '{fn}' is not a large file. indexes.Length != 1.");

                string originalFileName = fn.Substring(0, indexes[0]);
                string afterOriginalFileName = fn.Substring(indexes[0] + 1);
                if (originalFileName.IsEmpty() || afterOriginalFileName.IsEmpty())
                    throw new ArgumentException($"Filename '{fn}' is not a large file.");

                string extension;
                int dotIndex = afterOriginalFileName.IndexOf('.');
                string digitsStr;
                if (dotIndex != -1)
                {
                    extension = afterOriginalFileName.Substring(dotIndex);
                    digitsStr = afterOriginalFileName.Substring(0, dotIndex);
                }
                else
                {
                    extension = "";
                    digitsStr = afterOriginalFileName;
                }

                if (digitsStr.IsNumber() == false)
                    throw new ArgumentException($"Filename '{fn}' is not a large file. digitsStr.IsNumber() == false.");

                if (digitsStr.Length != fs.Params.NumDigits)
                    throw new ArgumentException($"Filename '{fn}' is not a large file. digitsStr.Length != fs.Params.NumDigits.");

                this.DirectoryPath = dir;
                this.OriginalFileNameWithoutExtension = originalFileName;
                this.FileNumber = digitsStr.ToInt();
                this.Extension = extension;

                string filename = $"{OriginalFileNameWithoutExtension}{Extension}";
                this.LogicalFilePath = fs.PathInterpreter.Combine(this.DirectoryPath, filename);
            }

            public string GeneratePhysicalPath(int? fileNumberOverwrite = null)
            {
                int fileNumber = fileNumberOverwrite ?? FileNumber;
                string fileNumberStr = fileNumber.ToString($"D{LargeFileSystem.Params.NumDigits}");
                Debug.Assert(fileNumberStr.Length == LargeFileSystem.Params.NumDigits);

                string filename = $"{OriginalFileNameWithoutExtension}{LargeFileSystem.Params.SplitStr}{fileNumberStr}{Extension}";

                return LargeFileSystem.PathInterpreter.Combine(DirectoryPath, filename);
            }
        }

        CancellationTokenSource CancelSource = new CancellationTokenSource();
        CancellationToken CancelToken => CancelSource.Token;

        public FileSystemBase UnderlayFileSystem { get; }
        public LargeFileSystemParams Params { get; }

        AsyncLock AsyncLockObj = new AsyncLock();

        public FileSystemObjectPool UnderlayFileSystemPoolForRead { get; }
        public FileSystemObjectPool UnderlayFileSystemPoolForWrite { get; }

        public LargeFileSystem(AsyncCleanuperLady lady, FileSystemBase underlayFileSystem, LargeFileSystemParams param) : base(lady, underlayFileSystem.PathInterpreter)
        {
            this.UnderlayFileSystem = underlayFileSystem;
            this.Params = param;

            this.UnderlayFileSystemPoolForRead = this.UnderlayFileSystem.ObjectPoolForRead;
            this.UnderlayFileSystemPoolForWrite = this.UnderlayFileSystem.ObjectPoolForWrite;
        }

        protected override Task<string> NormalizePathImplAsync(string path, CancellationToken cancel = default)
            => this.UnderlayFileSystem.NormalizePathAsync(path, cancel);

        protected override async Task<FileObjectBase> CreateFileImplAsync(FileParameters option, CancellationToken cancel = default)
        {
            string fileName = this.PathInterpreter.GetFileName(option.Path);
            if (fileName.IndexOf(Params.SplitStr) != -1)
                throw new ApplicationException($"The original filename '{fileName}' contains '{Params.SplitStr}'.");

            using (TaskUtil.CreateCombinedCancellationToken(out CancellationToken operationCancel, this.CancelToken, cancel))
            {
                using (await AsyncLockObj.LockWithAwait(operationCancel))
                {
                    cancel.ThrowIfCancellationRequested();

                    ParsedPath[] relatedFiles = await GetPhysicalFileStateInternal(option.Path, operationCancel);

                    return await LargeFileObject.CreateFileAsync(this, option, relatedFiles, operationCancel);
                }
            }
        }

        public async Task<ParsedPath[]> GetPhysicalFileStateInternal(string logicalFilePath, CancellationToken cancel)
        {
            List<ParsedPath> ret = new List<ParsedPath>();

            ParsedPath parsed = new ParsedPath(this, logicalFilePath, 0);

            FileSystemEntity[] dirEntities = await UnderlayFileSystem.EnumDirectoryAsync(parsed.DirectoryPath, false, cancel);

            var relatedFiles = dirEntities.Where(x => x.IsDirectory == false);
            foreach (var f in relatedFiles)
            {
                if (f.Name.StartsWith(parsed.OriginalFileNameWithoutExtension, PathInterpreter.PathStringComparison))
                {
                    try
                    {
                        ParsedPath parsedForFile = new ParsedPath(this, f.FullPath, f);
                        if (parsed.LogicalFilePath.IsSame(parsedForFile.LogicalFilePath, PathInterpreter.PathStringComparison))
                        {
                            ret.Add(parsedForFile);
                        }
                    }
                    catch { }
                }
            }

            ret.Sort((x, y) => x.FileNumber.CompareTo(y.FileNumber));

            return ret.ToArray();
        }

        protected override async Task<FileSystemEntity[]> EnumDirectoryImplAsync(string directoryPath, CancellationToken cancel = default)
        {
            checked
            {
                FileSystemEntity[] dirEntities = await UnderlayFileSystem.EnumDirectoryAsync(directoryPath, false, cancel);

                var relatedFiles = dirEntities.Where(x => x.IsDirectory == false).Where(x => x.Name.IndexOf(Params.SplitStr) != -1);

                var sortedRelatedFiles = relatedFiles.ToList();
                sortedRelatedFiles.Sort((x, y) => x.Name.Cmp(y.Name, PathInterpreter.PathStringComparison));
                sortedRelatedFiles.Reverse();

                Dictionary<string, FileSystemEntity> parsedFileDictionaly = new Dictionary<string, FileSystemEntity>(PathInterpreter.PathStringComparer);

                foreach (FileSystemEntity f in sortedRelatedFiles)
                {
                    try
                    {
                        ParsedPath parsed = new ParsedPath(this, f.FullPath, f);

                        if (parsedFileDictionaly.ContainsKey(parsed.LogicalFilePath) == false)
                        {
                            var newFileEntity = new FileSystemEntity()
                            {
                                FullPath = parsed.LogicalFilePath,
                                Name = PathInterpreter.GetFileName(parsed.LogicalFilePath),
                                Size = f.Size + parsed.FileNumber * Params.MaxSinglePhysicalFileSize,
                                Attributes = f.Attributes,
                                Updated = f.Updated,
                                Created = f.Created,
                            };
                            parsedFileDictionaly.Add(parsed.LogicalFilePath, newFileEntity);
                        }
                        else
                        {
                            var fileEntity = parsedFileDictionaly[parsed.LogicalFilePath];

                            if (fileEntity.Updated < f.Updated) fileEntity.Updated = f.Updated;

                            if (fileEntity.Created > f.Created) fileEntity.Created = f.Created;
                        }
                    }
                    catch { }
                }

                var retList = dirEntities.Where(x => x.IsDirectory).OrderBy(x => x.Name, PathInterpreter.PathStringComparer)
                    .Concat(parsedFileDictionaly.Values.OrderBy(x => x.Name, PathInterpreter.PathStringComparer));

                return retList.ToArrayList();
            }
        }

        protected override Task CreateDirectoryImplAsync(string directoryPath, FileOperationFlags flags = FileOperationFlags.None, CancellationToken cancel = default)
            => UnderlayFileSystem.CreateDirectoryAsync(directoryPath, flags, cancel);


        public bool TryParseOriginalPath(string physicalPath, out ParsedPath parsed)
        {
            try
            {
                parsed = new ParsedPath(this, physicalPath);
                return true;
            }
            catch
            {
                parsed = null;
                return false;
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                CancelSource.TryCancelNoBlock();
                this.UnderlayFileSystemPoolForRead.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }

        public override async Task _CleanupAsyncInternal()
        {
            try
            {
                // Here
            }
            finally { await base._CleanupAsyncInternal(); }
        }

        protected override async Task<FileSystemMetadata> GetFileMetadataImplAsync(string path, CancellationToken cancel = default)
        {
            LargeFileSystem.ParsedPath[] physicalFiles = await GetPhysicalFileStateInternal(path, cancel);
            var lastFileParsed = physicalFiles.OrderBy(x => x.FileNumber).LastOrDefault();

            if (lastFileParsed == null)
            {
                // File not found
                throw new IOException($"The file 'path' not found.");
            }
            else
            {
                // File exists
                checked
                {
                    FileSystemMetadata ret = await UnderlayFileSystem.GetFileMetadataAsync(lastFileParsed.PhysicalFilePath, cancel);
                    long sizeOfLastFile = ret.Size;
                    sizeOfLastFile = Math.Min(sizeOfLastFile, Params.MaxSinglePhysicalFileSize);

                    long currentFileSize = lastFileParsed.FileNumber * Params.MaxSinglePhysicalFileSize + sizeOfLastFile;

                    ret.Size = currentFileSize;
                    ret.Updated = physicalFiles.Max(x => x.PhysicalEntity.Updated);
                    ret.Created = physicalFiles.Min(x => x.PhysicalEntity.Created);

                    return ret;
                }
            }
        }

        protected override async Task DeleteFileImplAsync(string path, CancellationToken cancel = default)
        {
            LargeFileSystem.ParsedPath[] physicalFiles = await GetPhysicalFileStateInternal(path, cancel);
            List<LargeFileSystem.ParsedPath> filesToDelete = physicalFiles.ToList();
            FileSystemObjectPool pool = UnderlayFileSystemPoolForWrite;

            await pool.EnumAndCloseHandlesAsync((key, file) =>
            {
                if (filesToDelete.Where(x => x.PhysicalFilePath.IsSame(file.FileParams.Path, PathInterpreter.PathStringComparison)).Any())
                {
                    return true;
                }
                return false;
            },
            () =>
            {
                foreach (LargeFileSystem.ParsedPath deleteFile in filesToDelete.OrderByDescending(x => x.PhysicalFilePath))
                {
                    UnderlayFileSystem.DeleteFile(deleteFile.PhysicalFilePath, cancel);
                }
            },
            (x, y) =>
            {
                return -(x.FileParams.Path.CompareTo(y.FileParams.Path));
            },
            cancel);
        }
    }
}
