using Demo.Shared;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuickFix;

namespace Demo.FIXMessageProcessor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args);
            if (args.Length == 1 && args[0] == "service")
            {
                builder.UseWindowsService(options => options.ServiceName = "DemoFIXMessageProcessor");
            }

            using IHost host = builder.ConfigureServices(services =>
            {
                services.AddSingleton<ITelemetryInitializer>(x =>
                    new ServiceTelemetryInitializer(SharedConstants.ApplicationNames.FixMessageProcessor));

                services.AddSingleton<IApplication, Executor>();
                services.AddSingleton<SimpleMatchingEnginePipeClient>();
                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
                services.AddHostedService<HostService>();
                services.AddApplicationInsightsTelemetryWorkerService();
            }).Build();

            await host.RunAsync();
        }
    }
}