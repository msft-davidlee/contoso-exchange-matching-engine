using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Demo.MarketDataRecipient
{
    public class MulticastClientHostService : IHostedService
    {
        private readonly string _multicastIPAddress;
        private readonly string _multicastPort;
        private readonly ILogger<MulticastClientHostService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private bool _shouldStop;
        private readonly string _clientName;
        private readonly ClientConfiguration _clientConfiguration;

        public MulticastClientHostService(
            IConfiguration configuration,
            ILogger<MulticastClientHostService> logger,
            TelemetryClient telemetryClient,
            ClientConfiguration clientConfiguration)
        {
            _multicastIPAddress = configuration["MulticastIPAddress"];
            _multicastPort = configuration["MulticastPort"];
            _logger = logger;
            _telemetryClient = telemetryClient;
            _clientName = configuration[SharedConstants.ClientNameConfig];
            _clientConfiguration = clientConfiguration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting client {_clientName}");

            if (!string.IsNullOrEmpty(_multicastIPAddress) &&
                !string.IsNullOrEmpty(_multicastPort))
            {
                _logger.LogInformation($"Starting listener task ip={_multicastIPAddress} port={_multicastPort}");

                IPAddress mcastAddress;
                int mcastPort;
                if (IPAddress.TryParse(_multicastIPAddress, out mcastAddress) &&
                    int.TryParse(_multicastPort, out mcastPort))
                {
                    UdpClient? udpClient = null;
                    try
                    {
                        udpClient = new UdpClient(AddressFamily.InterNetwork);
                        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, mcastPort));
                        udpClient.JoinMulticastGroup(mcastAddress);
                    }
                    catch (Exception ex)
                    {
                        var message = ex.Message;
                        _logger.LogError($"Unable to initialize: {message},", ex);
                        return;
                    }

                    await Task.Run(() =>
                    {
                        _logger.LogInformation("Running listener task");
                        while (!_shouldStop)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            try
                            {
                                var ipEndPoint = new IPEndPoint(mcastAddress, mcastPort); ;
                                var data = udpClient.Receive(ref ipEndPoint);
                                if (_clientConfiguration.IsTestMode())
                                {
                                    var message = Encoding.Default.GetString(data);
                                    _logger.LogInformation($"Message: {message}");

                                    if (message.StartsWith("timestamp="))
                                    {
                                        using (_telemetryClient.StartOperation<RequestTelemetry>(
                                            Metrics.TestMatchingEngineToMarketDataRecipientInMs))
                                        {
                                            var dateTimeString = message.Split('=')[1];
                                            TimeSpan timeSpan = DateTime.UtcNow - Extensions.FromFIX(dateTimeString);
                                            _logger.LogInformation($"Elasped-In-Millseconds={timeSpan.TotalMilliseconds}");
                                            _telemetryClient.TrackMetric(Metrics.TestMatchingEngineToMarketDataRecipientInMs,
                                                timeSpan.TotalMilliseconds);
                                        }
                                    }
                                }
                                else
                                {
                                    var marketDataReport = MarketDataReport.Parse(data);

                                    var r = new RequestTelemetry { Name = SharedConstants.Traces.MarketDataReported };
                                    r.Context.Operation.ParentId = marketDataReport.DependencyId;
                                    r.Context.Operation.Id = marketDataReport.OperationId;
                                    using (_telemetryClient.StartOperation(r))
                                    {
                                        _logger.LogInformation($"Message: {marketDataReport.GetFormattedText()}");
                                        if (marketDataReport.Created.HasValue)
                                        {
                                            _telemetryClient.TrackMetric(Metrics.MatchingEngineToMarketDataRecipientInMs,
                                                marketDataReport.Created.Value, null);
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                var message = e.ToString();
                                _logger.LogError($"Receive error: {message}", e);

                                // Stop when an exception occurs.
                                break;
                            }
                        }

                        udpClient.Close();
                        udpClient.Dispose();
                    }, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Unable to receive message from multicast endpoint as configuration is not valid.");
                }
            }
            else
            {
                _logger.LogInformation("Unable to receive message from multicast endpoint as it is not configured.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shouldStop = true;
            return Task.CompletedTask;
        }
    }
}
