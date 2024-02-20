using Microsoft.ApplicationInsights;

namespace Tingle.Dependabot.ApplicationInsights;

// from https://medium.com/@asimmon/prevent-net-application-insights-telemetry-loss-d82a06c3673f
internal class InsightsShutdownFlushService(TelemetryClient telemetryClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Flush the remaining telemetry data when application shutdown is requested.
        // Using "CancellationToken.None" ensures that the application doesn't stop until the telemetry data is flushed.
        //
        // If you want to use the "cancellationToken" argument, make sure to configure "HostOptions.ShutdownTimeout" with a sufficiently large duration,
        // and silence the eventual "OperationCanceledException" exception. Otherwise, you will still be at risk of losing telemetry data.
        var successfullyFlushed = await telemetryClient.FlushAsync(CancellationToken.None);
        if (!successfullyFlushed)
        {
            // Here you can handle th case where transfer of telemetry data to the server has failed with HTTP status that cannot be retried.
        }
    }
}
