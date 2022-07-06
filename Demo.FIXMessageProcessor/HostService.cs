using Microsoft.Extensions.Hosting;
using Acceptor;
using QuickFix;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Demo.Shared;

namespace Demo.FIXMessageProcessor
{
    public class HostService : BackgroundService
    {
        private const string HttpServerPrefix = "http://127.0.0.1:5080/";
        private const string CONGIG_FILE = "executor.cfg";
        private readonly ILogger<HostService> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly IApplication _executorApp;

        public HostService(
            IApplication executorApp,
            ILogger<HostService> logger,
            TelemetryClient telemetryClient)
        {
            _executorApp = executorApp;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FIXMessageProcessor started");

            try
            {
                SessionSettings settings = new SessionSettings(CONGIG_FILE);
                IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
                ILogFactory logFactory = new FileLogFactory(settings);
                ThreadedSocketAcceptor acceptor = new ThreadedSocketAcceptor(_executorApp, storeFactory, settings, logFactory);
                HttpServer srv = new HttpServer(HttpServerPrefix, settings);

                acceptor.Start();
                srv.Start();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Heartbeat
                    _telemetryClient.TrackHeartbeat(
                        SharedConstants.ApplicationNames.FixMessageProcessor);

                    await Task.Delay(TimeSpan.FromSeconds(
                        SharedConstants.ServicePollingIntervalSeconds));
                }

                srv.Stop();
                acceptor.Stop();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.

                _logger.LogInformation("FIXMessageProcessor exited");

                Environment.Exit(1);
            }

            _logger.LogInformation("FIXMessageProcessor exited");
        }
    }
}
