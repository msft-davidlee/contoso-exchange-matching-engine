using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace Demo.FIXMessageProcessor
{
    public class Executor : MessageCracker, IApplication
    {
        private readonly ILogger<Executor> _logger;
        private readonly SimpleMatchingEnginePipeClient _simpleMatchingEnginePipeClient;
        private readonly TelemetryClient _telemetryClient;

        public Executor(
            ILogger<Executor> logger,
            SimpleMatchingEnginePipeClient simpleMatchingEnginePipeClient,
            TelemetryClient telemetryClient)
        {
            _logger = logger;
            _simpleMatchingEnginePipeClient = simpleMatchingEnginePipeClient;
            _telemetryClient = telemetryClient;
        }

        int orderID = 0;
        int execID = 0;

        private string GenOrderID() { return (++orderID).ToString(); }
        private string GenExecID() { return (++execID).ToString(); }

        #region QuickFix.Application Methods

        public void FromApp(Message message, SessionID sessionID)
        {
            _logger.LogInformation($"IN: {message}");
            Crack(message, sessionID);
        }

        public void ToApp(Message message, SessionID sessionID)
        {
            _logger.LogInformation($"OUT: {message}");
        }

        public void FromAdmin(Message message, SessionID sessionID) { }
        public void OnCreate(SessionID sessionID) { }
        public void OnLogout(SessionID sessionID) { }
        public void OnLogon(SessionID sessionID) { }
        public void ToAdmin(Message message, SessionID sessionID) { }
        #endregion

        #region MessageCracker overloads


        private void ProcessNewSingleOrder(QuickFix.FIX44.NewOrderSingle n, SessionID s, string operationId)
        {
            var dateTime = n.Header.IsSetField(SendingTime.TAG) ?
                n.Header.GetDateTime(SendingTime.TAG) : n.TransactTime.getValue();

            Symbol symbol = n.Symbol;
            Side side = n.Side;
            OrderQty orderQty = n.OrderQty;
            Price price = n.Price;
            ClOrdID clOrdID = n.ClOrdID;

            _telemetryClient.TrackMetric(Metrics.ClientToFixProcessorInMs,
                dateTime, n.ClOrdID.getValue());

            // First we send need to trigger  
            var sideValue = side.getValue();
            if (sideValue == Side.BUY || sideValue == Side.SELL)
            {
                string symbolValue = n.Symbol.getValue();

                bool? isOrderAccepted = _simpleMatchingEnginePipeClient.Send(
                      sideValue == Side.BUY,
                      orderQty.getValue(),
                      price.getValue(),
                      Convert.ToInt32(n.ClOrdID.getValue()),
                      symbolValue,
                      operationId).GetAwaiter().GetResult();

                if (price.Obj == 0)
                    throw new IncorrectTagValue(price.Tag);

                var exReport = new QuickFix.FIX44.ExecutionReport(
                    new OrderID(GenOrderID()),
                    new ExecID(GenExecID()),
                    new ExecType(isOrderAccepted.HasValue ? ExecType.PENDING_NEW :
                        isOrderAccepted == true ? ExecType.FILL : ExecType.REJECTED),
                    new OrdStatus(
                        isOrderAccepted.HasValue ? OrdStatus.PENDING_NEW :
                        isOrderAccepted == true ? OrdStatus.FILLED : OrdStatus.REJECTED),
                    symbol, //shouldn't be here?
                    side,
                    new LeavesQty(0),
                    new CumQty(orderQty.getValue()),
                    new AvgPx(price.getValue()));

                exReport.Set(clOrdID);
                exReport.Set(symbol);
                exReport.Set(orderQty);
                exReport.Set(new LastQty(orderQty.getValue()));
                exReport.Set(new LastPx(price.getValue()));

                if (n.IsSetAccount())
                    exReport.SetField(n.Account);

                DateTime start = DateTime.UtcNow;
                var d = new DependencyTelemetry
                {
                    Name = SharedConstants.Traces.ReturnExecutionReport,
                    Type = SharedConstants.Protocols.Socket,
                    Target = SharedConstants.ApplicationNames.ClientBuyerOrSeller,
                    Timestamp = start
                };
                
                exReport.Text = new Text($"{d.Id}|{operationId}");

                using (_telemetryClient.StartOperation(d))
                {
                    try
                    {
                        Session.SendToTarget(exReport, s);
                        d.Duration = DateTime.UtcNow - start;
                        d.Success = true;
                    }
                    catch (SessionNotFound ex)
                    {
                        _logger.LogError("==session not found exception!==", ex);
                        d.Success = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message, ex);
                        d.Success = false;
                    }
                }
            }
        }

        public void OnMessage(QuickFix.FIX44.NewOrderSingle n, SessionID s)
        {
            string? mixedId = n.IsSetField(Text.TAG) ? n.GetString(Text.TAG) : null;
            if (mixedId == null)
            {
                using (var c = _telemetryClient.StartOperation<RequestTelemetry>(SharedConstants.Traces.ProcessFIXMessage))
                {
                    ProcessNewSingleOrder(n, s, c.Telemetry.Context.Operation.Id);
                }
            }
            else
            {
                var idList = mixedId.Split('|');

                var r = new RequestTelemetry { Name = SharedConstants.Traces.ProcessFIXMessage };
                r.Context.Operation.ParentId = idList[0];
                r.Context.Operation.Id = idList[1];
                using (_telemetryClient.StartOperation(r))
                {
                    ProcessNewSingleOrder(n, s, idList[1]);
                }
            }
        }

        public void OnMessage(QuickFix.FIX44.News n, SessionID s)
        {

        }

        public void OnMessage(QuickFix.FIX44.OrderCancelRequest msg, SessionID s)
        {
            string orderid = (msg.IsSetOrderID()) ? msg.OrderID.Obj : "unknown orderID";
            QuickFix.FIX44.OrderCancelReject ocj = new QuickFix.FIX44.OrderCancelReject(
                new OrderID(orderid), msg.ClOrdID, msg.OrigClOrdID, new OrdStatus(OrdStatus.REJECTED), new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST));
            ocj.CxlRejReason = new CxlRejReason(CxlRejReason.OTHER);
            ocj.Text = new Text("Executor does not support order cancels");

            try
            {
                Session.SendToTarget(ocj, s);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        public void OnMessage(QuickFix.FIX44.OrderCancelReplaceRequest msg, SessionID s)
        {
            string orderid = (msg.IsSetOrderID()) ? msg.OrderID.Obj : "unknown orderID";
            QuickFix.FIX44.OrderCancelReject ocj = new QuickFix.FIX44.OrderCancelReject(
                new OrderID(orderid), msg.ClOrdID, msg.OrigClOrdID, new OrdStatus(OrdStatus.REJECTED), new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REPLACE_REQUEST));
            ocj.CxlRejReason = new CxlRejReason(CxlRejReason.OTHER);
            ocj.Text = new Text("Executor does not support order cancel/replaces");

            try
            {
                Session.SendToTarget(ocj, s);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        public void OnMessage(QuickFix.FIX44.BusinessMessageReject n, SessionID s)
        {

        }

        #endregion //MessageCracker overloads
    }
}
