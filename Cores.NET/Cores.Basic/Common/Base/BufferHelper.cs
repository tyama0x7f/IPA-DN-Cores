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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Buffers.Binary;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

namespace IPA.Cores.Helper.Basic
{
    static class SpanMemoryBufferHelper
    {
        public static SpanBuffer<T> _AsSpanBuffer<T>(this Span<T> span) => new SpanBuffer<T>(span);
        public static SpanBuffer<T> _AsSpanBuffer<T>(this Memory<T> memory) => new SpanBuffer<T>(memory.Span);
        public static SpanBuffer<T> _AsSpanBuffer<T>(this T[] data) => new SpanBuffer<T>(data.AsSpan());
        public static SpanBuffer<T> _AsSpanBuffer<T>(this T[] data, int offset) => new SpanBuffer<T>(data.AsSpan(offset));
        public static SpanBuffer<T> _AsSpanBuffer<T>(this T[] data, int offset, int size) => new SpanBuffer<T>(data.AsSpan(offset, size));

        public static ReadOnlySpanBuffer<T> _AsReadOnlySpanBuffer<T>(this ReadOnlySpan<T> span) => new ReadOnlySpanBuffer<T>(span);
        public static ReadOnlySpanBuffer<T> _AsReadOnlySpanBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlySpanBuffer<T>(memory.Span);
        public static ReadOnlySpanBuffer<T> _AsReadOnlySpanBuffer<T>(this T[] data) => new ReadOnlySpanBuffer<T>(data._AsReadOnlySpan());
        public static ReadOnlySpanBuffer<T> _AsReadOnlySpanBuffer<T>(this T[] data, int offset) => new ReadOnlySpanBuffer<T>(data._AsReadOnlySpan(offset));
        public static ReadOnlySpanBuffer<T> _AsReadOnlySpanBuffer<T>(this T[] data, int offset, int size) => new ReadOnlySpanBuffer<T>(data._AsReadOnlySpan(offset, size));

        public static FastMemoryBuffer<T> _AsFastMemoryBuffer<T>(this Memory<T> memory) => new FastMemoryBuffer<T>(memory);
        public static FastMemoryBuffer<T> _AsFastMemoryBuffer<T>(this T[] data) => new FastMemoryBuffer<T>(data.AsMemory());
        public static FastMemoryBuffer<T> _AsFastMemoryBuffer<T>(this T[] data, int offset) => new FastMemoryBuffer<T>(data.AsMemory(offset));
        public static FastMemoryBuffer<T> _AsFastMemoryBuffer<T>(this T[] data, int offset, int size) => new FastMemoryBuffer<T>(data.AsMemory(offset, size));

        public static FastReadOnlyMemoryBuffer<T> _AsFastReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new FastReadOnlyMemoryBuffer<T>(memory);
        public static FastReadOnlyMemoryBuffer<T> _AsFastReadOnlyMemoryBuffer<T>(this T[] data) => new FastReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory());
        public static FastReadOnlyMemoryBuffer<T> _AsFastReadOnlyMemoryBuffer<T>(this T[] data, int offset) => new FastReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory(offset));
        public static FastReadOnlyMemoryBuffer<T> _AsFastReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new FastReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory(offset, size));

        public static MemoryBuffer<T> _AsMemoryBuffer<T>(this Memory<T> memory) => new MemoryBuffer<T>(memory);
        public static MemoryBuffer<T> _AsMemoryBuffer<T>(this T[] data) => new MemoryBuffer<T>(data.AsMemory());
        public static MemoryBuffer<T> _AsMemoryBuffer<T>(this T[] data, int offset) => new MemoryBuffer<T>(data.AsMemory(offset));
        public static MemoryBuffer<T> _AsMemoryBuffer<T>(this T[] data, int offset, int size) => new MemoryBuffer<T>(data.AsMemory(offset, size));

        public static ReadOnlyMemoryBuffer<T> _AsReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlyMemoryBuffer<T>(memory);
        public static ReadOnlyMemoryBuffer<T> _AsReadOnlyMemoryBuffer<T>(this T[] data) => new ReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory());
        public static ReadOnlyMemoryBuffer<T> _AsReadOnlyMemoryBuffer<T>(this T[] data, int offset) => new ReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory(offset));
        public static ReadOnlyMemoryBuffer<T> _AsReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new ReadOnlyMemoryBuffer<T>(data._AsReadOnlyMemory(offset, size));

        public static BufferDirectStream _AsDirectStream(this MemoryBuffer<byte> buffer) => new BufferDirectStream(buffer);
        public static BufferDirectStream _AsDirectStream(this ReadOnlyMemoryBuffer<byte> buffer) => new BufferDirectStream(buffer);
        public static BufferDirectStream _AsDirectStream(this HugeMemoryBuffer<byte> buffer) => new BufferDirectStream(buffer);
    }

    static class MemoryExtHelper
    {
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this Memory<T> memory) => memory;
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this Span<T> span) => span;

        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this ArraySegment<T> segment) => segment.AsMemory();
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this ArraySegment<T> segment, int start) => segment.AsMemory(start);
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this ArraySegment<T> segment, int start, int length) => segment.AsMemory(start, length);
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this T[] array) => array.AsMemory();
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this T[] array, int start) => array.AsMemory(start);
        public static ReadOnlyMemory<T> _AsReadOnlyMemory<T>(this T[] array, int start, int length) => array.AsMemory(start, length);
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this T[] array, int start) => array.AsSpan(start);
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this T[] array) => array.AsSpan();
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this ArraySegment<T> segment, int start, int length) => segment.AsSpan(start, length);
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this ArraySegment<T> segment, int start) => segment.AsSpan(start);
        public static ReadOnlySpan<T> _AsReadOnlySpan<T>(this T[] array, int start, int length) => array.AsSpan(start, length);

        public static ushort _Endian16(this ushort v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static short _Endian16(this short v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static uint _Endian32(this uint v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static int _Endian32(this int v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static ulong _Endian64(this ulong v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static long _Endian64(this long v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;

        public static ushort _ReverseEndian16(this ushort v) => BinaryPrimitives.ReverseEndianness(v);
        public static short _ReverseEndian16(this short v) => BinaryPrimitives.ReverseEndianness(v);
        public static uint _ReverseEndian32(this uint v) => BinaryPrimitives.ReverseEndianness(v);
        public static int _ReverseEndian32(this int v) => BinaryPrimitives.ReverseEndianness(v);
        public static ulong _ReverseEndian64(this ulong v) => BinaryPrimitives.ReverseEndianness(v);
        public static long _ReverseEndian64(this long v) => BinaryPrimitives.ReverseEndianness(v);

        #region AutoGenerated

        public static unsafe bool _GetBool8(this byte[] data, int offset = 0)
        {
            return (data[offset] == 0) ? false : true;
        }

        public static unsafe byte _GetUInt8(this byte[] data, int offset = 0)
        {
            return (byte)data[offset];
        }

        public static unsafe sbyte _GetSInt8(this byte[] data, int offset = 0)
        {
            return (sbyte)data[offset];
        }

        public static unsafe ushort _GetUInt16(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr + offset))) : *((ushort*)(ptr + offset));
        }

        public static unsafe short _GetSInt16(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr + offset))) : *((short*)(ptr + offset));
        }

        public static unsafe uint _GetUInt32(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr + offset))) : *((uint*)(ptr + offset));
        }

        public static unsafe int _GetSInt32(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr + offset))) : *((int*)(ptr + offset));
        }

        public static unsafe ulong _GetUInt64(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr + offset))) : *((ulong*)(ptr + offset));
        }

        public static unsafe long _GetSInt64(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr + offset))) : *((long*)(ptr + offset));
        }

        public static unsafe bool _GetBool8(this Span<byte> span)
        {
            return (span[0] == 0) ? false : true;
        }

        public static unsafe byte _GetUInt8(this Span<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte _GetSInt8(this Span<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort _GetUInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short _GetSInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint _GetUInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int _GetSInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong _GetUInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long _GetSInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool _GetBool8(this ReadOnlySpan<byte> span)
        {
            return (span[0] == 0) ? false : true;
        }

        public static unsafe byte _GetUInt8(this ReadOnlySpan<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte _GetSInt8(this ReadOnlySpan<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort _GetUInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short _GetSInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint _GetUInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int _GetSInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong _GetUInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long _GetSInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool _GetBool8(this Memory<byte> memory)
        {
            return (memory.Span[0] == 0) ? false : true;
        }

        public static unsafe byte _GetUInt8(this Memory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte _GetSInt8(this Memory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort _GetUInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short _GetSInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint _GetUInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int _GetSInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong _GetUInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long _GetSInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool _GetBool8(this ReadOnlyMemory<byte> memory)
        {
            return (memory.Span[0] == 0) ? false : true;
        }

        public static unsafe byte _GetUInt8(this ReadOnlyMemory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte _GetSInt8(this ReadOnlyMemory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort _GetUInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short _GetSInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint _GetUInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int _GetSInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong _GetUInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long _GetSInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }


        public static unsafe void _SetBool8(this bool value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetBool8(this byte[] data, bool value, int offset = 0)
        {
            data[offset] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetUInt8(this byte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void _SetUInt8(this byte[] data, byte value, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void _SetSInt8(this sbyte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void _SetSInt8(this byte[] data, sbyte value, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void _SetUInt16(this ushort value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ushort*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt16(this byte[] data, ushort value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ushort*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this short value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((short*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this byte[] data, short value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((short*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this uint value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this byte[] data, uint value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this int value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((int*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this byte[] data, int value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((int*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this ulong value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ulong*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this byte[] data, ulong value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ulong*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this long value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((long*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this byte[] data, long value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((long*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetBool8(this bool value, Span<byte> span)
        {
            span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetBool8(this Span<byte> span, bool value)
        {
            span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetUInt8(this byte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void _SetUInt8(this Span<byte> span, byte value)
        {
            span[0] = (byte)value;
        }

        public static unsafe void _SetSInt8(this sbyte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void _SetSInt8(this Span<byte> span, sbyte value)
        {
            span[0] = (byte)value;
        }

        public static unsafe void _SetUInt16(this ushort value, Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt16(this Span<byte> span, ushort value)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this short value, Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this Span<byte> span, short value)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this uint value, Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this Span<byte> span, uint value)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this int value, Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this Span<byte> span, int value)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this ulong value, Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this Span<byte> span, ulong value)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this long value, Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this Span<byte> span, long value)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetBool8(this bool value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetBool8(this Memory<byte> memory, bool value)
        {
            memory.Span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void _SetUInt8(this byte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void _SetUInt8(this Memory<byte> memory, byte value)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void _SetSInt8(this sbyte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void _SetSInt8(this Memory<byte> memory, sbyte value)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void _SetUInt16(this ushort value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt16(this Memory<byte> memory, ushort value)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this short value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt16(this Memory<byte> memory, short value)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this uint value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt32(this Memory<byte> memory, uint value)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this int value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt32(this Memory<byte> memory, int value)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this ulong value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetUInt64(this Memory<byte> memory, ulong value)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this long value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void _SetSInt64(this Memory<byte> memory, long value)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }


        public static unsafe byte[] _GetBool8(this bool value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)(value ? 1 : 0);
            return data;
        }

        public static unsafe byte[] _GetUInt8(this byte value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)value;
            return data;
        }

        public static unsafe byte[] _GetSInt8(this sbyte value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)value;
            return data;
        }

        public static unsafe byte[] _GetUInt16(this ushort value)
        {
            byte[] data = new byte[2];
            fixed (byte* ptr = data)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] _GetSInt16(this short value)
        {
            byte[] data = new byte[2];
            fixed (byte* ptr = data)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] _GetUInt32(this uint value)
        {
            byte[] data = new byte[4];
            fixed (byte* ptr = data)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] _GetSInt32(this int value)
        {
            byte[] data = new byte[4];
            fixed (byte* ptr = data)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] _GetUInt64(this ulong value)
        {
            byte[] data = new byte[8];
            fixed (byte* ptr = data)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] _GetSInt64(this long value)
        {
            byte[] data = new byte[8];
            fixed (byte* ptr = data)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }
        #endregion

        public static void _WalkWrite<T>(ref this Span<T> span, ReadOnlySpan<T> data) => data.CopyTo(span._Walk(data.Length));

        public static ReadOnlySpan<T> _Walk<T>(ref this ReadOnlySpan<T> span, int size)
        {
            if (size == 0) return Span<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = span;
            span = span.Slice(size);
            return original.Slice(0, size);
        }

        public static Span<T> _Walk<T>(ref this Span<T> span, int size)
        {
            if (size == 0) return Span<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = span;
            span = span.Slice(size);
            return original.Slice(0, size);
        }

        public static void _WalkWriteBool8(ref this Span<byte> span, bool value) => value._SetBool8(span._Walk(1));
        public static void _WalkWriteUInt8(ref this Span<byte> span, byte value) => value._SetUInt8(span._Walk(1));
        public static void _WalkWriteUInt16(ref this Span<byte> span, ushort value) => value._SetUInt16(span._Walk(2));
        public static void _WalkWriteUInt32(ref this Span<byte> span, uint value) => value._SetUInt32(span._Walk(4));
        public static void _WalkWriteUInt64(ref this Span<byte> span, ulong value) => value._SetUInt64(span._Walk(8));
        public static void _WalkWriteSInt8(ref this Span<byte> span, sbyte value) => value._SetSInt8(span._Walk(1));
        public static void _WalkWriteSInt16(ref this Span<byte> span, short value) => value._SetSInt16(span._Walk(2));
        public static void _WalkWriteSInt32(ref this Span<byte> span, int value) => value._SetSInt32(span._Walk(4));
        public static void _WalkWriteSInt64(ref this Span<byte> span, long value) => value._SetSInt64(span._Walk(8));

        public static Span<T> _WalkRead<T>(ref this Span<T> span, int size) => span._Walk(size);

        public static ReadOnlySpan<T> _WalkRead<T>(ref this ReadOnlySpan<T> span, int size) => span._Walk(size);

        public static bool _WalkReadBool8(ref this Span<byte> span) => span._WalkRead(1)._GetBool8();
        public static byte _WalkReadUInt8(ref this Span<byte> span) => span._WalkRead(1)._GetUInt8();
        public static ushort _WalkReadUInt16(ref this Span<byte> span) => span._WalkRead(2)._GetUInt16();
        public static uint _WalkReadUInt32(ref this Span<byte> span) => span._WalkRead(4)._GetUInt32();
        public static ulong _WalkReadUInt64(ref this Span<byte> span) => span._WalkRead(8)._GetUInt64();
        public static sbyte _WalkReadSInt8(ref this Span<byte> span) => span._WalkRead(1)._GetSInt8();
        public static short _WalkReadSInt16(ref this Span<byte> span) => span._WalkRead(2)._GetSInt16();
        public static int _WalkReadSInt32(ref this Span<byte> span) => span._WalkRead(4)._GetSInt32();
        public static long _WalkReadSInt64(ref this Span<byte> span) => span._WalkRead(8)._GetSInt64();

        public static bool _WalkReadBool8(ref this ReadOnlySpan<byte> span) => span._WalkRead(1)._GetBool8();
        public static byte _WalkReadUInt8(ref this ReadOnlySpan<byte> span) => span._WalkRead(1)._GetUInt8();
        public static ushort _WalkReadUInt16(ref this ReadOnlySpan<byte> span) => span._WalkRead(2)._GetUInt16();
        public static uint _WalkReadUInt32(ref this ReadOnlySpan<byte> span) => span._WalkRead(4)._GetUInt32();
        public static ulong _WalkReadUInt64(ref this ReadOnlySpan<byte> span) => span._WalkRead(8)._GetUInt64();
        public static sbyte _WalkReadSInt8(ref this ReadOnlySpan<byte> span) => span._WalkRead(1)._GetSInt8();
        public static short _WalkReadSInt16(ref this ReadOnlySpan<byte> span) => span._WalkRead(2)._GetSInt16();
        public static int _WalkReadSInt32(ref this ReadOnlySpan<byte> span) => span._WalkRead(4)._GetSInt32();
        public static long _WalkReadSInt64(ref this ReadOnlySpan<byte> span) => span._WalkRead(8)._GetSInt64();

        public static Memory<T> _Walk<T>(ref this Memory<T> memory, int size)
        {
            if (size == 0) return Memory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = memory;
            memory = memory.Slice(size);
            return original.Slice(0, size);
        }

        public static ReadOnlyMemory<T> _Walk<T>(ref this ReadOnlyMemory<T> memory, int size)
        {
            if (size == 0) return ReadOnlyMemory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = memory;
            memory = memory.Slice(size);
            return original.Slice(0, size);
        }

        public static int _WalkGetPin<T>(this Memory<T> memory) => _WalkGetPin(memory._AsReadOnlyMemory());
        public static int _WalkGetPin<T>(this ReadOnlyMemory<T> memory) => memory._AsSegment().Offset;

        public static int _WalkGetCurrentLength<T>(this Memory<T> memory, int compareTargetPin) => _WalkGetCurrentLength(memory._AsReadOnlyMemory(), compareTargetPin);

        public static int _WalkGetCurrentLength<T>(this ReadOnlyMemory<T> memory, int compareTargetPin)
        {
            int currentPin = memory._WalkGetPin();
            if (currentPin < compareTargetPin) throw new ArgumentOutOfRangeException("currentPin < compareTargetPin");
            return currentPin - compareTargetPin;
        }

        public static Memory<T> _SliceWithPin<T>(this Memory<T> memory, int pin, int? size = null)
        {
            if (size == 0) return Memory<T>.Empty;
            if (pin < 0) throw new ArgumentOutOfRangeException("pin");

            ArraySegment<T> a = memory._AsSegment();
            if (size == null)
            {
                size = a.Offset + a.Count - pin;
            }
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if ((a.Offset + a.Count) < pin)
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < pin");
            }
            if ((a.Offset + a.Count) < (pin + size))
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < (pin + size)");
            }

            ArraySegment<T> b = new ArraySegment<T>(a.Array, pin, size ?? 0);
            return b.AsMemory();
        }

        public static ReadOnlyMemory<T> _SliceWithPin<T>(this ReadOnlyMemory<T> memory, int pin, int? size = null)
        {
            if (size == 0) return Memory<T>.Empty;
            if (pin < 0) throw new ArgumentOutOfRangeException("pin");

            ArraySegment<T> a = memory._AsSegment();
            if (size == null)
            {
                size = a.Offset + a.Count - pin;
            }
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if ((a.Offset + a.Count) < pin)
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < pin");
            }
            if ((a.Offset + a.Count) < (pin + size))
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < (pin + size)");
            }

            ArraySegment<T> b = new ArraySegment<T>(a.Array, pin, size ?? 0);
            return b.AsMemory();
        }

        public static void _WalkAutoRynamicEnsureReserveBuffer<T>(ref this Memory<T> memory, int size) => memory._WalkAutoInternal(size, false, true);
        public static Memory<T> _WalkAutoDynamic<T>(ref this Memory<T> memory, int size) => memory._WalkAutoInternal(size, false, false);
        public static Memory<T> _WalkAutoStatic<T>(ref this Memory<T> memory, int size) => memory._WalkAutoInternal(size, true, false);

        static Memory<T> _WalkAutoInternal<T>(ref this Memory<T> memory, int size, bool noReAlloc, bool noStep)
        {
            if (size == 0) return Memory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if (memory.Length >= size)
            {
                return memory._Walk(size);
            }

            if (((long)memory.Length + (long)size) > int.MaxValue) throw new OverflowException("size");

            ArraySegment<T> a = memory._AsSegment();
            long requiredLen = (long)a.Offset + (long)a.Count + (long)size;
            if (requiredLen > int.MaxValue) throw new OverflowException("size");

            int newLen = a.Array.Length;
            while (newLen < requiredLen)
            {
                newLen = (int)Math.Min(Math.Max((long)newLen, 128) * 2, int.MaxValue);
            }

            T[] newArray = a.Array;
            if (newArray.Length < newLen)
            {
                if (noReAlloc)
                {
                    throw new ArgumentOutOfRangeException("Internal byte array overflow: array.Length < newLen");
                }
                newArray = a.Array._ReAlloc(newLen);
            }

            if (noStep == false)
            {
                a = new ArraySegment<T>(newArray, a.Offset, Math.Max(a.Count, size));
            }
            else
            {
                a = new ArraySegment<T>(newArray, a.Offset, a.Count);
            }

            var m = a.AsMemory();

            if (noStep == false)
            {
                var ret = m._Walk(size);
                memory = m;
                return ret;
            }
            else
            {
                memory = m;
                return Memory<T>.Empty;
            }
        }

        public static void _WalkWriteBool8(ref this Memory<byte> memory, bool value) => value._SetBool8(memory._Walk(1));
        public static void _WalkWriteUInt8(ref this Memory<byte> memory, byte value) => value._SetUInt8(memory._Walk(1));
        public static void _WalkWriteUInt16(ref this Memory<byte> memory, ushort value) => value._SetUInt16(memory._Walk(2));
        public static void _WalkWriteUInt32(ref this Memory<byte> memory, uint value) => value._SetUInt32(memory._Walk(4));
        public static void _WalkWriteUInt64(ref this Memory<byte> memory, ulong value) => value._SetUInt64(memory._Walk(8));
        public static void _WalkWriteSInt8(ref this Memory<byte> memory, sbyte value) => value._SetSInt8(memory._Walk(1));
        public static void _WalkWriteSInt16(ref this Memory<byte> memory, short value) => value._SetSInt16(memory._Walk(2));
        public static void _WalkWriteSInt32(ref this Memory<byte> memory, int value) => value._SetSInt32(memory._Walk(4));
        public static void _WalkWriteSInt64(ref this Memory<byte> memory, long value) => value._SetSInt64(memory._Walk(8));
        public static void _WalkWrite<T>(ref this Memory<T> memory, ReadOnlyMemory<T> data) => data.CopyTo(memory._Walk(data.Length));
        public static void _WalkWrite<T>(ref this Memory<T> memory, ReadOnlySpan<T> data) => data.CopyTo(memory._Walk(data.Length).Span);
        public static void _WalkWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory._Walk(data.Length).Span);

        public static void _WalkAutoDynamicWriteBool8(ref this Memory<byte> memory, bool value) => value._SetBool8(memory._WalkAutoDynamic(1));
        public static void _WalkAutoDynamicWriteUInt8(ref this Memory<byte> memory, byte value) => value._SetUInt8(memory._WalkAutoDynamic(1));
        public static void _WalkAutoDynamicWriteUInt16(ref this Memory<byte> memory, ushort value) => value._SetUInt16(memory._WalkAutoDynamic(2));
        public static void _WalkAutoDynamicWriteUInt32(ref this Memory<byte> memory, uint value) => value._SetUInt32(memory._WalkAutoDynamic(4));
        public static void _WalkAutoDynamicWriteUInt64(ref this Memory<byte> memory, ulong value) => value._SetUInt64(memory._WalkAutoDynamic(8));
        public static void _WalkAutoDynamicWriteSInt8(ref this Memory<byte> memory, sbyte value) => value._SetSInt8(memory._WalkAutoDynamic(1));
        public static void _WalkAutoDynamicWriteSInt16(ref this Memory<byte> memory, short value) => value._SetSInt16(memory._WalkAutoDynamic(2));
        public static void _WalkAutoDynamicWriteSInt32(ref this Memory<byte> memory, int value) => value._SetSInt32(memory._WalkAutoDynamic(4));
        public static void _WalkAutoDynamicWriteSInt64(ref this Memory<byte> memory, long value) => value._SetSInt64(memory._WalkAutoDynamic(8));
        public static void _WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory._WalkAutoDynamic(data.Length));
        public static void _WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory._WalkAutoDynamic(data.Length).Span);
        public static void _WalkAutoDynamicWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory._WalkAutoDynamic(data.Length).Span);

        public static void _WalkAutoStaticWriteBool8(ref this Memory<byte> memory, bool value) => value._SetBool8(memory._WalkAutoStatic(1));
        public static void _WalkAutoStaticWriteUInt8(ref this Memory<byte> memory, byte value) => value._SetUInt8(memory._WalkAutoStatic(1));
        public static void _WalkAutoStaticWriteUInt16(ref this Memory<byte> memory, ushort value) => value._SetUInt16(memory._WalkAutoStatic(2));
        public static void _WalkAutoStaticWriteUInt32(ref this Memory<byte> memory, uint value) => value._SetUInt32(memory._WalkAutoStatic(4));
        public static void _WalkAutoStaticWriteUInt64(ref this Memory<byte> memory, ulong value) => value._SetUInt64(memory._WalkAutoStatic(8));
        public static void _WalkAutoStaticWriteSInt8(ref this Memory<byte> memory, sbyte value) => value._SetSInt8(memory._WalkAutoStatic(1));
        public static void _WalkAutoStaticWriteSInt16(ref this Memory<byte> memory, short value) => value._SetSInt16(memory._WalkAutoStatic(2));
        public static void _WalkAutoStaticWriteSInt32(ref this Memory<byte> memory, int value) => value._SetSInt32(memory._WalkAutoStatic(4));
        public static void _WalkAutoStaticWriteSInt64(ref this Memory<byte> memory, long value) => value._SetSInt64(memory._WalkAutoStatic(8));
        public static void _WalkAutoStaticWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory._WalkAutoStatic(data.Length));
        public static void _WalkAutoStaticWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory._WalkAutoStatic(data.Length).Span);
        public static void _WalkAutoStaticWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory._WalkAutoStatic(data.Length).Span);

        public static ReadOnlyMemory<T> _WalkRead<T>(ref this ReadOnlyMemory<T> memory, int size) => memory._Walk(size);
        public static Memory<T> _WalkRead<T>(ref this Memory<T> memory, int size) => memory._Walk(size);

        public static bool _WalkReadBool8(ref this Memory<byte> memory) => memory._WalkRead(1)._GetBool8();
        public static byte _WalkReadUInt8(ref this Memory<byte> memory) => memory._WalkRead(1)._GetUInt8();
        public static ushort _WalkReadUInt16(ref this Memory<byte> memory) => memory._WalkRead(2)._GetUInt16();
        public static uint _WalkReadUInt32(ref this Memory<byte> memory) => memory._WalkRead(4)._GetUInt32();
        public static ulong _WalkReadUInt64(ref this Memory<byte> memory) => memory._WalkRead(8)._GetUInt64();
        public static sbyte _WalkReadSInt8(ref this Memory<byte> memory) => memory._WalkRead(1)._GetSInt8();
        public static short _WalkReadSInt16(ref this Memory<byte> memory) => memory._WalkRead(2)._GetSInt16();
        public static int _WalkReadSInt32(ref this Memory<byte> memory) => memory._WalkRead(4)._GetSInt32();
        public static long _WalkReadSInt64(ref this Memory<byte> memory) => memory._WalkRead(8)._GetSInt64();

        public static bool _WalkReadBool8(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(1)._GetBool8();
        public static byte _WalkReadUInt8(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(1)._GetUInt8();
        public static ushort _WalkReadUInt16(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(2)._GetUInt16();
        public static uint _WalkReadUInt32(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(4)._GetUInt32();
        public static ulong _WalkReadUInt64(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(8)._GetUInt64();
        public static sbyte _WalkReadSInt8(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(1)._GetSInt8();
        public static short _WalkReadSInt16(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(2)._GetSInt16();
        public static int _WalkReadSInt32(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(4)._GetSInt32();
        public static long _WalkReadSInt64(ref this ReadOnlyMemory<byte> memory) => memory._WalkRead(8)._GetSInt64();

        static Action InternalFastThrowVitalException = new Action(() => { throw new ApplicationException("Vital Error"); });
        public static void _FastThrowVitalError()
        {
            InternalFastThrowVitalException();
        }

        public static ArraySegment<T> _AsSegmentSlow<T>(this Memory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
            {
                _FastThrowVitalError();
            }

            return seg;
        }

        public static ArraySegment<T> _AsSegmentSlow<T>(this ReadOnlyMemory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
            {
                _FastThrowVitalError();
            }

            return seg;
        }

        public static T[] _ReAlloc<T>(this T[] src, int newSize)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (newSize == src.Length)
            {
                return src;
            }

            T[] ret = src;
            Array.Resize(ref ret, newSize);
            return ret;
        }

        public static Span<T> _ReAlloc<T>(this Span<T> src, int newSize, int maxCopySize = int.MaxValue)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (maxCopySize < 0) throw new ArgumentOutOfRangeException("maxCopySize");
            if (newSize == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[newSize];
                int copySize = Math.Min(Math.Min(src.Length, ret.Length), maxCopySize);
                if (copySize >= 1)
                    src.Slice(0, copySize).CopyTo(ret);
                return ret.AsSpan();
            }
        }

        public static Memory<T> _ReAlloc<T>(this Memory<T> src, int newSize, int maxCopySize = int.MaxValue)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (maxCopySize < 0) throw new ArgumentOutOfRangeException("maxCopySize");
            if (newSize == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[newSize];
                int copySize = Math.Min(Math.Min(src.Length, ret.Length), maxCopySize);
                if (copySize >= 1)
                    src.Slice(0, copySize).CopyTo(ret);
                return ret.AsMemory();
            }
        }

        public static void _FastFree<T>(this T[] a)
        {
            if (a.Length >= Cores.Basic.MemoryHelper.MemoryUsePoolThreshold)
                ArrayPool<T>.Shared.Return(a);
        }

        public static void _FastFree<T>(this Memory<T> memory) => memory._GetInternalArray()._FastFree();

        public static T[] _GetInternalArray<T>(this Memory<T> memory)
        {
            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                ptr += Cores.Basic.MemoryHelper._MemoryObjectOffset;
                T[] o = Unsafe.Read<T[]>(ptr);
                return o;
            }
        }
        public static int _GetInternalArrayLength<T>(this Memory<T> memory) => _GetInternalArray(memory).Length;

        public static ArraySegment<T> _AsSegment<T>(this Memory<T> memory)
        {
            if (Cores.Basic.MemoryHelper._UseFast == false) return _AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + Cores.Basic.MemoryHelper._MemoryObjectOffset),
                    *((int*)(ptr + Cores.Basic.MemoryHelper._MemoryIndexOffset)),
                    *((int*)(ptr + Cores.Basic.MemoryHelper._MemoryLengthOffset))
                    );
            }
        }

        public static ArraySegment<T> _AsSegment<T>(this ReadOnlyMemory<T> memory)
        {
            if (Cores.Basic.MemoryHelper._UseFast == false) return _AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + Cores.Basic.MemoryHelper._MemoryObjectOffset),
                    *((int*)(ptr + Cores.Basic.MemoryHelper._MemoryIndexOffset)),
                    *((int*)(ptr + Cores.Basic.MemoryHelper._MemoryLengthOffset))
                    );
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T _AsStruct<T>(this ref byte data) => ref Unsafe.As<byte, T>(ref data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T _AsStruct<T>(this Span<byte> data) => ref Unsafe.As<byte, T>(ref data[0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref readonly T _AsStruct<T>(this ReadOnlySpan<byte> data)
        {
            fixed (void* ptr = &data[0])
                return ref Unsafe.AsRef<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T _AsStruct<T>(this Memory<byte> data) => ref Unsafe.As<byte, T>(ref data.Span[0]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref readonly T _AsStruct<T>(this ReadOnlyMemory<byte> data)
        {
            fixed (void* ptr = &data.Span[0])
                return ref Unsafe.AsRef<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T _AsStructSafe<T>(this Span<byte> data, int minSize = 0)
        {
            if (minSize <= 0) minSize = Unsafe.SizeOf<T>();
            return ref Unsafe.As<byte, T>(ref data.Slice(0, minSize)[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref readonly T _AsStructSafe<T>(this ReadOnlySpan<byte> data, int minSize = 0)
        {
            if (minSize <= 0) minSize = Unsafe.SizeOf<T>();
            fixed (void* ptr = &data.Slice(0, minSize)[0])
                return ref Unsafe.AsRef<T>(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T _AsStructSafe<T>(this Memory<byte> data, int minSize = 0)
        {
            if (minSize <= 0) minSize = Unsafe.SizeOf<T>();
            return ref Unsafe.As<byte, T>(ref data.Span.Slice(0, minSize)[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref readonly T _AsStructSafe<T>(this ReadOnlyMemory<byte> data, int minSize = 0)
        {
            if (minSize <= 0) minSize = Unsafe.SizeOf<T>();
            fixed (void* ptr = &data.Span.Slice(0, minSize)[0])
                return ref Unsafe.AsRef<T>(ptr);
        }
    }
}
