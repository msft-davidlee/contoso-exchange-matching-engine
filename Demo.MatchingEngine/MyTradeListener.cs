using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using OrderMatcher;
using OrderMatcher.Types;

namespace Demo.MatchingEngine
{
    public class MyTradeListener : ITradeListener
    {
        private readonly ILogger<MyTradeListener> _logger;
        private readonly MarketDataReportCollector _marketDataReportCollector;        
        private readonly string _symbol;

        public MyTradeListener(ILogger<MyTradeListener> logger, MarketDataReportCollector marketDataReportCollector, string symbol)
        {
            _logger = logger;
            _marketDataReportCollector = marketDataReportCollector;
            _symbol = symbol;
        }
        public void OnAccept(OrderId orderId)
        {
            _logger.LogInformation($"OnAccept Order Triggered.... orderId : {orderId}");
        }

        public void OnCancel(OrderId orderId, Quantity remainingQuantity, Quantity cost, Quantity fee, CancelReason cancelReason)
        {
            _logger.LogInformation($"Order Cancelled.... orderId : {orderId}, remainingQuantity : {remainingQuantity}, cancelReason : {cancelReason}");
        }

        public void OnOrderTriggered(OrderId orderId)
        {
            _logger.LogInformation($"Stop Order Triggered.... orderId : {orderId}");
        }

        public void OnTrade(OrderId incomingOrderId, OrderId restingOrderId, Price matchPrice, Quantity matchQuantity, Quantity? askRemainingQuantity, Quantity? askFee, Quantity? bidCost, Quantity? bidFee)
        {
            _logger.LogInformation($"Order matched.... incomingOrderId : {incomingOrderId}, restingOrderId : {restingOrderId}, executedQuantity : {matchQuantity}, executedPrice : {matchPrice}");
            _marketDataReportCollector.Add(incomingOrderId, new MarketDataReport(_symbol, matchPrice, matchQuantity));
        }
    }
}
