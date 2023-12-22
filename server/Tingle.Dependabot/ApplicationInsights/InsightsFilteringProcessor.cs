using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Tingle.Dependabot.ApplicationInsights;

/// <summary>
/// Implementation of <see cref="ITelemetryProcessor"/> that filters out unneeded telemetry.
/// </summary>
internal class InsightsFilteringProcessor : ITelemetryProcessor
{
    private static readonly string[] excludedRequestNames =
    [
        "ServiceBusReceiver.Receive",
        "ServiceBusProcessor.ProcessMessage",
    ];

    private readonly ITelemetryProcessor next;

    public InsightsFilteringProcessor(ITelemetryProcessor next)
    {
        this.next = next;
    }

    /// <inheritdoc/>
    public void Process(ITelemetry item)
    {
        // Skip unneeded RequestTelemetry
        if (item is RequestTelemetry rt)
        {
            // Skip known request names
            if (rt.Name is not null && excludedRequestNames.Contains(rt.Name, StringComparer.OrdinalIgnoreCase))
            {
                return; // terminate the processor pipeline
            }

            // Skip requests for /health and /liveness because they are better diagnosed via logs
            var path = rt.Url?.AbsolutePath;
            if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/liveness", StringComparison.OrdinalIgnoreCase))
            {
                return; // terminate the processor pipeline
            }
        }

        // Skip requests sent to the orchestrator to find the details of a pod/host
        // Sometimes they fail, like when the service is starting up
        if (item is DependencyTelemetry dt
            && string.Equals("http", dt.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals("10.0.0.1", dt.Target, StringComparison.OrdinalIgnoreCase))
        {
            return; // terminate the processor pipeline
        }

        // process all the others
        next.Process(item);
    }
}
