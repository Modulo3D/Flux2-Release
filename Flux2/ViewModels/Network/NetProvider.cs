﻿using DynamicData.Kernel;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using EmbedIO.WebSockets;
using HttpMultipartParser;
using Modulo3DNet;
using ReactiveUI;
using RestSharp;
using Swan.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flux.ViewModels
{
    public class NetworkState
    {
        public bool NetworkAvaiable { get; }
        public bool InternetAvaiable { get; }

        public NetworkState(bool network, bool internet)
        {
            NetworkAvaiable = network;
            InternetAvaiable = internet;
        }
    }

    internal class RemoteControlWebSocket : WebSocketModule
    {
        public FluxViewModel Flux { get; }
        public CompositeDisposable Disposables { get; }

        public RemoteControlWebSocket(FluxViewModel flux, string urlPath) : base(urlPath, true)
        {
            Flux = flux;
            Disposables = new CompositeDisposable();
        }
        private async Task SendRemoteControlDataAsync(Optional<RemoteControlData> rc)
        {
            try
            {
                if (rc.HasValue)
                {
                    var data = JsonUtils.Serialize(rc.Value);
                    foreach (var context in ActiveContexts)
                        await SendRemoteControlDataAsync(context, data);
                }
            }
            catch (Exception)
            {
            }
        }
        public static string Compress(string payload)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(payload);
            using (var ms = new MemoryStream())
            {
                using (var zip = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    zip.Write(buffer, 0, buffer.Length);
                    zip.Close();
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
        private async Task SendRemoteControlDataAsync(IWebSocketContext context, string data)
        {
            var payload = Compress(data);
            await SendAsync(context, payload);
        }
        protected override async Task OnClientConnectedAsync(IWebSocketContext context)
        {
            foreach (var other_context in ActiveContexts)
            {
                if (other_context != context)
                {
                    await other_context.WebSocket.CloseAsync();
                    other_context.WebSocket.Dispose();
                }
            }

            InitializeRemoteView();
            var data = JsonUtils.Serialize(Flux.RemoteControlData);
            await SendRemoteControlDataAsync(context, data);
        }

        private void InitializeRemoteView()
        {
            if (Flux.IsRemoteViewInitialized)
                return;
            Flux.InitializeRemoteView();
            Flux.WhenAnyValue(f => f.RemoteControlData)
                .DistinctUntilChanged()
                .ThrottleMax(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200))
                .Subscribe(async d => await SendRemoteControlDataAsync(d))
                .DisposeWith(Disposables);
        }

        protected override async Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            try
            {
                var remote_request = Encoding.GetString(buffer);
                var remote_data = remote_request.Split("::", StringSplitOptions.RemoveEmptyEntries);
                var control_path = remote_data[0].Split("//", StringSplitOptions.RemoveEmptyEntries).Skip(1);
                var interact_path = remote_data[1].Split("==", StringSplitOptions.RemoveEmptyEntries);

                Optional<IRemoteControl> control_item = Flux;
                foreach (var path in control_path)
                {
                    var path_data = path.Split("??", StringSplitOptions.RemoveEmptyEntries);
                    var control_list_path = path_data[0];
                    var control_item_path = path_data.Length > 1 ? path_data[1] : "";

                    var control_list = control_item.Value.RemoteContents.Lookup(control_list_path);
                    if (!control_list.HasValue)
                        return;

                    control_item = control_list.Value.LookupControl(control_item_path);
                }

                // catch disposed object exception
                try
                {
                    if (interact_path.Length > 1)
                    {
                        var input_name = interact_path[0];
                        var input_value = interact_path[1];
                        var input = control_item.Value.RemoteInputs.Lookup(input_name);
                        if (!input.HasValue)
                            return;

                        var value = input_value.Trim('\'');
                        input.Value.SetValue(value);
                    }
                    else
                    {
                        var command_name = interact_path[0];
                        var command = control_item.Value.RemoteCommands.Lookup(command_name);
                        if (!command.HasValue)
                            return;

                        //await Task.Delay(1000);
                        await command.Value.Execute();
                    }
                }
                catch (Exception)
                {
                }
            }
            catch (Exception ex)
            {
                Flux.Messages.LogException(this, ex);
            }
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                Disposables?.Dispose();
        }
    }

    public class NetProvider : ReactiveObjectRC<NetProvider>, IFluxNetProvider
    {
        public const int WebServerPort = 8080;

        public RestClient Client { get; }
        public FluxViewModel Flux { get; }
        public Ping PLCNetworkPinger { get; }
        public Ping InterNetworkPinger { get; }
        public UdpDiscovery UdpDiscovery { get; }

        private bool _InterNetworkConnectivity;
        public bool InterNetworkConnectivity
        {
            get => _InterNetworkConnectivity;
            set => this.RaiseAndSetIfChanged(ref _InterNetworkConnectivity, value);
        }

        private bool _PLCNetworkConnectivity;
        public bool PLCNetworkConnectivity
        {
            get => _PLCNetworkConnectivity;
            set => this.RaiseAndSetIfChanged(ref _PLCNetworkConnectivity, value);
        }

        public NetProvider(FluxViewModel main)
        {
            Flux = main;
            Client = new RestClient(
                configureSerialization: c => c.UseSerializer<JsonNetRestSerializer>());
            PLCNetworkPinger = new Ping();
            InterNetworkPinger = new Ping();
            UdpDiscovery = new UdpDiscovery();
        }

        public void Initialize()
        {
            InitializeWebServer();
            InitializeUDPDiscovery();

            DisposableThread.Start(PingPLCNetworkAsync, TimeSpan.FromSeconds(5));
            DisposableThread.Start(PingInterNetworkAsync, TimeSpan.FromSeconds(10));
        }

        private async Task PingPLCNetworkAsync()
        {
            var core_settings = Flux.SettingsProvider.CoreSettings.Local;
            PLCNetworkConnectivity = await PLCNetworkPinger.PingAsync(
                    core_settings.PLCAddress,
                    TimeSpan.FromSeconds(5));
        }
        private async Task PingInterNetworkAsync()
        {
            InterNetworkConnectivity = await InterNetworkPinger.PingAsync(
                "8.8.8.8",
                TimeSpan.FromSeconds(10));
        }

        private void InitializeWebServer()
        {
            Logger.UnregisterLogger<ConsoleLogger>();
            var server = new WebServer(WebServerPort)
                .WithLocalSessionManager()
                .WithModule(new RemoteControlWebSocket(Flux, "/remote"))
                .WithAction("/", HttpVerbs.Options, e =>
                {
                    e.Response.Headers.Add("Interface-Type", "Flux");
                    return Task.CompletedTask;
                })
                .WithWebApi("/plc", (c, d) => WebServerUtils.SerializeData(c, d), m => m.WithController(() => new PlcWebApiController(Flux)))
                .WithWebApi("/api", (c, d) => WebServerUtils.SerializeData(c, d), m => m.WithController(() => new FluxWebApiController(Flux)))
                .WithWebApi("/settings/user", Flux.SettingsProvider.UserSettings, user =>
                {
                    user
                        .WithWebApiSetting(s => s.PrinterName)
                        .WithWebApiSetting(s => s.CostHour);
                })
                .WithWebApi("/settings/core", Flux.SettingsProvider.CoreSettings, core =>
                {
                    core
                        .WithWebApiReadSetting(s => s.PrinterID)
                        .WithWebApiReadSetting(s => s.PrinterGuid);
                })
                .WithWebApi("/settings/stats", Flux.StatsProvider.Stats, stats =>
                {
                    stats
                        .WithWebApiReadSetting(s => s.UsedPrintAreas.Items);
                })
                .WithWebApi("/settings/feeders", (IFluxFeedersViewModel)Flux.Feeders, settings =>
                {
                    settings
                        .WithWebApiReadSetting(s => s.Materials.KeyValues)
                        .WithWebApiReadSetting(s => s.Nozzles.KeyValues)
                        .WithWebApiReadSetting(s => s.Tools.KeyValues);
                })
                .WithWebApi("/settings/status", (IFluxStatusProvider)Flux.StatusProvider, settings =>
                {
                    settings
                        .WithWebApiReadSetting(s => s.FluxStatus)
                        .WithWebApiReadSetting(s => s.PrintProgress)
                        .WithWebApiReadSetting(s => s.StartEvaluation)
                        .WithWebApiReadSetting(s => s.StatusEvaluation)
                        .WithWebApiReadSetting(s => s.PrintingEvaluation);
                });
            server.Start();
        }
        private void InitializeUDPDiscovery()
        {
            try
            {
                UdpDiscovery.StartSending(() =>
                {
                    var address = Flux.SettingsProvider.HostAddress;
                    if (address.HasValue)
                        return new IPEndPoint(address.Value, WebServerPort);
                    return new IPEndPoint(IPAddress.Loopback, WebServerPort);
                }, TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Flux.Messages.LogMessage(NetResponse.UDP_DISCOVERY_EXCEPTION, ex);
            }
        }
    }
    public class FluxWebApiController : WebApiController
    {
        public FluxViewModel Flux { get; }
        public FluxWebApiController(FluxViewModel flux)
        {
            Flux = flux;
        }

        [Route(HttpVerbs.Put, "/mcode")]
        public async Task<bool> PutMCode()
        {
            try
            {
                var parser = await MultipartFormDataParser.ParseAsync(Request.InputStream);

                var mcode_file = Path.GetFileName(parser.Files[0].FileName);
                var mcode_name = Path.GetFileNameWithoutExtension(mcode_file);
                if (!MCodeKey.TryParse(mcode_name, out var mcode_key))
                    return false;

                var mcode_file_storage = Files.AccessFile(Directories.MCodes, mcode_file);
                if (mcode_file_storage.Exists)
                    mcode_file_storage.Delete();

                using (var source_stream = mcode_file_storage.Create())
                    await parser.Files[0].Data.CopyToAsync(source_stream);

                await Flux.MCodes.ImportMCodesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogMessage(NetResponse.PUT_MCODE_EXCEPTION, ex);
                return false;
            }
        }
        [Route(HttpVerbs.Put, "/database")]
        public async Task<bool> PutDatabase()
        {
            try
            {
                var parser = await MultipartFormDataParser.ParseAsync(Request.InputStream);

                if (!Flux.DatabaseProvider.Database.HasValue)
                {
                    await receive_database_async(parser.Files[0].Data, Files.Database.FullName);
                }
                else
                {
                    await Flux.DatabaseProvider.Database.Value.AccessLocalDatabaseAsync(db => receive_database_async(parser.Files[0].Data, db.Filename));
                }

                await Flux.DatabaseProvider.InitializeAsync();

                return true;
            }
            catch (Exception ex)
            {
                Flux.Messages.LogMessage(NetResponse.PUT_MCODE_EXCEPTION, ex);
                return false;
            }

            static async Task receive_database_async(Stream stream, string database_connection)
            {
                if (File.Exists(database_connection))
                    File.Delete(database_connection);

                using (var source_stream = File.Create(database_connection))
                    await stream.CopyToAsync(source_stream);
            }
        }
        [Route(HttpVerbs.Get, "/memory")]
        public async Task GetMemory()
        {
            var variables = Flux.ConnectionProvider.VariableStoreBase.Variables;
            var full_variables = variables.SelectMany(v =>
            {
                switch (v.Value)
                {
                    case IFLUX_Variable pvar:
                        return new[] { pvar };
                    case IFLUX_Array parr:
                        return parr.Variables.Items;
                    default:
                        return new IFLUX_Variable[0];
                }
            });

            var sb = new StringBuilder();
            var text_format = "{0, -35} {1,-20} {2,-10}";
            foreach (var variable in full_variables)
                sb.AppendLine(string.Format(text_format, variable.Name, variable.Unit, variable.IValue));

            await HttpContext.SendStringAsync(sb.ToString(), "text/plain", Encoding.UTF8);
        }
    }
    public class PlcWebApiController : WebApiController
    {
        public FluxViewModel Flux { get; }
        public PlcWebApiController(FluxViewModel flux)
        {
            Flux = flux;
        }

        [Route(HttpVerbs.Get, "/download")]
        public Task<Optional<string>> GetFileAsync([QueryField] string folder, [QueryField] string name) => Flux.ConnectionProvider.GetFileAsync(folder, name, HttpContext.CancellationToken);

        /*[Route(HttpVerbs.Post, "/upload")]
        public Task<bool> PutFileAsync([QueryField] string folder, [QueryField] string name) => Flux.ConnectionProvider.PutFileAsync(folder, name, HttpContext.GetRequestBodyAsStringAsync(), HttpContext.CancellationToken);*/

        [Route(HttpVerbs.Get, "/delete")]
        public Task<bool> DeleteAsync([QueryField] string folder, [QueryField] string name) => Flux.ConnectionProvider.DeleteAsync(folder, name, HttpContext.CancellationToken);

        [Route(HttpVerbs.Get, "/rename")]
        public Task<bool> RenameAsync([QueryField] string folder, [QueryField] string old_name, [QueryField] string new_name) => Flux.ConnectionProvider.RenameAsync(folder, old_name, new_name, HttpContext.CancellationToken);


        [Route(HttpVerbs.Get, "/list")]
        public Task<Optional<FLUX_FileList>> ListFilesAsync([QueryField] string folder) => Flux.ConnectionProvider.ListFilesAsync(folder, HttpContext.CancellationToken);

        [Route(HttpVerbs.Get, "/clear")]
        public Task<bool> ClearFolderAsync([QueryField] string folder) => Flux.ConnectionProvider.ClearFolderAsync(folder, HttpContext.CancellationToken);

        [Route(HttpVerbs.Get, "/create")]
        public Task<bool> CreateFolder([QueryField] string folder, [QueryField] string name) => Flux.ConnectionProvider.CreateFolderAsync(folder, name, HttpContext.CancellationToken);

    }
}
