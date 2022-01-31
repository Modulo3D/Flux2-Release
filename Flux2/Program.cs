using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

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
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService(p => 
                    {
                        var flux_viewmodel = new FluxViewModel();
                        flux_viewmodel.InitializeRemoteView();
                        return flux_viewmodel;
                    });
                });
        }
    }
}
