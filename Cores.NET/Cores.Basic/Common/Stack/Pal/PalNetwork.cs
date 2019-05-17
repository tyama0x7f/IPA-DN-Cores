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
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Net.NetworkInformation;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using System.Collections.Immutable;

namespace IPA.Cores.Basic
{
    class PalX509Certificate
    {
        public X509Certificate NativeCertificate { get; }

        public override string ToString() => this.ToString(false);

        public string ToString(bool details) => this.NativeCertificate.ToString(details);

        public string HashSHA1 => this.NativeCertificate.GetCertHashString();

        public PalX509Certificate(X509Certificate nativeCertificate)
        {
            NativeCertificate = nativeCertificate;
        }

        public PalX509Certificate(ReadOnlySpan<byte> pkcs12Data, string password = null)
        {
            NativeCertificate = Secure.LoadPkcs12(pkcs12Data.ToArray(), password);
        }

        public PalX509Certificate(FilePath filePath, string password = null)
            : this(filePath.EasyAccess.Binary.Span, password) { }
    }

    struct PalSocketReceiveFromResult
    {
        public int ReceivedBytes;
        public EndPoint RemoteEndPoint;
    }

    class PalSocket : IDisposable
    {
        public static bool OSSupportsIPv4 { get => Socket.OSSupportsIPv4; }
        public static bool OSSupportsIPv6 { get => Socket.OSSupportsIPv6; }

        Socket _Socket;

        public AddressFamily AddressFamily { get; }
        public SocketType SocketType { get; }
        public ProtocolType ProtocolType { get; }

        CriticalSection LockObj = new CriticalSection();

        public CachedProperty<bool> NoDelay { get; }
        public CachedProperty<int> LingerTime { get; }
        public CachedProperty<int> SendBufferSize { get; }
        public CachedProperty<int> ReceiveBufferSize { get; }
        public long NativeHandle { get; }

        public CachedProperty<EndPoint> LocalEndPoint { get; }
        public CachedProperty<EndPoint> RemoteEndPoint { get; }

        public TcpDirectionType Direction { get; }

        IHolder Leak;

        public PalSocket(Socket s, TcpDirectionType direction)
        {
            _Socket = s;

            AddressFamily = _Socket.AddressFamily;
            SocketType = _Socket.SocketType;
            ProtocolType = _Socket.ProtocolType;

            Direction = direction;

            NoDelay = new CachedProperty<bool>(value => _Socket.NoDelay = value, () => _Socket.NoDelay);
            LingerTime = new CachedProperty<int>(value =>
            {
                if (value <= 0) value = 0;
                if (value == 0)
                    _Socket.LingerState = new LingerOption(false, 0);
                else
                    _Socket.LingerState = new LingerOption(true, value);

                try
                {
                    if (value == 0)
                        _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                    else
                        _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, false);
                }
                catch { }

                return value;
            }, () =>
            {
                var lingerOption = _Socket.LingerState;
                if (lingerOption == null || lingerOption.Enabled == false)
                    return 0;
                else
                    return lingerOption.LingerTime;
            });
            SendBufferSize = new CachedProperty<int>(value => _Socket.SendBufferSize = value, () => _Socket.SendBufferSize);
            ReceiveBufferSize = new CachedProperty<int>(value => _Socket.ReceiveBufferSize = value, () => _Socket.ReceiveBufferSize);
            LocalEndPoint = new CachedProperty<EndPoint>(null, () => _Socket.LocalEndPoint);
            RemoteEndPoint = new CachedProperty<EndPoint>(null, () => _Socket.RemoteEndPoint);

            NativeHandle = _Socket.Handle.ToInt64();

            Leak = LeakChecker.Enter(LeakCounterKind.PalSocket);
        }

        public PalSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, TcpDirectionType direction)
            : this(new Socket(addressFamily, socketType, protocolType), direction) { }

        public Task ConnectAsync(IPAddress address, int port) => ConnectAsync(new IPEndPoint(address, port));

        public async Task ConnectAsync(EndPoint remoteEP)
        {
            await _Socket.ConnectAsync(remoteEP);

            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public void Connect(EndPoint remoteEP) => _Socket.Connect(remoteEP);

        public void Connect(IPAddress address, int port) => _Socket.Connect(address, port);

        public void Bind(EndPoint localEP)
        {
            _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            _Socket.Bind(localEP);
            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public void Listen(int backlog = int.MaxValue)
        {
            _Socket.Listen(backlog);
            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public async Task<PalSocket> AcceptAsync()
        {
            Socket newSocket = await _Socket.AcceptAsync();
            return new PalSocket(newSocket, TcpDirectionType.Server);
        }

        public Task<int> SendAsync(IEnumerable<ReadOnlyMemory<byte>> buffers)
        {
            List<ArraySegment<byte>> sendArraySegmentsList = new List<ArraySegment<byte>>();
            foreach (ReadOnlyMemory<byte> mem in buffers)
                sendArraySegmentsList.Add(mem._AsSegment());

            return _Socket.SendAsync(sendArraySegmentsList, SocketFlags.None);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> buffer)
        {
            return await _Socket.SendAsync(buffer, SocketFlags.None);
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer)
        {
            return await _Socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public async Task<int> SendToAsync(Memory<byte> buffer, EndPoint remoteEP)
        {
            try
            {
                Task<int> t = _Socket.SendToAsync(buffer._AsSegment(), SocketFlags.None, remoteEP);
                if (t.IsCompleted == false)
                    await t;
                int ret = t._GetResult();
                if (ret <= 0) throw new SocketDisconnectedException();
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                return buffer.Length;
            }
        }

        static readonly IPEndPoint StaticUdpEndPointIPv4 = new IPEndPoint(IPAddress.Any, 0);
        static readonly IPEndPoint StaticUdpEndPointIPv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
        const int UdpMaxRetryOnIgnoreError = 1000;

        public async Task<PalSocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer)
        {
            int numRetry = 0;

            var bufferSegment = buffer._AsSegment();

            LABEL_RETRY:

            try
            {
                Task<SocketReceiveFromResult> t = _Socket.ReceiveFromAsync(bufferSegment, SocketFlags.None,
                    this.AddressFamily == AddressFamily.InterNetworkV6 ? StaticUdpEndPointIPv6 : StaticUdpEndPointIPv4);
                if (t.IsCompleted == false)
                {
                    numRetry = 0;
                    await t;
                }
                SocketReceiveFromResult ret = t._GetResult();
                if (ret.ReceivedBytes <= 0) throw new SocketDisconnectedException();
                return new PalSocketReceiveFromResult()
                {
                    ReceivedBytes = ret.ReceivedBytes,
                    RemoteEndPoint = ret.RemoteEndPoint,
                };
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e) || _Socket.Available >= 1)
            {
                numRetry++;
                if (numRetry >= UdpMaxRetryOnIgnoreError)
                {
                    throw;
                }
                await Task.Yield();
                goto LABEL_RETRY;
            }
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                _Socket._DisposeSafe();

                Leak._DisposeSafe();
            }
        }

        public static bool CanUdpSocketErrorBeIgnored(SocketException e)
        {
            switch (e.SocketErrorCode)
            {
                case SocketError.ConnectionReset:
                case SocketError.NetworkReset:
                case SocketError.MessageSize:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                case SocketError.NoBufferSpaceAvailable:
                case SocketError.AddressNotAvailable:
                case SocketError.ConnectionRefused:
                case SocketError.Interrupted:
                case SocketError.WouldBlock:
                case SocketError.TryAgain:
                case SocketError.InProgress:
                case SocketError.InvalidArgument:
                case (SocketError)12: // ENOMEM
                case (SocketError)10068: // WSAEUSERS
                    return true;
            }
            return false;
        }
    }

    class PalStream : StreamImplBase
    {
        protected Stream NativeStream;
        protected NetworkStream NativeNetworkStream;

        public bool IsNetworkStream => (NativeNetworkStream != null);

        public PalStream(Stream nativeStream)
        {
            NativeStream = nativeStream;

            NativeNetworkStream = NativeStream as NetworkStream;
        }

        protected override ValueTask<int> ReadImplAsync(Memory<byte> buffer, CancellationToken cancel = default)
            => NativeStream.ReadAsync(buffer, cancel);

        protected override ValueTask WriteImplAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
            => NativeStream.WriteAsync(buffer, cancel);

        Once DisposeFlag;

        public override int ReadTimeout { get => NativeStream.ReadTimeout; set => NativeStream.ReadTimeout = value; }
        public override int WriteTimeout { get => NativeStream.WriteTimeout; set => NativeStream.WriteTimeout = value; }
        public override bool DataAvailable => NativeNetworkStream?.DataAvailable ?? true;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                NativeStream._DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }

        protected override Task FlushImplAsync(CancellationToken cancel = default) => NativeStream.FlushAsync(cancel);
    }

    delegate bool PalSslValidateRemoteCertificateCallback(PalX509Certificate cert);

    delegate PalX509Certificate PalSslCertificateSelectionCallback(object param, string sniHostName);

    class PalSslClientAuthenticationOptions
    {
        public PalSslClientAuthenticationOptions() { }

        public string TargetHost { get; set; }
        public PalSslValidateRemoteCertificateCallback ValidateRemoteCertificateProc { get; set; }
        public string[] ServerCertSHA1List { get; set; } = new string[0];
        public bool AllowAnyServerCert { get; set; } = false;

        public PalX509Certificate ClientCertificate { get; set; }

        public SslClientAuthenticationOptions GetNativeOptions()
        {
            SslClientAuthenticationOptions ret = new SslClientAuthenticationOptions()
            {
                TargetHost = TargetHost,
                AllowRenegotiation = true,
                RemoteCertificateValidationCallback = new RemoteCertificateValidationCallback((sender, cert, chain, err) =>
                {
                    string sha1 = cert.GetCertHashString();

                    bool b1 = (ValidateRemoteCertificateProc != null ? ValidateRemoteCertificateProc(new PalX509Certificate(cert)) : false);
                    bool b2 = ServerCertSHA1List?.Where(x => x._IsSamei(sha1)).Any() ?? false;
                    bool b3 = this.AllowAnyServerCert;

                    return b1 || b2 || b3;
                }),
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            };

            if (this.ClientCertificate != null)
                ret.ClientCertificates.Add(this.ClientCertificate.NativeCertificate);

            return ret;
        }
    }

    class PalSslServerAuthenticationOptions
    {
        public PalSslServerAuthenticationOptions() { }

        public PalSslValidateRemoteCertificateCallback ValidateRemoteCertificateProc { get; set; }
        public string[] ClientCertSHA1List { get; set; } = new string[0];
        public bool AllowAnyClientCert { get; set; } = true;

        public PalSslCertificateSelectionCallback CertificateSelectionProc { get; set; }
        public object CertificateSelectionProcParam { get; set; }

        public PalX509Certificate Certificate { get; set; }

        public SslServerAuthenticationOptions GetNativeOptions()
        {
            SslServerAuthenticationOptions ret = new SslServerAuthenticationOptions()
            {
                AllowRenegotiation = true,
                RemoteCertificateValidationCallback = new RemoteCertificateValidationCallback((sender, cert, chain, err) =>
                {
                    string sha1 = cert.GetCertHashString();

                    bool b1 = (ValidateRemoteCertificateProc != null ? ValidateRemoteCertificateProc(new PalX509Certificate(cert)) : false);
                    bool b2 = ClientCertSHA1List?.Where(x => x._IsSamei(sha1)).Any() ?? false;
                    bool b3 = this.AllowAnyClientCert;

                    return b1 || b2 || b3;
                }),
                ClientCertificateRequired = !AllowAnyClientCert,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            };

            bool certExists = false;

            if (this.CertificateSelectionProc != null)
            {
                object param = this.CertificateSelectionProcParam;
                ret.ServerCertificateSelectionCallback = (obj, sniHostName) =>
                {
                    PalX509Certificate cert = this.CertificateSelectionProc(param, sniHostName);
                    return cert.NativeCertificate;
                };

                certExists = true;
            }

            if (this.Certificate != null)
            {
                ret.ServerCertificateSelectionCallback = (obj, sniHostName) =>
                {
                    return this.Certificate.NativeCertificate;
                };

                certExists = true;
            }

            if (certExists == false)
                throw new ApplicationException("CertificateSelectionProc or Certificate must be specified.");

            return ret;
        }
    }

    class PalSslStream : PalStream
    {
        SslStream Ssl;
        public PalSslStream(Stream innerStream) : base(new SslStream(innerStream, true))
        {
            this.Ssl = (SslStream)NativeStream;
        }

        public Task AuthenticateAsClientAsync(PalSslClientAuthenticationOptions sslClientAuthenticationOptions, CancellationToken cancellationToken)
            => Ssl.AuthenticateAsClientAsync(sslClientAuthenticationOptions.GetNativeOptions(), cancellationToken);

        public Task AuthenticateAsServerAsync(PalSslServerAuthenticationOptions sslServerAuthenticationOptions, CancellationToken cancellationToken)
            => Ssl.AuthenticateAsServerAsync(sslServerAuthenticationOptions.GetNativeOptions(), cancellationToken);

        public string SslProtocol => Ssl.SslProtocol.ToString();
        public string CipherAlgorithm => Ssl.CipherAlgorithm.ToString();
        public int CipherStrength => Ssl.CipherStrength;
        public string HashAlgorithm => Ssl.HashAlgorithm.ToString();
        public int HashStrength => Ssl.HashStrength;
        public string KeyExchangeAlgorithm => Ssl.KeyExchangeAlgorithm.ToString();
        public int KeyExchangeStrength => Ssl.KeyExchangeStrength;
        public PalX509Certificate LocalCertificate => new PalX509Certificate(Ssl.LocalCertificate);
        public PalX509Certificate RemoteCertificate => new PalX509Certificate(Ssl.RemoteCertificate);

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                this.Ssl._DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    static class PalDns
    {
        public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, int timeout = Timeout.Infinite, CancellationToken cancel = default)
            => TaskUtil.DoAsyncWithTimeout(c => Dns.GetHostAddressesAsync(hostNameOrAddress),
                timeout: timeout, cancel: cancel);

        public static Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress, int timeout = Timeout.Infinite, CancellationToken cancel = default)
            => TaskUtil.DoAsyncWithTimeout(c => Dns.GetHostEntryAsync(hostNameOrAddress),
                timeout: timeout, cancel: cancel);
    }

    class PalHostNetInfo : BackgroundStateDataBase
    {
        public override BackgroundStateDataUpdatePolicy DataUpdatePolicy =>
            new BackgroundStateDataUpdatePolicy(300, 6000, 2000);

        public string HostName;
        public string DomainName;
        public string FqdnHostName => HostName + (string.IsNullOrEmpty(DomainName) ? "" : "." + DomainName);
        public bool IsIPv4Supported;
        public bool IsIPv6Supported;
        public IReadOnlyList<IPAddress> IPAddressList = null;

        public static bool IsUnix { get; } = (Environment.OSVersion.Platform != PlatformID.Win32NT);

        static IPAddress[] GetLocalIPAddressBySocketApi() => PalDns.GetHostAddressesAsync(Dns.GetHostName())._GetResult();

        class ByteComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
        }

        public PalHostNetInfo()
        {
            IPGlobalProperties prop = IPGlobalProperties.GetIPGlobalProperties();
            this.HostName = prop.HostName;
            this.DomainName = prop.DomainName;
            HashSet<IPAddress> hash = new HashSet<IPAddress>();

            if (IsUnix)
            {
                UnicastIPAddressInformationCollection info = prop.GetUnicastAddresses();
                foreach (UnicastIPAddressInformation ip in info)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork || ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        hash.Add(ip.Address);
                }
            }
            else
            {
                try
                {
                    IPAddress[] info = GetLocalIPAddressBySocketApi();
                    if (info.Length >= 1)
                    {
                        foreach (IPAddress ip in info)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                                hash.Add(ip);
                        }
                    }
                }
                catch { }
            }

            if (PalSocket.OSSupportsIPv4)
            {
                this.IsIPv4Supported = true;
                hash.Add(IPAddress.Any);
                hash.Add(IPAddress.Loopback);
            }
            if (PalSocket.OSSupportsIPv6)
            {
                this.IsIPv6Supported = true;
                hash.Add(IPAddress.IPv6Any);
                hash.Add(IPAddress.IPv6Loopback);
            }

            try
            {
                var cmp = new ByteComparer();
                this.IPAddressList = hash.OrderBy(x => x.AddressFamily)
                    .ThenBy(x => x.GetAddressBytes(), cmp)
                    .ThenBy(x => (x.AddressFamily == AddressFamily.InterNetworkV6 ? x.ScopeId : 0))
                    .ToList();
            }
            catch { }
        }

        public Memory<byte> IPAddressListBinary
        {
            get
            {
                FastMemoryBuffer<byte> ret = new FastMemoryBuffer<byte>();
                foreach (IPAddress addr in IPAddressList)
                {
                    ret.WriteSInt32((int)addr.AddressFamily);
                    ret.Write(addr.GetAddressBytes());
                    if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                        ret.WriteSInt64(addr.ScopeId);
                }
                return ret;
            }
        }

        public override bool Equals(BackgroundStateDataBase otherArg)
        {
            PalHostNetInfo other = otherArg as PalHostNetInfo;
            if (string.Equals(this.HostName, other.HostName) == false) return false;
            if (string.Equals(this.DomainName, other.DomainName) == false) return false;
            if (this.IsIPv4Supported != other.IsIPv4Supported) return false;
            if (this.IsIPv6Supported != other.IsIPv6Supported) return false;
            if (this.IPAddressListBinary.Span.SequenceEqual(other.IPAddressListBinary.Span) == false) return false;
            return true;
        }

        Action callMeCache = null;

        public override void RegisterSystemStateChangeNotificationCallbackOnlyOnceImpl(Action callMe)
        {
            callMeCache = callMe;

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            callMeCache();

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            callMeCache();

            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }

        public static IPAddress GetLocalIPForDestinationHost(IPAddress dest)
        {
            try
            {
                using (PalSocket sock = new PalSocket(dest.AddressFamily, SocketType.Dgram, ProtocolType.IP, TcpDirectionType.Client))
                {
                    sock.Connect(dest, 65530);
                    IPEndPoint ep = sock.LocalEndPoint.Value as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            using (PalSocket sock = new PalSocket(dest.AddressFamily, SocketType.Dgram, ProtocolType.Udp, TcpDirectionType.Unknown))
            {
                sock.Connect(dest, 65531);
                IPEndPoint ep = sock.LocalEndPoint.Value as IPEndPoint;
                return ep.Address;
            }
        }

        public static async Task<IPAddress> GetLocalIPv4ForInternetAsync()
        {
            try
            {
                return GetLocalIPForDestinationHost(IPAddress.Parse("8.8.8.8"));
            }
            catch { }

            try
            {
                using (PalSocket sock = new PalSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, TcpDirectionType.Client))
                {
                    var hostent = await PalDns.GetHostEntryAsync("www.msftncsi.com");
                    var addr = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
                    await sock.ConnectAsync(addr, 443);
                    IPEndPoint ep = sock.LocalEndPoint.Value as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            try
            {
                using (PalSocket sock = new PalSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, TcpDirectionType.Client))
                {
                    var hostent = await PalDns.GetHostEntryAsync("www.msftncsi.com");
                    var addr = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
                    await sock.ConnectAsync(addr, 80);
                    IPEndPoint ep = sock.LocalEndPoint.Value as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            try
            {
                return BackgroundState<PalHostNetInfo>.Current.Data.IPAddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                    .Where(x => IPAddress.IsLoopback(x) == false).Where(x => x != IPAddress.Any).First();
            }
            catch { }

            return IPAddress.Any;
        }

    }
}


