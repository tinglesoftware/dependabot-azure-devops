using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using System.Diagnostics;

namespace Tingle.Dependabot.OpenTelemetry;

internal sealed class FilteringTraceProcessor(IHttpContextAccessor httpContextAccessor) : BaseProcessor<Activity>
{
    //private static readonly string AspNetCoreActivitySourceName = "Microsoft.AspNetCore";
    private static readonly string HttpClientActivitySourceName = "System.Net.Http";
    private static readonly string AzureHttpActivitySourceName = "Azure.Core.Http";

    private static readonly string[] ExcludedOperationNames =
    [
        "ServiceBusReceiver.Receive",
        "ServiceBusProcessor.ProcessMessage",
    ];

    /// <inheritdoc/>
    public override void OnStart(Activity data)
    {
        if (ShouldSkip(httpContextAccessor, data))
        {
            data.IsAllDataRequested = false;
        }
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (ShouldSkip(httpContextAccessor, data))
        {
            data.IsAllDataRequested = false;
        }
    }

    private static bool ShouldSkip(IHttpContextAccessor httpContextAccessor, Activity data)
    {
        // Prevent all exporters from exporting internal activities
        if (data.Kind == ActivityKind.Internal) return true;

        // Skip known operation names
        if (ExcludedOperationNames.Contains(data.OperationName, StringComparer.OrdinalIgnoreCase)) return true;

        // Azure SDKs create their own client span before calling the service using HttpClient
        // In this case, we would see two spans corresponding to the same operation
        // 1) created by Azure SDK 2) created by HttpClient
        // To prevent this duplication we are filtering the span from HttpClient
        // as span from Azure SDK contains all relevant information needed.
        // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/core/Azure.Core/samples/Diagnostics.md#avoiding-double-collection-of-http-activities
        // https://github.com/Azure/azure-sdk-for-net/blob/0aaf525dd6176cd5c8167f6c0934d635417ccdae/sdk/monitor/Azure.Monitor.OpenTelemetry.AspNetCore/src/OpenTelemetryBuilderExtensions.cs#L114-L127
        var sourceName = data.Source.Name;
        var parentName = data.Parent?.Source.Name;
        if (sourceName == HttpClientActivitySourceName && parentName == AzureHttpActivitySourceName) return true;

        // Skip requests sent to the orchestrator to find the details of a pod/host
        // Sometimes they fail, like when the service is starting up
        if (sourceName == HttpClientActivitySourceName)
        {
            var address = data.Tags.FirstOrDefault(p => p.Key == "server.address").Value;
            if (string.Equals(address, "10.0.0.1", StringComparison.OrdinalIgnoreCase)) return true;
        }

        // NOTE: should we decide to ignore these, we should also ignore the child spans
        //// Skip requests for /health and /liveness because they are better diagnosed via logs
        //if (sourceName == AspNetCoreActivitySourceName)
        //{
        //    var context = httpContextAccessor.HttpContext;
        //    var path = data.Tags.FirstOrDefault(p => p.Key == "url.path").Value ?? context?.Request.Path.Value;
        //    if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)) return true;
        //    if (string.Equals(path, "/liveness", StringComparison.OrdinalIgnoreCase)) return true;
        //}

        return false;
    }
}
