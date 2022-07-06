using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderMatcher;
using System.Dynamic;
using System.Text.Json;

namespace Demo.MatchingEngine
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 4 && args[0] == "mcastts")
            {
                try
                {
                    var ip = args[1];
                    var port = args[2];
                    var count = Convert.ToInt32(args[3]);

                    for (int i = 0; i < count; i++)
                    {
                        var testMessage = $"timestamp={Extensions.GetFIXCurrentDateTime()}";
                        MulticastService.SendTest(ip, port, testMessage);
                        Console.WriteLine($"test message sent: {testMessage}");
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.ToString());
                }
                return;
            }

            // Helper function to test multicast
            if (args.Length == 4 && args[0] == "mcast")
            {
                try
                {
                    var ip = args[1];
                    var port = args[2];
                    var testMessage = args[3];
                    MulticastService.SendTest(ip, port, testMessage);
                    Console.WriteLine("Success!");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.ToString());
                }
                return;
            }

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
            if (args.Length == 1 && args[0] == "service")
            {
                builder.UseWindowsService(options => options.ServiceName = "DemoMatchingEngine");
            }

            using IHost host = builder.ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options => options.ShutdownTimeout = SharedConstants.DefaultTimeout());

                services.AddSingleton<ITelemetryInitializer>(x =>
                    new ServiceTelemetryInitializer(SharedConstants.ApplicationNames.MatchingEngine));

                services.AddSingleton<MyFeeProvider>();
                services.AddSingleton<MulticastService>();
                services.AddSingleton<MarketDataReportCollector>();

                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build();

                var symbols = config.GetSection("Symbols").Get<string[]>();
                foreach (var symbol in symbols)
                {
                    services.AddScoped(x => new SimpleMatchingEngine(symbol,
                        new MyTradeListener(
                            x.GetRequiredService<ILogger<MyTradeListener>>(),
                            x.GetRequiredService<MarketDataReportCollector>(),
                            symbol),
                            x.GetRequiredService<MyFeeProvider>(), 1, 2));
                }

                services.AddHostedService<SimpleMatchingEngineService>();
                services.AddApplicationInsightsTelemetryWorkerService();
            }).Build();

            await host.RunAsync();
        }
    }
}
