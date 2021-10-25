using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Services;
using Microsoft.Python.LanguageServer.Controllers;
using Microsoft.Python.LanguageServer.Services;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Server
{
    public class RestScope
    {
        public static IServiceManager Services { get; set; }

        public static void RunInScope(string[] args) 
        {
            using (CoreShell.Create()) {
                Services = CoreShell.Current.ServiceManager;
                var clientApp = new ClientApplicationRest();

                var osp = new OSPlatform();
                Services
                    .AddService(clientApp)
                    .AddService(new Logger(clientApp))
                    .AddService(new UIService(clientApp))
                    .AddService(new ProgressService(clientApp))
                    .AddService(new TelemetryService(clientApp))
                    .AddService(new IdleTimeService())
                    .AddService(osp)
                    .AddService(new ProcessServices())
                    .AddService(new FileSystem(osp));

                var messageFormatter = new JsonMessageFormatter();
                // StreamJsonRpc v1.4 serializer defaults
                messageFormatter.JsonSerializer.NullValueHandling = NullValueHandling.Ignore;
                messageFormatter.JsonSerializer.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
                messageFormatter.JsonSerializer.Converters.Add(new UriConverter());
                Services.AddService(messageFormatter.JsonSerializer);
                
                try {
                    IHost host = CreateHostBuilder(args).Build();
                    host.Run();
                    
                } catch (Exception e) {
                    DumLogger.Log(e.Message);
                    DumLogger.Log(e.StackTrace);
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) {
            var res = Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => {
                webBuilder.UseStartup<Startup>();
                if (args.Length > 0) {
                    var url = $"http://localhost:{args[0]}";
                    webBuilder.UseUrls(url);
                }
            });
            return res;
        }
    }
}
