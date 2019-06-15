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
using System.IO.Enumeration;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Diagnostics;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using System.Runtime.InteropServices;
using IPA.Cores.ClientApi.Acme;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;



#pragma warning disable CS0219
#pragma warning disable CS0162


namespace IPA.TestDev
{
    [Serializable]
    [DataContract]
    class TestData
    {
        [DataMember]
        public int A;
        [DataMember]
        public string B;
        [DataMember]
        public int C;
    }

    static class EnumTestClass
    {
        public static int GetValue<TKey>(TKey src) where TKey : unmanaged, Enum
        {
            return src.GetHashCode();
        }
        public static unsafe int GetValue2<TKey>(TKey src) where TKey : unmanaged, Enum
        {
            void* ptr = Unsafe.AsPointer(ref src);
            return *((int*)ptr);
        }
    }

    class TestHiveData1
    {
        public string Str;
        public string Date;
        public List<string> StrList = new List<string>();
    }

    class ZZ
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TcpDirectionType Z;
    }

    static class TestClass
    {
        public static void Test()
        {
            //"eyJ0ZXJtc09mU2VydmljZUFncmVlZCI6dHJ1ZSwiY29udGFjdCI6WyJtYWlsdG86ZGEuMTkwNjE1QHNvZnRldGhlci5jby5qcCJdLCJzdGF0dXMiOm51bGwsImlkIjpudWxsLCJjcmVhdGVkQXQiOiIwMDAxLTAxLTAxVDAwOjAwOjAwIiwia2V5IjpudWxsLCJpbml0aWFsSXAiOm51bGwsIm9yZGVycyI6bnVsbCwiTG9jYXRpb24iOm51bGx9"
            //    ._Base64UrlDecode()._GetString_UTF8()._Print();
            //return;
            //Test_Generic();

            //Test_RSA_Cert();

            //Test_ECDSA_Cert();

            //Test_Acme();

            Test_Acme_Junk();

            //Test_HiveLock();

            //Test_PersistentCache();
        }

        public static void Test_Generic()
        {
            if (false)
            {
                PkiUtil.GenerateKeyPair(PkiAlgorithm.ECDSA, 256, out PrivKey privateKey, out PubKey publicKey);

                var signer = privateKey.GetSigner();
                var verifier = publicKey.GetVerifier();

                publicKey.EcdsaParameters.Q.AffineXCoord.GetEncoded()._GetHexString()._Print();

                byte[] target = "Hello"._GetBytes_Ascii();

                byte[] sign = signer.Sign(target);

                signer.AlgorithmName._Print();
                sign._GetHexString()._Print();

                target[2] = 0;

                verifier.Verify(sign, target)._Print();
            }
            else
            {
                PkiUtil.GenerateKeyPair(PkiAlgorithm.ECDSA, 256, out PrivKey privateKey, out PubKey publicKey);

                JwsUtil.Encapsulate(privateKey, "abc", "url", Secure.Rand(8))._PrintAsJson();
            }
        }

        public static void Test_Acme_Junk()
        {
            LetsEncryptClient c = new LetsEncryptClient("https://acme-staging-v02.api.letsencrypt.org/directory");
            c.Init("da.190615@softether.co.jp")._GetResult();
        }

        public static void Test_Acme()
        {
            PkiUtil.GenerateKeyPair(PkiAlgorithm.RSA, 2048, out PrivKey key, out PubKey publicKey);

            AcmeClientOptions o = new AcmeClientOptions();

            using (AcmeClient acme = new AcmeClient(o))
            {
                acme.NewAccountAsync(key, "mailto:da.190614@softether.co.jp"._SingleArray())._GetResult();
            }
        }

        public static void Test_PersistentCache()
        {
            PersistentLocalCache<TestHiveData1> c = new PersistentLocalCache<TestHiveData1>("cacheTest1", new TimeSpan(0, 0, 15), true,
                async (can) =>
                {
                    throw new ApplicationException("a");
                    await Task.CompletedTask;
                    return new TestHiveData1() { Date = DateTime.Now._ToDtStr(true) };
                }
                );

            var d = c.GetAsync().Result;

            Con.WriteLine(d.Date);
        }

        public static void Test_HiveLock()
        {
            int num = 10;
            HiveData<HiveKeyValue> test = Hive.LocalAppSettings["testlock"];

            for (int i = 0; i < num; i++)
            {
                test.AccessData(true, kv =>
                {
                    int value = kv.GetSInt32("value");
                    value++;
                    kv.SetSInt32("value", value);
                });
            }
        }
        public static void Test_ECDSA_Cert()
        {
            string tmpDir = @"c:\tmp\190614_ecdsa";

            PrivKey privateKey;
            PubKey publicKey;

            Lfs.CreateDirectory(tmpDir);

            if (true)
            {
                PkiUtil.GenerateEcdsaKeyPair(256, out privateKey, out publicKey);

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_private.key"), privateKey.Export());
                Lfs.WriteDataToFile(tmpDir._CombinePath("root_private_encrypted.key"), privateKey.Export("microsoft"));

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_public.key"), publicKey.Export());
            }

            privateKey = new PrivKey(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_private_encrypted.key")).Span, "microsoft");
            publicKey = new PubKey(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_public.key")).Span);

            Certificate cert;

            if (true)
            {
                cert = new Certificate(privateKey, new CertificateOptions(PkiAlgorithm.ECDSA, "www.abc", serial: new byte[] { 1, 2, 3 }, shaSize: PkiShaSize.SHA512));

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_cert.crt"), cert.Export());
            }

            cert = new Certificate(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_cert.crt")).Span);

            Csr csr = new Csr(PkiAlgorithm.ECDSA, new CertificateOptions(PkiAlgorithm.ECDSA, "www.softether.com"), 256);
            Lfs.WriteDataToFile(@"C:\TMP\190614_ecdsa\testcsr.txt", csr.ExportPem());

            DoNothing();
        }

        public static void Test_RSA_Cert()
        {
            string tmpDir = @"c:\tmp\190613_cert";

            PrivKey privateKey;
            PubKey publicKey;

            Lfs.CreateDirectory(tmpDir);

            if (true)
            {
                PkiUtil.GenerateRsaKeyPair(1024, out privateKey, out publicKey);

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_private.key"), privateKey.Export());
                Lfs.WriteDataToFile(tmpDir._CombinePath("root_private_encrypted.key"), privateKey.Export("microsoft"));

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_public.key"), publicKey.Export());
            }

            privateKey = new PrivKey(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_private_encrypted.key")).Span, "microsoft");
            publicKey = new PubKey(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_public.key")).Span);

            Certificate cert;

            if (true)
            {
                cert = new Certificate(privateKey, new CertificateOptions(PkiAlgorithm.RSA, "www.abc", serial: new byte[] { 1, 2, 3 }));

                Lfs.WriteDataToFile(tmpDir._CombinePath("root_cert.crt"), cert.Export());
            }

            cert = new Certificate(Lfs.ReadDataFromFile(tmpDir._CombinePath("root_cert.crt")).Span);

            //CertificateStore store = new CertificateStore(Lfs.ReadDataFromFile(@"C:\TMP\190613_cert\p12test.p12").Span);
            CertificateStore store = new CertificateStore(Lfs.ReadDataFromFile(@"H:\Crypto\all.open.ad.jp\cert.pfx").Span, "microsoft");

            Lfs.WriteDataToFile(@"C:\TMP\190613_cert\export_test.pfx", store.ExportPkcs12());

            store.ExportChainedPem(out ReadOnlyMemory<byte> exportPemCert, out ReadOnlyMemory<byte> exportPemKey);
            Lfs.WriteDataToFile(@"C:\TMP\190613_cert\export_test_chained.cer", exportPemCert);
            Lfs.WriteDataToFile(@"C:\TMP\190613_cert\export_test_chained.key", exportPemKey);

            CertificateStore store2 = new CertificateStore(Lfs.ReadDataFromFile(@"C:\TMP\190613_cert\export_test_chained.cer").Span, Lfs.ReadDataFromFile(@"C:\TMP\190613_cert\export_test_chained.key").Span, "");

            Lfs.WriteDataToFile(@"C:\TMP\190613_cert\export_test2.pfx", store2.ExportPkcs12());

            Csr csr = new Csr(PkiAlgorithm.RSA, new CertificateOptions(PkiAlgorithm.ECDSA, "www.softether.com", shaSize: PkiShaSize.SHA512), 1024);
            Lfs.WriteDataToFile(@"C:\TMP\190613_cert\testcsr.txt", csr.ExportPem());

            DoNothing();
        }

        public static void Test_11()
        {
            // Logger Tester
            PalSslServerAuthenticationOptions svrSsl = new PalSslServerAuthenticationOptions(DevTools.TestSampleCert, true, null);
            PalSslClientAuthenticationOptions cliSsl = new PalSslClientAuthenticationOptions(false, null, DevTools.TestSampleCert.HashSHA1);

            using (LogClient client = new LogClient(new LogClientOptions(null, cliSsl, "127.0.0.1")))
            {
                using (LogServer server = new LogServer(new LogServerOptions(null, @"c:\tmp\190612", FileFlags.OnCreateSetCompressionFlag, null, null, svrSsl)))
                {
                    CancellationTokenSource cts = new CancellationTokenSource();

                    Task testTask = TaskUtil.StartAsyncTaskAsync(async () =>
                    {
                        for (int i = 0; ; i++)
                        {
                            if (cts.IsCancellationRequested) return;

                            client.WriteLog(new LogJsonData()
                            {
                                AppName = "App",
                                Data = "Hello World " + i.ToString(),
                                Guid = Str.NewGuid(),
                                Kind = LogKind.Default,
                                MachineName = "Neko",
                                Priority = LogPriority.Info.ToString(),
                                Tag = "TagSan",
                                TimeStamp = DateTimeOffset.Now,
                                TypeName = "xyz"
                            }
                            );

                            await Task.Delay(100);
                        }
                    });

                    Con.ReadLine("Exit>");

                    cts.Cancel();

                    testTask._TryWait();
                }
            }
        }

        public static unsafe void Test01()
        {
            if (true)
            {

            }

            if (true)
            {
                using (PCapPacketRecorder r = new PCapPacketRecorder(new TcpPseudoPacketGeneratorOptions(TcpDirectionType.Client, IPAddress.Parse("192.168.0.1"), 1, IPAddress.Parse("192.168.0.2"), 2)))
                {
                    r.RegisterEmitter(new PCapFileEmitter(new PCapFileEmitterOptions(new FilePath(@"c:\tmp\190608\test.pcapng", flags: FileFlags.AutoCreateDirectory),
    false)));

                    var g = r.TcpGen;

                    g.EmitConnected();

                    g.EmitData("1aa1"._GetBytes_Ascii(), Direction.Send);
                    g.EmitData("4d4"._GetBytes_Ascii(), Direction.Recv);
                    g.EmitData("2bbbb2"._GetBytes_Ascii(), Direction.Send);
                    g.EmitData("5eeeeeeeee5"._GetBytes_Ascii(), Direction.Recv);
                    g.EmitData("3cccccc3"._GetBytes_Ascii(), Direction.Send);
                    g.EmitData("6fffffffffffffff6"._GetBytes_Ascii(), Direction.Recv);

                    //g.EmitReset(Direction.Send);

                    g.EmitFinish(Direction.Send);
                }
                return;
            }


            Packet p = PCapUtil.NewEmptyPacketForPCap(PacketSizeSets.NormalTcpIpPacket_V4 + 5, 0);

            ref byte payloadDest = ref p.PrependSpan<byte>(5);
            "Hello"._GetBytes_Ascii().CopyTo(new Span<byte>(Unsafe.AsPointer(ref payloadDest), 5));

            PacketSpan<TCPHeader> tcp = p.PrependSpanWithData<TCPHeader>(
                new TCPHeader()
                {
                    AckNumber = 123U._Endian32(),
                    SeqNumber = 456U._Endian32(),
                    SrcPort = 80U._Endian16(),
                    DstPort = 443U._Endian16(),
                    Flag = TCPFlags.Ack | TCPFlags.Psh,
                    HeaderLen = (byte)((sizeof(TCPHeader)) / 4),
                    WindowSize = 1234U._Endian16(),
                },
                sizeof(TCPHeader));

            PacketSpan<IPv4Header> ip = tcp.PrependSpanWithData<IPv4Header>(ref p,
                new IPv4Header()
                {
                    SrcIP = 0x12345678,
                    DstIP = 0xdeadbeef,
                    Flags = IPv4Flags.DontFragment,
                    HeaderLen = (byte)(sizeof(IPv4Header) / 4),
                    Identification = 0x1234U._Endian16(),
                    Protocol = IPProtocolNumber.TCP,
                    TimeToLive = 12,
                    TotalLength = ((ushort)(sizeof(IPv4Header) + tcp.GetTotalPacketSize(ref p)))._Endian16(),
                    Version = 4,
                });

            ref IPv4Header v4Header = ref ip.GetRefValue(ref p);

            ref TCPHeader tcpHeader = ref tcp.GetRefValue(ref p);

            v4Header.Checksum = v4Header.CalcIPv4Checksum();

            //tcpHeader.Checksum = v4Header.CalcTcpUdpPseudoChecksum(Unsafe.AsPointer(ref tcpHeader), ip.GetPayloadSize(ref p));
            tcpHeader.Checksum = tcpHeader.CalcTcpUdpPseudoChecksum(ref v4Header, "Hello"._GetBytes_Ascii());

            PacketSpan<VLanHeader> vlan = ip.PrependSpanWithData<VLanHeader>(ref p,
                new VLanHeader()
                {
                    VLanId_EndianSafe = (ushort)1234,
                    Protocol = EthernetProtocolId.IPv4._Endian16(),
                });

            EthernetHeader etherHeaderData = new EthernetHeader()
            {
                Protocol = EthernetProtocolId.VLan._Endian16(),
            };

            etherHeaderData.SrcAddress[0] = 0x00; etherHeaderData.SrcAddress[1] = 0xAC; etherHeaderData.SrcAddress[2] = 0x01;
            etherHeaderData.SrcAddress[3] = 0x23; etherHeaderData.SrcAddress[4] = 0x45; etherHeaderData.SrcAddress[5] = 0x47;

            etherHeaderData.DestAddress[0] = 0x00; etherHeaderData.DestAddress[1] = 0x98; etherHeaderData.DestAddress[2] = 0x21;
            etherHeaderData.DestAddress[3] = 0x33; etherHeaderData.DestAddress[4] = 0x89; etherHeaderData.DestAddress[5] = 0x01;

            PacketSpan<EthernetHeader> ether = vlan.PrependSpanWithData<EthernetHeader>(ref p, in etherHeaderData);

            /*var spanBuffer = PCapUtil.GenerateStandardPCapNgHeader();
            p._PCapEncapsulateEnhancedPacketBlock(0, 0, "Hello");
            spanBuffer.SeekToEnd();
            spanBuffer.Write(p.Span);

            Lfs.WriteDataToFile(@"c:\tmp\190604\test1.pcapng", spanBuffer.Span.ToArray(), FileOperationFlags.AutoCreateDirectory);*/

            using (PCapBuffer pcap = new PCapBuffer(new PCapFileEmitter(new PCapFileEmitterOptions(new FilePath(@"c:\tmp\190607\pcap1.pcapng", flags: FileFlags.AutoCreateDirectory),
                appendMode: false))))
            {
                pcap.WritePacket(ref p, 0, "");
                //pcap.WritePacket(p.Span, 0, "");

                Con.WriteLine($"{p.MemStat_NumRealloc}  {p.MemStat_PreAllocSize}  {p.MemStat_PostAllocSize}");
            }
        }

        public static unsafe void Test__()
        {
            Con.WriteLine(Unsafe.SizeOf<PacketParsed>());

            //var packetMem = Res.AppRoot["190527_novlan_simple_tcp.txt"].HexParsedBinary;
            //var packetMem = Res.AppRoot["190527_novlan_simple_udp.txt"].HexParsedBinary;
            //var packetMem = Res.AppRoot["190527_vlan_simple_tcp.txt"].HexParsedBinary;
            //var packetMem = Res.AppRoot["190527_vlan_simple_udp.txt"].HexParsedBinary;
            //var packetMem = Res.AppRoot["190531_vlan_pppoe_tcp.txt"].HexParsedBinary;
            //var packetMem = Res.AppRoot["190531_vlan_pppoe_udp.txt"].HexParsedBinary;
            var packetMem = Res.AppRoot["190531_vlan_pppoe_l2tp_tcp.txt"].HexParsedBinary;

            Packet packet = new Packet(default, packetMem._CloneSpan());

            PacketParsed parsed = new PacketParsed(ref packet);

            //Con.WriteLine(packet.Parsed.L2_TagVLan1.TagVlan.RefValueRead.VLanId);

            NoOp();
        }
    }
}


