using Demo.Shared;
using H.Pipes;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace Demo.FIXMessageProcessor
{
    public class SimpleMatchingEnginePipeClient
    {
        private readonly ILogger<SimpleMatchingEnginePipeClient> _logger;
        private readonly TelemetryClient _telemetryClient;
        public SimpleMatchingEnginePipeClient(
            ILogger<SimpleMatchingEnginePipeClient> logger, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task<bool?> Send(bool isBuy, decimal openQty, decimal price, int orderId, string symbol, string operationId)
        {
            using (var c = _telemetryClient.StartOperation<RequestTelemetry>(SharedConstants.Traces.OrderToMatchingEngine))
            {
                c.Telemetry.Context.Operation.Id = operationId;

                bool? isOrderAccepted = null;
                _logger.LogInformation($"Creating NamedPipe client to send order {orderId}");

                await using var client = new PipeClient<OrderMessage>(SharedConstants.PipeName);
                {
                    client.MessageReceived += (o, args) =>
                    {
                        // Broadcast message by server may return to all clients, that's
                        // why it's better to do a check.
                        if (args.Message != null && args.Message.OrderId == orderId)
                        {
                            var r = new RequestTelemetry { Name = SharedConstants.Traces.OrderAccpeted };
                            r.Context.Operation.ParentId = args.Message.DependencyId;
                            r.Context.Operation.Id = args.Message.OperationId;
                            using (_telemetryClient.StartOperation(r))
                            {
                                isOrderAccepted = args.Message.IsOrderAccepted;
                                _logger.LogInformation("Message Received: " + args.Message);
                            }
                        }
                    };

                    client.Disconnected += (o, args) => _logger.LogInformation("Disconnected from server");
                    client.Connected += (o, args) => _logger.LogInformation("Connected to server");
                    client.ExceptionOccurred += (o, args) => _logger.LogError(args.Exception.Message, args.Exception);

                    DateTime start = DateTime.UtcNow;
                    var d = new DependencyTelemetry
                    {
                        Name = SharedConstants.Traces.SendOrderToMatchingEngine,
                        Type = SharedConstants.Protocols.NamedPipe,
                        Target = SharedConstants.ApplicationNames.MatchingEngine,
                        Timestamp = start
                    };
                    
                    using (_telemetryClient.StartOperation(d))
                    {
                        await client.ConnectAsync();
                        await client.WriteAsync(new OrderMessage
                        {
                            IsBuy = isBuy,
                            Quantity = openQty,
                            Price = price,
                            Created = DateTime.UtcNow,
                            OrderId = orderId,
                            Symbol = symbol,
                            OperationId = operationId,
                            DependencyId = d.Id
                        });
                        d.Success = true;
                        d.Duration = DateTime.UtcNow - start;
                    }

                    // Expect a response from matching engine within TimeoutInMs.
                    // Otherwise, we are closing the connection.
                    using (_telemetryClient.StartOperation<RequestTelemetry>(SharedConstants.Traces.MatchingEngineResponsed))
                    {
                        DateTime startTrack = DateTime.UtcNow;
                        while (true)
                        {
                            if (isOrderAccepted.HasValue) break;

                            TimeSpan elasped = DateTime.UtcNow - startTrack;
                            if (elasped.TotalMilliseconds > SharedConstants.TimeoutInMs) break;
                        }
                    }
                }

                return isOrderAccepted;
            }
        }
    }
}
