using Demo.Shared;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Demo.CustomerOrder
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Helper function to update local config file.
            if (args.Length == 2 && args[0] == "initconfig")
            {
                if (File.Exists(HostService.CONFIG_FILE))
                {
                    var lines = File.ReadAllLines(HostService.CONFIG_FILE);
                    var sb = new StringBuilder();
                    foreach (var line in lines)
                    {
                        var append = line.StartsWith("SocketConnectHost=") ?
                            $"SocketConnectHost={args[1]}" : line;
                        sb.AppendLine(append);
                    }
                    File.WriteAllText(HostService.CONFIG_FILE, sb.ToString());
                    Console.WriteLine($"{HostService.CONFIG_FILE} updated.");
                }
                return;
            }

            var builder = Host.CreateDefaultBuilder(args);

            using IHost host = builder.UseConsoleLifetime().ConfigureServices(services =>
            {
                int initializedOrderIdStart = 0;
                if (args.Length == 1) int.TryParse(args[0], out initializedOrderIdStart);

                services.AddSingleton(x => new FIXLoggingOptions { InitializedOrderIdStart = initializedOrderIdStart });
                services.AddSingleton<TradeClientApp>();
                services.AddSingleton<ITelemetryInitializer>(x =>
                {
                    var config = x.GetRequiredService<IConfiguration>();
                    var clientName = config[SharedConstants.ClientNameConfig];
                    return new ServiceTelemetryInitializer(!string.IsNullOrEmpty(clientName) ? clientName : SharedConstants.ApplicationNames.DemoClient);
                });

                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
                services.AddHostedService<HostService>();
                services.AddApplicationInsightsTelemetryWorkerService();
            }).Build();


            await host.RunAsync();
        }
    }
}