using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Demo.MatchingEngine
{
    public class MarketDataReportCollector
    {
        private readonly MulticastService _multicastService;
        private readonly ILogger<MarketDataReportCollector> _logger;
        private readonly ConcurrentDictionary<int, List<MarketDataReport>> _data = new ConcurrentDictionary<int, List<MarketDataReport>>();

        public MarketDataReportCollector(MulticastService multicastService,
            ILogger<MarketDataReportCollector> logger)
        {
            _multicastService = multicastService;
            _logger = logger;
        }

        public void Add(int orderId, MarketDataReport marketDataReport)
        {
            if (_data.ContainsKey(orderId))
            {
                _data[orderId].Add(marketDataReport);
            }
            else
            {
                _data.TryAdd(orderId, new List<MarketDataReport> { marketDataReport });
            }
        }

        public bool IsExist(int orderId)
        {
            return _data.ContainsKey(orderId);
        }

        public void Send(int orderId, TelemetryClient telemetryClient, string operationId)
        {
            var list = _data[orderId].Where(x => !x.Sent);
            foreach (var m in list)
            {
                DateTime dStart = DateTime.UtcNow;
                var d = new DependencyTelemetry
                {
                    Name = SharedConstants.Traces.SendMarketDataReport,
                    Type = SharedConstants.Protocols.Multicast,
                    Target = SharedConstants.ApplicationNames.MarketDataRecipient,
                    Timestamp = dStart
                };

                using (telemetryClient.StartOperation(d))
                {
                    m.OperationId = operationId;
                    m.DependencyId = d.Id;

                    try
                    {
                        _multicastService.Send(m);
                        d.Duration = DateTime.UtcNow - dStart;
                        d.Success = true;
                        m.Sent = true;
                    }
                    catch (Exception e)
                    {
                        d.Success = false;
                        _logger.LogError(e.Message, e);
                    }
                }
            }
        }
    }
}
