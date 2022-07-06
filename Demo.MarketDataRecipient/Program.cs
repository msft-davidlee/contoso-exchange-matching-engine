using Demo.Shared;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Dynamic;
using System.Text.Json;

namespace Demo.MarketDataRecipient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Helper function to update local config file.
            if (args.Length == 3 && args[0] == "initconfig")
            {
                const string appFile = "appsettings.json";
                if (File.Exists(appFile))
                {
                    dynamic obj = JsonSerializer.Deserialize<ExpandoObject>(File.ReadAllText(appFile));
                    obj.MulticastIPAddress = args[1];
                    obj.MulticastPort = args[2];

                    var jsonWriteOptions = new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    };
                    string json = JsonSerializer.Serialize(obj, jsonWriteOptions);
                    File.WriteAllText(appFile, json);
                    Console.WriteLine($"{appFile} updated.");
                }
                return;
            }

            var builder = Host.CreateDefaultBuilder(args);

            using IHost host = builder.ConfigureServices(services =>
            {
                services.AddSingleton<ITelemetryInitializer>(x =>
                {
                    var config = x.GetRequiredService<IConfiguration>();
                    var clientName = config[SharedConstants.ClientNameConfig];
                    return new ServiceTelemetryInitializer(!string.IsNullOrEmpty(clientName) ? clientName : SharedConstants.ApplicationNames.MarketDataRecipient);
                });

                services.AddSingleton(x =>
                {
                    if (args.Length == 1 && args[0] == "enabletestmode")
                    {
                        return new ClientConfiguration(true);
                    }
                    var cfg = x.GetRequiredService<IConfiguration>();
                    return new ClientConfiguration(cfg["TestMode"] == "true");
                });

                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
                services.AddHostedService<MulticastClientHostService>();
                services.AddApplicationInsightsTelemetryWorkerService();
            }).Build();


            await host.RunAsync();
        }
    }
}