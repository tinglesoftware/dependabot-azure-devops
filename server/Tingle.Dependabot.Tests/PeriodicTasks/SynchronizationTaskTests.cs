using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory;
using Xunit;
using Xunit.Abstractions;

namespace Tingle.Dependabot.Tests.PeriodicTasks;

public class SynchronizationTaskTests
{
    private readonly ITestOutputHelper outputHelper;

    public SynchronizationTaskTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task SynchronizationInnerAsync_Works()
    {
        await TestAsync(async (harness, pt) =>
        {
            await pt.SyncAsync();

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<ProcessSynchronization>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Null(inner.RepositoryId);
            Assert.Null(inner.RepositoryProviderId);
            Assert.False(inner.Trigger);
        });
    }

    private async Task TestAsync(Func<InMemoryTestHarness, SynchronizationTask, Task> executeAndVerify)
    {
        var host = Host.CreateDefaultBuilder()
                       .ConfigureLogging(builder => builder.AddXUnit(outputHelper))
                       .ConfigureServices((context, services) =>
                       {
                           services.AddEventBus(builder => builder.AddInMemoryTransport().AddInMemoryTestHarness());
                       })
                       .Build();

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync();

        try
        {
            var pt = ActivatorUtilities.GetServiceOrCreateInstance<SynchronizationTask>(provider);

            await executeAndVerify(harness, pt);

            // Ensure there were no publish failures
            Assert.Empty(await harness.FailedAsync());
        }
        finally
        {
            await harness.StopAsync();
        }
    }
}
