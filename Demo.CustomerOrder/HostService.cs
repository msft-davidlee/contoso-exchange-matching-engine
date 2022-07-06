using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Demo.CustomerOrder
{
    public class HostService : IHostedService
    {
        public const string CONFIG_FILE = "tradeclient.cfg";
        private readonly QuickFix.Transport.SocketInitiator _initiator;
        private readonly FIXLoggingOptions _fIXLoggingOptions;
        private readonly TradeClientApp _application;

        public HostService(
            IConfiguration configuration,
            FIXLoggingOptions fIXLoggingOptions,
            TradeClientApp application)
        {
            bool enableIncomingConsoleOutput;
            bool.TryParse(configuration["EnableIncomingConsoleOutput"], out enableIncomingConsoleOutput); ;

            QuickFix.SessionSettings settings = new QuickFix.SessionSettings(CONFIG_FILE);

            _fIXLoggingOptions = fIXLoggingOptions;
            _application = application;

            QuickFix.IMessageStoreFactory storeFactory = new QuickFix.FileStoreFactory(settings);
            QuickFix.ILogFactory logFactory = new QuickFix.ScreenLogFactory(
                enableIncomingConsoleOutput,
                enableIncomingConsoleOutput, true);
            _initiator = new QuickFix.Transport.SocketInitiator(application, storeFactory, settings, logFactory);

            // this is a developer-test kludge.  do not emulate.
            application.MyInitiator = _initiator;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _initiator.Start();
            _application.Run(_fIXLoggingOptions.InitializedOrderIdStart);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _initiator.Stop();

            return Task.CompletedTask;
        }
    }
}
