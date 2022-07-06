namespace Demo.Shared
{
    public static class SharedConstants
    {
        public const int TimeoutInMs = 300;
        public const string PipeName = "simplematchingengine";
        public const string ClientNameConfig = "AppClientName";
        public static class ApplicationNames
        {
            public const string DemoClient = "DemoClient";
            public const string FixMessageProcessor = "FixMessageProcessor";
            public const string MatchingEngine = "MatchingEngine";
            public const string MarketDataRecipient = "MarketDataRecipient";
            public const string ClientSeller = "ClientSeller";
            public const string ClientBuyer = "ClientBuyer";
            public const string ClientBuyerOrSeller = "ClientBuyerOrSeller";
        }

        public const int ServicePollingIntervalSeconds = 15;

        public const string DefaultSendSymbol = "DefaultSendSymbol";

        public static class Traces
        {
            public const string SendFIXMessage = "SendFIXMessage";
            public const string ExecuteOrderInMatchingEngine = "ExecuteOrderInMatchingEngine";
            public const string ReturnExecutionReport = "ReturnExecutionReport";
            public const string ProcessFIXMessage = "ProcessFIXMessage";
            public const string ProcessExecutionReport = "ProcessExecutionReport";
            public const string OrderToMatchingEngine = "OrderToMatchingEngine";
            public const string OrderToConfirmation = "OrderToConfirmation";
            public const string SendOrderToMatchingEngine = "SendOrderToMatchingEngine";
            public const string MatchingEngineResponsed = "MatchingEngineResponsed";
            public const string ProcessingOrderInMatchingEngine = "ProcessingOrderInMatchingEngine";
            public const string RespondingMatchingEngineResultToFixMessageProcessor = "RespondingMatchingEngineResultToFixMessageProcessor";
            public const string MarketDataReported = "MarketDataReported";
            public const string SendMarketDataReport = "SendMarketDataReport";
            public const string OrderAccpeted = "OrderAccpeted";
        }

        public static class Protocols
        {
            public const string Socket = "Socket";
            public const string NamedPipe = "NamedPipe";
            public const string Multicast = "Multicast";
        }

        public const int DefaultTimeoutInSeconds = 5;
        public static TimeSpan DefaultTimeout()
        {
            return TimeSpan.FromSeconds(DefaultTimeoutInSeconds);
        }
    }
}