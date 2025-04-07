using System.Reflection;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Exporter;
using OpenTelemetry.Extensions.Enrichment;
using OpenTelemetry.Logs;
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

        static string GetSegment(OtelCategory category) => category.ToString().ToLowerInvariant();

        void ConfigureExporterOptionsForAzureMonitor(AzureMonitorExporterOptions options)
        {
            options.ConnectionString = appInsightsConnectionString;
            options.DisableOfflineStorage = false;
            options.Credential = null; // we shall set DefaultAzureCredential once ingestion is locked to be authenticated via Entra and the roles in place
        }
        void ConfigureExporterOptionsForAxiom(OtlpExporterOptions options, OtelCategory category)
        {
            options.Endpoint = new Uri($"https://api.axiom.co/v1/{GetSegment(category)}");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Headers = $"Authorization=Bearer {axiomApiKey}, X-Axiom-Dataset={GetSegment(category)}";
        }

        // configure the resource
        otel.ConfigureResource(resource =>
        {
            resource.AddAttributes([new("environment", environment.EnvironmentName)]);

            // add environment detectors
            resource.AddHostDetector();
            resource.AddProcessRuntimeDetector();
            resource.AddDetector(new GitResourceDetector());

            // add detectors for Azure
            resource.AddAzureAppServiceDetector();
            resource.AddAzureVMDetector();
            resource.AddAzureContainerAppsDetector();

            // add service name and version (should override any existing values)
            resource.AddService("dependabot-azdo", serviceVersion: GetVersion());
        });

        // add tracing
        otel.WithTracing(tracing =>
        {
            tracing.AddSource([
                "Azure.*",
                "Tingle.EventBus",
                "Tingle.PeriodicTasks",
            ]);
            tracing.AddHttpClientInstrumentation(o => o.RecordException = true);
            tracing.AddEntityFrameworkCoreInstrumentation();
            tracing.AddSqlClientInstrumentation(o => o.RecordException = true);
            tracing.AddAspNetCoreInstrumentation(o => o.RecordException = true);

            // add enrichers
            tracing.AddTraceEnricher<DependabotTraceEnricher>();

            // add the exporters
            if (appInsights) tracing.AddAzureMonitorTraceExporter(ConfigureExporterOptionsForAzureMonitor);
            if (axiom) tracing.AddOtlpExporter(OtelExporterOptionsName.AxiomTraces, opt => ConfigureExporterOptionsForAxiom(opt, OtelCategory.Traces));
        });

        // add metrics
        otel.WithMetrics(metrics =>
        {
            metrics.AddHttpClientInstrumentation();
            metrics.AddProcessInstrumentation();
            metrics.AddRuntimeInstrumentation();
            metrics.AddAspNetCoreInstrumentation();

            // add the exporters
            if (appInsights) metrics.AddAzureMonitorMetricExporter(ConfigureExporterOptionsForAzureMonitor);
            // Axiom does not support metrics yet, see https://axiom.co/docs/send-data/opentelemetry
            //if (axiom) metrics.AddOtlpExporter(OtelExporterOptionsName.AxiomMetrics, opt => ConfigureExporterOptionsForAxiom(opt, OtelCategory.Metrics));
        });

        // add logging support
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            // add the exporters
            if (appInsights) options.AddAzureMonitorLogExporter(ConfigureExporterOptionsForAzureMonitor);
            if (axiom) options.AddOtlpExporter(OtelExporterOptionsName.AxiomLogs, opt => ConfigureExporterOptionsForAxiom(opt, OtelCategory.Logs));
        });

        return builder;
    }

    private enum OtelCategory { Traces, Metrics, Logs, }
    private readonly struct OtelExporterOptionsName
    {
        public enum OtelProviderKind { Axiom, }

        private readonly string name;

        public OtelExporterOptionsName() => throw new InvalidOperationException("This struct should not be instantiated directly."); // prevent instantiation
        private OtelExporterOptionsName(OtelProviderKind kind, OtelCategory category) => name = $"{kind}-{category}".ToLowerInvariant();

        public override string ToString() => name;

        public static implicit operator string(OtelExporterOptionsName name) => name.ToString();

        public static readonly OtelExporterOptionsName AxiomTraces = new(OtelProviderKind.Axiom, OtelCategory.Traces);
        public static readonly OtelExporterOptionsName AxiomMetrics = new(OtelProviderKind.Axiom, OtelCategory.Metrics);
        public static readonly OtelExporterOptionsName AxiomLogs = new(OtelProviderKind.Axiom, OtelCategory.Logs);
    }
}
