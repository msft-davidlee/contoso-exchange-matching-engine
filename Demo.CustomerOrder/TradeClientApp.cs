using Demo.Shared;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using System.Collections.Concurrent;

namespace Demo.CustomerOrder
{
    public class TradeClientApp : MessageCracker, IApplication
    {
        private int _orderId = 0;
        private readonly ILogger<TradeClientApp> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly string _clientName;
        private readonly string _defaultSendSymbol;
        private readonly IReadOnlyList<string> _simulationSymbols;
        public TradeClientApp(ILogger<TradeClientApp> logger, TelemetryClient telemetryClient, IConfiguration configuration)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            _clientName = configuration[SharedConstants.ClientNameConfig];
            _defaultSendSymbol = configuration[SharedConstants.DefaultSendSymbol];

            var symbols = configuration.GetSection("SimulationSymbols").Get<string[]>();
            _simulationSymbols = symbols.ToList();
        }

        private readonly ConcurrentBag<string> _orders = new ConcurrentBag<string>();

        Session? _session = null;

        // This variable is a kludge for developer test purposes.  Don't do this on a production application.
        public IInitiator? MyInitiator = null;

        #region IApplication interface overrides

        public void OnCreate(SessionID sessionID)
        {
            _session = Session.LookupSession(sessionID);
        }

        public void OnLogon(SessionID sessionID) { _logger.LogInformation("Logon - " + sessionID.ToString()); }
        public void OnLogout(SessionID sessionID) { _logger.LogInformation("Logout - " + sessionID.ToString()); }

        public void FromAdmin(QuickFix.Message message, SessionID sessionID) { }
        public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }

        public void FromApp(QuickFix.Message message, SessionID sessionID)
        {
            _logger.LogInformation($"IN: {message}");
            try
            {
                Crack(message, sessionID);
            }
            catch (Exception ex)
            {
                _logger.LogError("Cracker exception", ex);
            }
        }

        public void ToApp(QuickFix.Message message, SessionID sessionID)
        {
            try
            {
                bool possDupFlag = false;
                if (message.Header.IsSetField(Tags.PossDupFlag))
                {
                    possDupFlag = QuickFix.Fields.Converters.BoolConverter.Convert(
                        message.Header.GetString(Tags.PossDupFlag)); /// FIXME
                }
                if (possDupFlag)
                    throw new DoNotSend();
            }
            catch (FieldNotFoundException)
            { }

            _logger.LogInformation("OUT: " + message.ToString());
        }
        #endregion

        #region MessageCracker handlers
        public void OnMessage(ExecutionReport m, SessionID s)
        {
            string? mixedId = m.IsSetField(Text.TAG) ? m.GetString(Text.TAG) : null;

            if (mixedId == null)
            {
                string orderId = m.ClOrdID.getValue();
                _orders.Add(orderId);
                _logger.LogInformation($"Received execution report for order {orderId}");
            }
            else
            {
                var idList = mixedId.Split('|');
                var r = new RequestTelemetry { Name = SharedConstants.Traces.ProcessExecutionReport };
                r.Context.Operation.ParentId = idList[0];
                r.Context.Operation.Id = idList[1];
                using (_telemetryClient.StartOperation(r))
                {
                    string orderId = m.ClOrdID.getValue();
                    _orders.Add(orderId);
                    _logger.LogInformation($"Received execution report with Text field for order {orderId}");
                }
            }
        }

        public void OnMessage(OrderCancelReject m, SessionID s)
        {
            _logger.LogInformation("Received order cancel reject");
        }
        #endregion

        // This is used only in my test environment to sim some traffic
        // to test app insights
        private void RunSimulation(string cmd)
        {
            try
            {
                var parts = cmd.Split(' ');
                var buyOrSell = Convert.ToInt32(parts[1]);
                var frequencyInSeconds = Convert.ToInt32(parts[2]);
                var durationInMins = Convert.ToInt32(parts[3]);

                DateTime start = DateTime.Now;
                var symbolIndex = 0;
                var symbolMaxIndex = _simulationSymbols.Count - 1;
                while (true)
                {
                    Random r = new Random();
                    if (buyOrSell != 0 || buyOrSell != 1)
                    {
                        buyOrSell = r.Next(0, 100) > 50 ? 1 : 0;
                    }

                    var selectedSymbol = _simulationSymbols[symbolIndex];
                    symbolIndex = (symbolIndex == symbolMaxIndex) ? 0 : symbolIndex + 1;

                    int qty = r.Next(10, 100);
                    int price = r.Next(10, 300);

                    ProcessLine($"{buyOrSell}|{qty}|{price}|{selectedSymbol}");

                    if ((DateTime.Now - start).TotalMinutes > durationInMins)
                    {
                        break;
                    }

                    Console.WriteLine("Waiting for next run...");
                    Task.Delay(frequencyInSeconds * 1000).GetAwaiter().GetResult();
                }

                Console.WriteLine("Completed simulation");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private void ProcessBuyOrSell(string buyOrSell, string cmd)
        {
            var parts = cmd.Split(' ');
            var qty = parts[1];
            var symbolAndPrice = parts[2].Split('@');

            string symbol = symbolAndPrice.Length == 1 ? SharedConstants.DefaultSendSymbol : symbolAndPrice[0];
            string price = symbolAndPrice[symbolAndPrice.Length == 1 ? 0 : 1];
            //IsBuy|OpenQuantity|Price|?Symbol
            ProcessLine($"{buyOrSell}|{qty}|{price}|{symbol}");
        }

        public void Run(int orderId)
        {
            _orderId = orderId;

            _logger.LogInformation($"Client name: {_clientName}");

            Console.WriteLine("Enter data file path or enter as IsBuy|OpenQuantity|Price|?Symbol:");
            try
            {
                while (true)
                {
                    var cmd = Console.ReadLine();
                    if (string.IsNullOrEmpty(cmd)) return;

                    if (cmd.Contains('|'))
                    {
                        ProcessLine(cmd);
                    }
                    else if (cmd.StartsWith("sim"))
                    {
                        RunSimulation(cmd);
                    }
                    else if (cmd.StartsWith("buy"))
                    {
                        ProcessBuyOrSell("1", cmd);
                    }
                    else if (cmd.StartsWith("sell"))
                    {
                        ProcessBuyOrSell("0", cmd);
                    }
                    else
                    {
                        var filePath = cmd;

                        if (!filePath.EndsWith(".csv"))
                            filePath = $"{filePath}.csv";

                        if (File.Exists(filePath))
                        {
                            using (StreamReader sr = new StreamReader(filePath))
                            {
                                bool hasNotSkippedFirstLine = true;
                                while (sr.Peek() >= 0)
                                {
                                    var line = sr.ReadLine();

                                    if (hasNotSkippedFirstLine)
                                    {
                                        hasNotSkippedFirstLine = false;
                                        continue;
                                    }

                                    if (!string.IsNullOrEmpty(line)) ProcessLine(line);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"File does not exist! {filePath}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Message Not Sent: {e.Message}", e);
            }
        }

        private void ProcessLine(string line)
        {
            NewOrderSingle? m = ParseCsvLineToOrder(line);
            if (m != null)
            {
                using (var c = _telemetryClient.StartOperation<RequestTelemetry>(
                        SharedConstants.Traces.OrderToConfirmation))
                {
                    DateTime dateTime = DateTime.UtcNow;

                    m.Header.GetString(Tags.BeginString);

                    DateTime start = DateTime.UtcNow;
                    var d = new DependencyTelemetry
                    {
                        Name = SharedConstants.Traces.SendFIXMessage,
                        Type = SharedConstants.Protocols.Socket,
                        Target = SharedConstants.ApplicationNames.FixMessageProcessor,
                        Timestamp = start
                    };

                    using (_telemetryClient.StartOperation(d))
                    {
                        m.Text = new Text($"{d.Id}|{c.Telemetry.Context.Operation.Id}");

                        try
                        {
                            SendMessage(m);
                            d.Duration = DateTime.UtcNow - start;
                            d.Success = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex.Message, ex);
                            d.Success = false;
                        }
                    }

                    var orderId = m.ClOrdID.getValue();

                    while (true)
                    {
                        if (_orders.Contains(orderId)) break;

                        var span = DateTime.UtcNow - dateTime;
                        if (span.TotalMilliseconds > SharedConstants.TimeoutInMs)
                        {
                            _logger.LogWarning($"Timeout has occured! Unable to locate order Id {orderId}");
                            return;
                        }
                    }

                    _telemetryClient.TrackMetric(Metrics.OrderToConfirmationToClientInMs, dateTime, orderId);
                }
            }
            else
            {
                _logger.LogWarning($"Unable to process {line}");
            }
        }

        private NewOrderSingle? ParseCsvLineToOrder(string? line)
        {
            if (line == null) return null;

            // Just create order Id from 1.
            _orderId += 1;

            var parts = line.Split('|');

            // IsBuy|OpenQuantity|Price|?Symbol
            bool isBuy = parts[0] == "1";
            int quantity = Convert.ToInt32(parts[1]);
            decimal price = Convert.ToDecimal(parts[2]);
            string sendSymbol = parts.Length == 4 ? parts[3] : _defaultSendSymbol;

            var orderId = _orderId.ToString();
            _logger.LogInformation($"Processing orderId: {orderId} symbol: {sendSymbol} isBuy: {isBuy}, qty: {quantity}, price: {price}");

            NewOrderSingle newOrderSingle = new NewOrderSingle(
                new ClOrdID(orderId),
                new Symbol(sendSymbol),
                new Side(isBuy ? Side.BUY : Side.SELL),
                new TransactTime(DateTime.UtcNow),
                new OrdType(OrdType.LIMIT));

            newOrderSingle.Set(new HandlInst('1'));
            newOrderSingle.Set(new OrderQty(quantity));
            // Default to DAY
            newOrderSingle.Set(new TimeInForce(TimeInForce.DAY));
            newOrderSingle.Set(new Price(price));

            return newOrderSingle;
        }

        private void SendMessage(QuickFix.Message m)
        {
            if (_session != null)
            {
                _session.Send(m);
            }
            else
            {
                // This probably won't ever happen.
                _logger.LogWarning("Can't send message: session not created.");
            }
        }
    }
}
