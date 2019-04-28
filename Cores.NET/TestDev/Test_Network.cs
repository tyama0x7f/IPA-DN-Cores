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

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using System.Diagnostics;

#pragma warning disable CS0162
#pragma warning disable CS0219

namespace IPA.TestDev
{
    partial class TestDevCommands
    {
        [ConsoleCommandMethod(
            "Net command",
            "Net [arg]",
            "Net test")]
        static int Net(ConsoleService c, string cmdName, string str)
        {
            ConsoleParam[] args = { };
            ConsoleParamValueList vl = c.ParseCommandList(cmdName, str, args);

            //Net_Test1_PlainTcp_Client();

            //Net_Test2_Ssl_Client();

            while (true)
            Net_Test3_PlainTcp_Server();

            return 0;
        }

        static void Net_Test3_PlainTcp_Server()
        {
            using (AsyncCleanuperLady lady = new AsyncCleanuperLady())
            {
                var listener = LocalNet.CreateListener(lady, new TcpListenParam(
                    async (listener2, sock) =>
                    {
                        var stream = sock.GetStream().NetworkStream;
                        StreamWriter w = new StreamWriter(stream);
                        while (true)
                        {
                            w.WriteLine(DateTimeOffset.Now.ToDtStr(true));
                            await w.FlushAsync();
                            await Task.Delay(100);
                        }
                    },
                    9821));

                Con.ReadLine(">");
            }
        }

        static void Net_Test2_Ssl_Client()
        {
            string hostname = "www.google.co.jp";

            using (TcpSock sock = LocalNet.Connect(new TcpConnectParam(hostname, 443)))
            {
                using (SslSock ssl = new SslSock(sock))
                {
                    var sslClientOptions = new PalSslClientAuthenticationOptions()
                    {
                        TargetHost = hostname,
                        ValidateRemoteCertificateProc = (cert) => { return true; },
                    };

                    ssl.StartSslClient(sslClientOptions);

                    var st = ssl.GetStream().NetworkStream;

                    var w = new StreamWriter(st);
                    var r = new StreamReader(st);

                    w.WriteLine("GET / HTTP/1.0");
                    w.WriteLine($"HOST: {hostname}");
                    w.WriteLine();
                    w.WriteLine();
                    w.Flush();

                    while (true)
                    {
                        string s = r.ReadLine();
                        if (s == null)
                        {
                            break;
                        }

                        Con.WriteLine(s);
                    }

                    sock.Disconnect();
                }
            }
        }

        static void Net_Test1_PlainTcp_Client()
        {
            while (true)
            {
                TcpSock sock = LocalNet.Connect(new TcpConnectParam("dnobori.cs.tsukuba.ac.jp", 80));
                {
                    var st = sock.GetStream().NetworkStream;

                    var w = new StreamWriter(st);
                    var r = new StreamReader(st);

                    w.WriteLine("GET / HTTP/1.0");
                    w.WriteLine("HOST: dnobori.cs.tsukuba.ac.jp");
                    w.WriteLine();
                    w.WriteLine();
                    w.Flush();

                    while (true)
                    {
                        string s = r.ReadLine();
                        if (s == null)
                        {
                            break;
                        }

                        Con.WriteLine(s);
                    }

                    sock.Disconnect();
                }
            }
        }
    }
}
