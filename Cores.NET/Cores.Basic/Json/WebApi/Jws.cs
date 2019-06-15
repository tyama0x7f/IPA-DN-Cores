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

#if CORES_BASIC_JSON
#if CORES_BASIC_SECURITY

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.IO;

using Newtonsoft.Json;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;
using Org.BouncyCastle.Asn1.Pkcs;

namespace IPA.Cores.Basic
{
    class JwsPacket
    {
        [JsonProperty("protected")]
        public string Protected;

        public string payload;

        public string signature;
    }

    class JwsRsaJwk
    {
        public string kty;
        public string crv;
        public string x;
        public string y;
        public string n;
        public string e;
    }

    class JwsProtected
    {
        public string alg;
        public JwsRsaJwk jwk;
        public string nonce;
        public string url;
        public string kid;
    }

    static class JwsUtil
    {
        public static JwsPacket Encapsulate(PrivKey key, string nonce, string url, object payload)
        {
            JwsRsaJwk jwk;
            string algName;
            string signerName;

            switch (key.Algorithm)
            {
                case PkiAlgorithm.ECDSA:
                    jwk = new JwsRsaJwk()
                    {
                        kty = "EC",
                        crv = "P-" + key.PublicKey.BitsSize,
                        x = key.PublicKey.EcdsaParameters.Q.AffineXCoord.GetEncoded()._Base64UrlEncode(),
                        y = key.PublicKey.EcdsaParameters.Q.AffineYCoord.GetEncoded()._Base64UrlEncode(),
                    };

                    switch (key.BitsSize)
                    {
                        case 256:
                            algName = "ES256";
                            signerName = "SHA-256withPLAIN-ECDSA";
                            break;

                        case 384:
                            algName = "ES384";
                            signerName = "SHA-384withPLAIN-ECDSA";
                            break;

                        default:
                            throw new ArgumentException("Unsupported key length.");
                    }

                    break;

                case PkiAlgorithm.RSA:
                    jwk = new JwsRsaJwk()
                    {
                        kty = "RSA",
                        n = key.PublicKey.RsaParameters.Modulus.ToByteArray()._Base64UrlEncode(),
                        e = key.PublicKey.RsaParameters.Exponent.ToByteArray()._Base64UrlEncode(),
                    };

                    algName = "RS256";
                    signerName = PkcsObjectIdentifiers.Sha256WithRsaEncryption.Id;
                    break;

                default:
                    throw new ArgumentException("Unsupported key.Algorithm.");
            }

            JwsProtected protect = new JwsProtected()
            {
                alg = algName,
                jwk = jwk,
                nonce = nonce,
                url = url,
            };

            protect._PrintAsJson(includeNull: true);

            payload._PrintAsJson();

            JwsPacket ret = new JwsPacket()
            {
                Protected = protect._ObjectToJson(base64url: true, includeNull: true),
                payload = payload._ObjectToJson(base64url: true),
            };

            var signer = key.GetSigner(signerName);

            byte[] signature = signer.Sign((ret.Protected + "." + ret.payload)._GetBytes_Ascii());

            ret.signature = signature._Base64UrlEncode();

            return ret;
        }
    }

    partial class WebApi
    {
        public virtual async Task<WebRet> RequestWithJwsObject(WebApiMethods method, PrivKey privKey, string nonce, string url, object payload, CancellationToken cancel = default, string postContentType = Consts.MediaTypes.Json)
        {
            JwsPacket reqPacket = JwsUtil.Encapsulate(privKey, nonce, url, payload);

            reqPacket._PrintAsJson();

            return await this.RequestWithJsonObject(method, url, reqPacket, cancel, postContentType);
        }
    }
}

#endif  // CORES_BASIC_SECURITY
#endif  // CORES_BASIC_JSON

