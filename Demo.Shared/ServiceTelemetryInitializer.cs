using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Demo.Shared
{
    public class ServiceTelemetryInitializer : ITelemetryInitializer
    {
        private readonly string _applicationName;

        public ServiceTelemetryInitializer(string applicationName)
        {
            _applicationName = applicationName;
        }

        public void Initialize(ITelemetry telemetry)
        {
            if (string.IsNullOrEmpty(telemetry.Context.Cloud.RoleName))
            {
                telemetry.Context.Cloud.RoleName = _applicationName;
                telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
            }
        }
    }
}
