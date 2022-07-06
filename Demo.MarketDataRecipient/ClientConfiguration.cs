namespace Demo.MarketDataRecipient
{
    public class ClientConfiguration
    {
        private readonly bool _testMode;
        public ClientConfiguration(bool testMode)
        {
            _testMode = testMode;
        }

        public bool IsTestMode()
        {
            return _testMode;
        }
    }
}
