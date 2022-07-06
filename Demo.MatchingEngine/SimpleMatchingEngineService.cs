using Demo.Shared;
using H.Pipes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderMatcher;
using OrderMatcher.Types;

namespace Demo.MatchingEngine
{
    public class SimpleMatchingEngineService : BackgroundService
    {
        private readonly IReadOnlyList<SimpleMatchingEngine> _simpleMatchingEngines;
        private readonly ILogger<SimpleMatchingEngineService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly MarketDataReportCollector _marketDataReportCollector;

        public SimpleMatchingEngineService(
            IEnumerable<SimpleMatchingEngine> simpleMatchingEngines,
            ILogger<SimpleMatchingEngineService> logger,
            TelemetryClient telemetryClient,
            MarketDataReportCollector marketDataReportCollector)
        {
            _simpleMatchingEngines = simpleMatchingEngines.ToList();
            _logger = logger;
            _telemetryClient = telemetryClient;
            _marketDataReportCollector = marketDataReportCollector;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await using var server = new PipeServer<OrderMessage>(SharedConstants.PipeName);

            server.ClientConnected += (o, args) =>
            {
                _logger.LogInformation("Client connected.");
            };

            server.ClientDisconnected += (o, args) =>
            {
                _logger.LogInformation("Client disconnected.");
            };

            server.MessageReceived += async (sender, args) =>
            {
                var msg = args.Message;
                if (msg != null)
                {
                    var r = new RequestTelemetry { Name = SharedConstants.Traces.ExecuteOrderInMatchingEngine };
                    r.Context.Operation.ParentId = msg.DependencyId;
                    r.Context.Operation.Id = msg.OperationId;

                    using (_telemetryClient.StartOperation(r))
                    {
                        var orderId = msg.OrderId.ToString();
                        _telemetryClient.TrackMetric(Metrics.FixProcessorToMatchingEngineInMs, msg.Created, orderId);

                        var timestamp = Convert.ToInt32(DateTimeOffset.Now.ToUnixTimeSeconds());
                        DateTime matchingEngineProcessTimeInMs = DateTime.UtcNow;

                        OrderMatchingResult result;

                        using (_telemetryClient.StartOperation<RequestTelemetry>(SharedConstants.Traces.ProcessingOrderInMatchingEngine))
                        {
                            SimpleMatchingEngine simpleMatchingEngine;
                            try
                            {
                                simpleMatchingEngine = _simpleMatchingEngines.Single(x => x.Symbol == msg.Symbol);

                                result = simpleMatchingEngine.AddOrder(new Order
                                {
                                    OpenQuantity = msg.Quantity,
                                    OrderId = msg.OrderId,
                                    IsBuy = msg.IsBuy,
                                    Price = msg.Price
                                }, timestamp);

                                _logger.LogInformation($"orderId: {msg.OrderId}, result: {result}");

                                msg.IsOrderAccepted = result == OrderMatchingResult.OrderAccepted;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e.Message, e);
                            }
                        }

                        _telemetryClient.TrackMetric(Metrics.MatchingEngineProcessTimeInMs, matchingEngineProcessTimeInMs, orderId);

                        DateTime fixedProcessorStart = DateTime.UtcNow;
                        var fixedProcessorDependency = new DependencyTelemetry
                        {
                            Name = SharedConstants.Traces.RespondingMatchingEngineResultToFixMessageProcessor,
                            Type = SharedConstants.Protocols.Socket,
                            Target = SharedConstants.ApplicationNames.FixMessageProcessor,
                            Timestamp = fixedProcessorStart
                        };

                        using (_telemetryClient.StartOperation(fixedProcessorDependency))
                        {
                            try
                            {
                                msg.DependencyId = fixedProcessorDependency.Id;
                                await server.WriteAsync(msg);
                                fixedProcessorDependency.Success = true;
                                fixedProcessorDependency.Duration = DateTime.UtcNow - fixedProcessorStart;
                            }
                            catch (IOException e)
                            {
                                fixedProcessorDependency.Data = e.ToString();
                                fixedProcessorDependency.Success = false;
                                _logger.LogError(e.Message, e);
                            }
                        }

                        if (msg.IsOrderAccepted.HasValue && msg.IsOrderAccepted.Value)
                        {
                            DateTime start = DateTime.UtcNow;
                            while (true)
                            {
                                if (_marketDataReportCollector.IsExist(msg.OrderId))
                                {
                                    _marketDataReportCollector.Send(msg.OrderId, _telemetryClient, msg.OperationId);
                                    break;
                                }

                                if ((DateTime.UtcNow - start).TotalMilliseconds > SharedConstants.TimeoutInMs)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            };

            server.ExceptionOccurred += (o, args) =>
            {
                _logger.LogError(args.Exception.Message, args.Exception);
            };

            await server.StartAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                // Heartbeat
                _telemetryClient.TrackHeartbeat(
                    SharedConstants.ApplicationNames.MatchingEngine);

                await Task.Delay(TimeSpan.FromSeconds(
                    SharedConstants.ServicePollingIntervalSeconds));
            }
        }
    }
}
