﻿// IPA Cores.NET
// 
// Copyright (c) 2019- IPA CyberLab.
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

// Author: Daiyuu Nobori
// Description

#if CORES_CODES_THINCONTROLLER

using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

namespace IPA.Cores.Codes
{
    // ThinController 設定
    [Serializable]
    public sealed class ThinControllerSettings : INormalizable
    {
        public List<string> WpcPathList = new List<string>();

        public ThinControllerSettings()
        {
        }

        public void Normalize()
        {
            if (this.WpcPathList._IsEmpty())
            {
                this.WpcPathList = new List<string>();

                this.WpcPathList.Add("/widecontrol/");
            }
        }
    }

    public class ThinController : AsyncService
    {
        // Hive
        readonly HiveData<ThinControllerSettings> SettingsHive;

        // 設定へのアクセスを容易にするための自動プロパティ
        CriticalSection ManagedSettingsLock => SettingsHive.DataLock;
        ThinControllerSettings ManagedSettings => SettingsHive.ManagedData;

        public ThinControllerSettings SettingsFastSnapshot => SettingsHive.CachedFastSnapshot;

        public ThinController(ThinControllerSettings settings, Func<ThinControllerSettings>? getDefaultSettings =null)
        {
            try
            {
                this.SettingsHive = new HiveData<ThinControllerSettings>(
                    Hive.SharedLocalConfigHive,
                    "ThinController",
                    getDefaultSettings,
                    HiveSyncPolicy.AutoReadWriteFile,
                    HiveSerializerSelection.RichJson);
            }
            catch (Exception ex)
            {
                this._DisposeSafe(ex);
                throw;
            }
        }

        public async Task HandleWpcAsync(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            ConnectionInfo connInfo = context.Connection;
            WebMethods method = request.Method._ParseEnum(WebMethods.GET);
            CancellationToken cancel = request._GetRequestCancellationToken();
            IPEndPoint remote = new IPEndPoint(connInfo.RemoteIpAddress!._UnmapIPv4(), connInfo.RemotePort);
            IPEndPoint local = new IPEndPoint(connInfo.LocalIpAddress!._UnmapIPv4(), connInfo.LocalPort);

            string replyBody = "Hello World\r\n";
            context.Response.StatusCode = 200;

            await context.Response.WriteAsync(replyBody, context.RequestAborted);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Use((context, next) =>
            {
                string path = context.Request.Path.Value._NonNullTrim();

                if (this.SettingsFastSnapshot.WpcPathList.Where(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)).Any())
                {
                    return HandleWpcAsync(context);
                }

                return next();
            });
        }

        protected override async Task CleanupImplAsync(Exception? ex)
        {
            try
            {
                this.SettingsHive._DisposeSafe();
            }
            finally
            {
                await base.CleanupImplAsync(ex);
            }
        }
    }
}

#endif

