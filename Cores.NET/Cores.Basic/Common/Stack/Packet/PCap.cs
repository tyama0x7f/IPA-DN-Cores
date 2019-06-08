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
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using System.Text;

namespace IPA.Cores.Basic
{
    static partial class CoresConfig
    {
        public static partial class PCapSettings
        {
            public static readonly Copenhagen<int> DefaultBufferSize = 4 * 1024 * 1024;
        }
    }

    [Flags]
    enum PCapBlockType : uint
    {
        SectionHeader = 0x0A0D0D0A,
        InterfaceDescription = 0x00000001,
        EnhancedPacket = 0x00000006,
    }

    [Flags]
    enum PCapLinkType : ushort
    {
        Loopback = 0,
        Ethernet = 1,
    }

    [Flags]
    enum PCapOptionCode : ushort
    {
        EndOfOption = 0,
        Comment = 1,
        CustomUtf8 = 2988,
        CustomBinary = 2989,
        CustomUtf8NonCopy = 19372,
        CustomBinaryNonCopy = 19373,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct PCapOptionHeader
    {
        public PCapOptionCode OptionCode;
        public ushort OptionLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct PCapGenericBlock
    {
        public PCapBlockType BlockType;
        public int BlockTotalLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct PCapSectionHeaderBlock
    {
        public PCapBlockType BlockType;
        public int BlockTotalLength;
        public uint ByteOrderMagic;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ulong SectionLength;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct PCapInterfaceDescriptionBlock
    {
        public PCapBlockType BlockType;
        public int BlockTotalLength;
        public PCapLinkType LinkType;
        public ushort Reserved;
        public uint SnapLen;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct PCapEnhancedPacketBlock
    {
        public PCapBlockType BlockType;
        public int BlockTotalLength;
        public int InterfaceId;
        public uint TimeStampHigh;
        public uint TimeStampLow;
        public int CapturePacketLength;
        public int OriginalPacketLength;
    }

    static partial class PacketSizeSets
    {
        public static readonly PacketSizeSet PcapNgPacket = new PacketSizeSet(Unsafe.SizeOf<PCapEnhancedPacketBlock>(), 4 + 6 /* size + padding * 2 */ );
    }

    class PCapFileEmitterOptions : LazyBufferFileEmitterOptions
    {
        public PCapFileEmitterOptions(FilePath filePath, bool appendMode = true, int delay = 0, int defragmentWriteBlockSize = 0)
            : base(filePath, appendMode, delay, defragmentWriteBlockSize, PCapUtil.StandardPCapNgHeader)
        {
        }
    }

    class PCapFileEmitter : LazyBufferFileEmitter
    {
        public PCapFileEmitter(PCapFileEmitterOptions options) : base(options)
        {
        }
    }

    class PCapBuffer : LazyBuffer
    {
        public PCapBuffer(LazyBufferOptions options = null, CancellationToken cancel = default) : base(options, cancel)
        {
        }

        public PCapBuffer(PCapFileEmitter emitter, int bufferSize = DefaultSize, FastStreamNonStopWriteMode discardMode = FastStreamNonStopWriteMode.DiscardExistingData, CancellationToken cancel = default)
            : base(emitter, new LazyBufferOptions(discardMode, bufferSize._DefaultSize(CoresConfig.PCapSettings.DefaultBufferSize)), cancel)
        {
        }

        public void WritePacket(ReadOnlySpan<byte> srcPacketData, long timeStampUsecs, string comment = null)
        {
            PacketSizeSet sizeSet = PacketSizeSets.PcapNgPacket;
            if (comment != null && comment.Length >= 1)
            {
                comment._TruncStr(10000);
                sizeSet += new PacketSizeSet(0, 8 + comment.Length * 3);
            }

            Packet pkt = new Packet(sizeSet, EnsureCopy.Yes, srcPacketData);

            WritePacket(ref pkt, timeStampUsecs, comment);
        }

        public void WritePacket(ref Packet pktDiscardable, long timeStampUsecs, string comment = null)
        {
            ref Packet pkt = ref pktDiscardable;

            pkt._PCapEncapsulateEnhancedPacketBlock(0, timeStampUsecs, comment);

            base.Write(pkt.Span._CloneMemory());
        }
    }

    class PCapPacketRecorder
    {
    }

    static class PCapUtil
    {
        public const int ByteOrderMagic = 0x1A2B3C4D;

        public static readonly ReadOnlyMemory<byte> StandardPCapNgHeader = GenerateStandardPCapNgHeader().Span.ToArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ref T _PCapEncapsulateHeader<T>(this ref Packet pkt, out PacketSpan<T> retSpan, PCapBlockType blockType, ReadOnlySpan<byte> options = default) where T : unmanaged
        {
            int currentSize = pkt.Span.Length;
            int mod32 = currentSize % 4;

            if (mod32 != 0)
            {
                // Padding for data
                pkt.AppendSpan(4 - mod32);
            }

            int optionsSize = options.Length;
            if (optionsSize >= 1)
            {
                // Append the options
                pkt.AppendSpanWithData(options);

                mod32 = optionsSize % 4;
                if (mod32 != 0)
                {
                    // Padding for options
                    pkt.AppendSpan(4 - mod32);
                }
            }

            // Padding for Block Total Length
            int blockTotalLength = (sizeof(T) + pkt.Length + 4);

            // Append the footer
            pkt.AppendSpanWithData<int>(in blockTotalLength);

            // Prepend the header
            ref T ret = ref pkt.PrependSpan<T>(out retSpan);

            ref PCapGenericBlock generic = ref Unsafe.As<T, PCapGenericBlock>(ref ret);

            generic.BlockType = blockType;
            generic.BlockTotalLength = blockTotalLength;

            return ref ret;
        }

        public unsafe static PacketSpan<PCapEnhancedPacketBlock> _PCapEncapsulateEnhancedPacketBlock(this ref Packet pkt, int interfaceId, long timeStampUsecs, string comment)
        {
            int packetDataSize = pkt.Length;

            Span<byte> option = stackalloc byte[32767 + sizeof(PCapOptionHeader)];

            if (comment != null && comment.Length >= 1)
            {
                Encoder enc = Str.Utf8Encoding.GetEncoder();

                int count = enc.GetBytes(comment, option.Slice(sizeof(PCapOptionHeader)), true);

                if (count <= 32767)
                {
                    ref PCapOptionHeader optHeader = ref option[0]._AsStruct<PCapOptionHeader>();
                    optHeader.OptionCode = PCapOptionCode.Comment;
                    optHeader.OptionLength = (ushort)count;

                    option = option.Slice(0, sizeof(PCapOptionHeader) + count);
                }
                else
                {
                    option = default;
                }
            }
            else
            {
                option = default;
            }

            fixed (byte* optionPtr = option)
            {
                return _PCapEncapsulateEnhancedPacketBlock(ref pkt, interfaceId, timeStampUsecs, new ReadOnlySpan<byte>(optionPtr, option.Length));
            }
        }

        public static unsafe PacketSpan<PCapEnhancedPacketBlock> _PCapEncapsulateEnhancedPacketBlock(this ref Packet pkt, int interfaceId, long timeStampUsecs, ReadOnlySpan<byte> options = default)
        {
            int packetDataSize = pkt.Length;

            ref PCapEnhancedPacketBlock header = ref pkt._PCapEncapsulateHeader<PCapEnhancedPacketBlock>(out PacketSpan<PCapEnhancedPacketBlock> retSpan, PCapBlockType.EnhancedPacket, options);

            header.InterfaceId = interfaceId;

            if (timeStampUsecs <= 0)
            {
                timeStampUsecs = PCapUtil.FastNow_TimeStampUsec;
            }

            if (BitConverter.IsLittleEndian)
            {
                header.TimeStampHigh = ((uint*)&timeStampUsecs)[1];
                header.TimeStampLow = ((uint*)&timeStampUsecs)[0];
            }
            else
            {
                header.TimeStampHigh = ((uint*)&timeStampUsecs)[0];
                header.TimeStampLow = ((uint*)&timeStampUsecs)[1];
            }

            header.CapturePacketLength = packetDataSize;
            header.OriginalPacketLength = packetDataSize;

            return retSpan;
        }

        public static long FastNow_TimeStampUsec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ConvertSystemTimeToTimeStampUsec(FastTick64.SystemTimeNow_Fast);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ConvertSystemTimeToTimeStampUsec(long systemTime)
        {
            return (systemTime + (9L * 3600 * 1000)) * 1000L;
        }

        public static SpanBuffer<byte> GenerateStandardPCapNgHeader()
        {
            Packet sectionHeaderPacket = new Packet();
            ref var section = ref sectionHeaderPacket._PCapEncapsulateHeader<PCapSectionHeaderBlock>(out _, PCapBlockType.SectionHeader);

            section.ByteOrderMagic = ByteOrderMagic;
            section.MajorVersion = 1;
            section.MinorVersion = 0;
            section.SectionLength = 0xffffffffffffffff;

            Packet interfaceDescriptionPacket = new Packet();
            ref var inf = ref interfaceDescriptionPacket._PCapEncapsulateHeader<PCapInterfaceDescriptionBlock>(out _, PCapBlockType.InterfaceDescription);
            inf.LinkType = PCapLinkType.Ethernet;

            SpanBuffer<byte> ret = new SpanBuffer<byte>();
            ret.Write(sectionHeaderPacket.Span);
            ret.Write(interfaceDescriptionPacket.Span);

            return ret;
        }

        public static Packet NewEmptyPacketForPCap(PacketSizeSet sizeSetForInnerPacket, int commentLength = 0)
        {
            PacketSizeSet sizeSet = sizeSetForInnerPacket;
            commentLength = Math.Min(commentLength, 10000);
            if (commentLength >= 1)
            {
                sizeSet += new PacketSizeSet(0, 8 + commentLength * 3);
            }
            sizeSet += PacketSizeSets.PcapNgPacket;

            Packet pkt = new Packet(sizeSet);

            return pkt;
        }
    }
}

