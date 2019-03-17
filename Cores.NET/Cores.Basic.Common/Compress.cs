﻿using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using IPA.Cores.Basic.Internal;

namespace IPA.Cores.Basic
{
    public static class ZLib
    {
        // データを圧縮する
        public static byte[] Compress(byte[] src)
        {
            return Compress(src, zlibConst.Z_DEFAULT_COMPRESSION);
        }
        public static byte[] Compress(byte[] src, int level)
        {
            return Compress(src, level, false);
        }
        public static byte[] Compress(byte[] src, int level, bool noHeader)
        {
            int dstSize = src.Length * 2 + 100;
            byte[] dst = new byte[dstSize];

            compress2(ref dst, src, level, noHeader);

            return dst;
        }

        // データを展開する
        public static byte[] Uncompress(byte[] src, int originalSize)
        {
            byte[] dst = new byte[originalSize];

            uncompress(ref dst, src);

            return dst;
        }

        static void compress2(ref byte[] dest, byte[] src, int level, bool noHeader)
        {
            ZStream stream = new ZStream();

            stream.next_in = src;
            stream.avail_in = src.Length;

            stream.next_out = dest;
            stream.avail_out = dest.Length;

            if (noHeader == false)
            {
                stream.deflateInit(level);
            }
            else
            {
                stream.deflateInit(level, -15);
            }

            stream.deflate(zlibConst.Z_FINISH);

            Array.Resize<byte>(ref dest, (int)stream.total_out);
        }

        static void uncompress(ref byte[] dest, byte[] src)
        {
            ZStream stream = new ZStream();

            stream.next_in = src;
            stream.avail_in = src.Length;

            stream.next_out = dest;
            stream.avail_out = dest.Length;

            stream.inflateInit();

            int err = stream.inflate(zlibConst.Z_FINISH);
            if (err != zlibConst.Z_STREAM_END)
            {
                stream.inflateEnd();
                throw new ApplicationException();
            }

            Array.Resize<byte>(ref dest, (int)stream.total_out);

            err = stream.inflateEnd();
            if (err != zlibConst.Z_OK)
            {
                throw new ApplicationException();
            }
        }
    }
}
