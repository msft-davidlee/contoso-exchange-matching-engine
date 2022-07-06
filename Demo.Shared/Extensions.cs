using Microsoft.ApplicationInsights;
using QuickFix.Fields.Converters;

namespace Demo.Shared
{
    public static class Extensions
    {
        public static double TrackMetric(this TelemetryClient telemetryClient, string metric, DateTime start, string? orderId)
        {
            return telemetryClient.TrackMetric(metric, (DateTime.UtcNow - start).TotalMilliseconds, orderId);
        }

        public static double TrackMetric(this TelemetryClient telemetryClient, string metric, double elapsed, string? orderId)
        {
            var propBag = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(orderId))
            {
                propBag.Add("orderId", orderId);
            }
            telemetryClient.TrackMetric(metric, elapsed, propBag);

            return elapsed;
        }

        private const string Region = "CentralUS";

        public static void TrackHeartbeat(this TelemetryClient telemetryClient, string applicationName)
        {
            telemetryClient.TrackAvailability($"{applicationName}_heartbeat",
                DateTimeOffset.UtcNow, TimeSpan.FromSeconds(
                    SharedConstants.ServicePollingIntervalSeconds), Region, true);
        }

        public static string GetFIXCurrentDateTime()
        {
            return DateTimeConverter.Convert(DateTime.UtcNow, TimeStampPrecision.Microsecond);
        }

        public static DateTime FromFIX(string currentDateTime)
        {
            return DateTimeConverter.ConvertToDateTime(currentDateTime, TimeStampPrecision.Microsecond);
        }
    }
}
