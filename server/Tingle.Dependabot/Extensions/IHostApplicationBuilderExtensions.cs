using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Tingle.Dependabot.OpenTelemetry;

namespace Microsoft.Extensions.DependencyInjection;

internal static class IHostApplicationBuilderExtensions
{
    public static T AddOpenTelemetry<T>(this T builder) where T : IHostApplicationBuilder
    {
        var environment = builder.Environment;
        var configuration = builder.Configuration;
        var otel = builder.Services.AddOpenTelemetry();

        otel.UseOtlpExporter();

        // prepare authentication for exporters
        var appInsightsConnectionString = configuration.GetValue<string?>("APPLICATIONINSIGHTS_CONNECTION_STRING");
        var axiomApiKey = configuration.GetValue<string?>("Axiom:ApiKey");

        var appInsights = !string.IsNullOrWhiteSpace(appInsightsConnectionString);
        var axiom = !string.IsNullOrWhiteSpace(axiomApiKey);

        static string GetVersion()
        {
            var assembly = Assembly.GetEntryAssembly()!;
            var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr != null && !string.IsNullOrWhiteSpace(attr.InformationalVersion)
                ? attr.InformationalVersion
                : assembly.GetName().Version!.ToString(3);
        }

        // configure the resource
        otel.ConfigureResource(resource =>
        {
            resource.AddAttributes([new("environment", environment.EnvironmentName)]);

            // add environment detectors
            resource.AddHostDetector();
            resource.AddProcessRuntimeDetector();
            resource.AddDetector(new GitResourceDetector());

            // add service name and version (should override any existing values)
            resource.AddService("dependabot-azdo", serviceVersion: GetVersion());
        });

        // add tracing
        otel.WithTracing(tracing =>
        {
            tracing.AddSource([
                "Tingle.EventBus",
                "Tingle.PeriodicTasks",
            ]);
            tracing.AddHttpClientInstrumentation(o => o.RecordException = true);
            tracing.AddEntityFrameworkCoreInstrumentation();
            tracing.AddAspNetCoreInstrumentation(o => o.RecordException = true);

            // add enrichers
            tracing.AddTraceEnricher<DependabotTraceEnricher>();
        });

        // add metrics
        otel.WithMetrics(metrics =>
        {
            metrics.AddHttpClientInstrumentation();
            metrics.AddProcessInstrumentation();
            metrics.AddRuntimeInstrumentation();
            metrics.AddAspNetCoreInstrumentation();
        });

        // add logging support
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
        });

        return builder;
    }
}
