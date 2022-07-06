namespace Demo.Shared
{
    public static class Metrics
    {
        public const string TestMatchingEngineToMarketDataRecipientInMs = "TestMatchingEngineToMarketDataRecipientInMs";
        public const string MatchingEngineToMarketDataRecipientInMs = "MatchingEngineToMarketDataRecipientInMs";
        public const string ClientToFixProcessorInMs = "ClientToFixProcessorInMs";
        public const string FixProcessorToMatchingEngineInMs = "FixProcessorToMatchingEngineInMs";
        public const string MatchingEngineProcessTimeInMs = "MatchingEngineProcessTimeInMs";
        public const string OrderToConfirmationToClientInMs = "OrderToConfirmationToClientInMs";
    }
}
