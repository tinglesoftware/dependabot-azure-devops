using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Tingle.Dependabot.ApplicationInsights;

internal class DependabotTelemetryInitializer : ITelemetryInitializer
{
    private const string KeyProjectId = "ProjectId";

    private readonly IHttpContextAccessor httpContextAccessor;

    public DependabotTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public void Initialize(ITelemetry telemetry)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null || telemetry is not RequestTelemetry rt) return; // ensure we have a context and the telemetry is for a request

        // add properties
        var props = rt.Properties;
        props.TryAddIfNotDefault(KeyProjectId, context.GetProjectId());
    }
}
