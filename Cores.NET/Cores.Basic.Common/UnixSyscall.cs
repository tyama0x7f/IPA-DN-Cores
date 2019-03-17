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
using System.Runtime.InteropServices;

#pragma warning disable 0618

namespace IPA.Cores.Basic
{
    static class Unisys
    {
        static class Libraries
        {
            public const string SystemNative = "System.Native";
        }

        public enum LockOperations : int
        {
            LOCK_SH = 1,    /* shared lock */
            LOCK_EX = 2,    /* exclusive lock */
            LOCK_NB = 4,    /* don't block when locking*/
            LOCK_UN = 8,    /* unlock */
        }

        public enum OpenFlags
        {
            // Access modes (mutually exclusive)
            O_RDONLY = 0x0000,
            O_WRONLY = 0x0001,
            O_RDWR = 0x0002,

            // Flags (combinable)
            O_CLOEXEC = 0x0010,
            O_CREAT = 0x0020,
            O_EXCL = 0x0040,
            O_TRUNC = 0x0080,
            O_SYNC = 0x0100,
        }

        public enum Permissions
        {
            Mask = S_IRWXU | S_IRWXG | S_IRWXO,

            S_IRWXU = S_IRUSR | S_IWUSR | S_IXUSR,
            S_IRUSR = 0x100,
            S_IWUSR = 0x80,
            S_IXUSR = 0x40,

            S_IRWXG = S_IRGRP | S_IWGRP | S_IXGRP,
            S_IRGRP = 0x20,
            S_IWGRP = 0x10,
            S_IXGRP = 0x8,

            S_IRWXO = S_IROTH | S_IWOTH | S_IXOTH,
            S_IROTH = 0x4,
            S_IWOTH = 0x2,
            S_IXOTH = 0x1,
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_FLock", SetLastError = true)]
        public static extern int FLock(IntPtr fd, LockOperations operation);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Open", SetLastError = true)]
        public static extern IntPtr Open(string filename, OpenFlags flags, int mode);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Close", SetLastError = true)]
        public static extern int Close(IntPtr fd);

        public enum PipeFlags
        {
            O_CLOEXEC = 0x0010,
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Pipe", SetLastError = true)]
        public static extern unsafe int Pipe(int* pipefd, PipeFlags flags = 0);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_FcntlSetIsNonBlocking", SetLastError = true)]
        public static extern int SetIsNonBlocking(IntPtr fd, int isNonBlocking);

        public static unsafe void NewPipe(out IntPtr p0_read, out IntPtr p1_write)
        {
            int* fds = stackalloc int[2];

            int ret = Pipe(fds);

            if (ret < 0)
            {
                throw new SystemException("SystemNative_Pipe failed.");
            }

            p0_read = new IntPtr(fds[0]);
            p1_write = new IntPtr(fds[1]);

            SetIsNonBlocking(p0_read, 1);
            SetIsNonBlocking(p1_write, 1);
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Write", SetLastError = true)]
        public static extern unsafe int Write(IntPtr fd, byte* buffer, int bufferSize);

        public static unsafe int Write(IntPtr fd, byte[] buffer, int offset, int size)
        {
            fixed (byte* p = buffer)
            {
                byte* p2 = p + offset;
                return Write(fd, p2, size);
            }
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Read", SetLastError = true)]
        internal static extern unsafe int Read(IntPtr fd, byte* buffer, int count);

        public static unsafe int Read(IntPtr fd, byte[] buffer, int offset, int size)
        {
            fixed (byte* p = buffer)
            {
                byte* p2 = p + offset;
                return Read(fd, p2, size);
            }
        }

        public enum PollEvents : short
        {
            POLLNONE = 0x0000,  // No events occurred.
            POLLIN = 0x0001,  // non-urgent readable data available
            POLLPRI = 0x0002,  // urgent readable data available
            POLLOUT = 0x0004,  // data can be written without blocked
            POLLERR = 0x0008,  // an error occurred
            POLLHUP = 0x0010,  // the file descriptor hung up
            POLLNVAL = 0x0020,  // the requested events were invalid
        }

        public struct PollEvent
        {
            public int FileDescriptor;         // The file descriptor to poll
            public PollEvents Events;          // The events to poll for
            public PollEvents TriggeredEvents; // The events that occurred which triggered the poll
        }

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_Poll")]
        public static extern unsafe int Poll(PollEvent* pollEvents, uint eventCount, int timeout, uint* triggered);

        public static unsafe int Poll(PollEvent[] event_list, int timeout)
        {
            fixed (PollEvent* pe = event_list)
            {
                uint* triggered = stackalloc uint[1];

                int ret = Poll(pe, (uint)event_list.Length, timeout, triggered);

                return ret;
            }
        }

        public static unsafe void Poll(IntPtr[] reads, IntPtr[] writes, int timeout)
        {
            if (timeout == 0) return;

            PollEvent[] p;
            int num, n, num_read_total, num_write_total;

            num_read_total = num_write_total = 0;
            foreach (IntPtr fd in reads) if ((int)fd != -1) num_read_total++;
            foreach (IntPtr fd in writes) if ((int)fd != -1) num_write_total++;

            num = num_read_total + num_write_total;

            p = new PollEvent[num];

            n = 0;

            foreach (IntPtr fd in reads)
            {
                if ((int)fd != -1)
                {
                    p[n].FileDescriptor = (int)fd;
                    p[n].Events = PollEvents.POLLIN | PollEvents.POLLPRI | PollEvents.POLLERR | PollEvents.POLLHUP;
                    n++;
                }
            }

            foreach (IntPtr fd in writes)
            {
                if ((int)fd != -1)
                {
                    p[n].FileDescriptor = (int)fd;
                    p[n].Events = PollEvents.POLLIN | PollEvents.POLLPRI | PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLOUT;
                    n++;
                }
            }

            if (num == 0)
            {
                ThreadObj.Sleep(timeout);
            }
            else
            {
                //Dbg.WriteLine("Poll Begin.");

                int ret = Poll(p, timeout);

                //Dbg.WriteLine($"Poll end: ret = {ret}, reads = {reads.Length}, writes = {writes.Length}, pfds = {p.Length}");

                //for (int i = 0; i < reads.Length; i++) Dbg.WriteLine($"reads[{i}] = {reads[i]}");
                //for (int i = 0; i < writes.Length; i++) Dbg.WriteLine($"writes[{i}] = {writes[i]}");
            }
        }
    }
}
