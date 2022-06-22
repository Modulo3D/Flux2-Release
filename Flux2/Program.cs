using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modulo3DStandard;
using System;
using System.Diagnostics;

namespace Flux.ViewModels
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureLogging((_, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddFile("../Flux/Log/flux.log", o =>
                    {
                        o.Append = true;
                        o.MaxRollingFiles = 10;
                        o.UseUtcTimestamp = true;
                        o.FileSizeLimitBytes = 5242880;
                    });

                    if(Debugger.IsAttached)
                        logging.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<FluxViewModel>();
                });
        }
    }
}
