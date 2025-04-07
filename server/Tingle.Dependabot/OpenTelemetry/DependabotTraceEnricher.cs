using OpenTelemetry.Extensions.Enrichment;
using System.Net;

namespace Tingle.Dependabot.OpenTelemetry;

internal class DependabotTraceEnricher(IHttpContextAccessor httpContextAccessor) : TraceEnricher
{
    private static readonly IPAddress[] ignoredIPs = [
        IPAddress.Parse("::1"),
        IPAddress.Parse("0.0.0.1"),
    ];

    public override void Enrich(in TraceEnrichmentBag bag)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return; // ensure we have a context

        // add properties
        TryAddIfNotDefault(bag, "project_id", context.GetProjectId());
        TryAddIfNotDefault(bag, "ip_address", GetIPAddress(context.Connection?.RemoteIpAddress)?.ToString());
    }

    private static void TryAddIfNotDefault(TraceEnrichmentBag bag, string key, object? value)
    {
        if (value is null) return;
        if (value is string str && string.IsNullOrWhiteSpace(str)) return;
        bag.Add(key, value);
    }

    private static IPAddress? GetIPAddress(IPAddress? address)
    {
        if (address == null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return ignoredIPs.Contains(address) ? null : address;
    }
}
