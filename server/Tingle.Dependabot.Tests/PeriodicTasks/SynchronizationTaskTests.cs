using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory;
using Xunit;

namespace Tingle.Dependabot.Tests.PeriodicTasks;

public class SynchronizationTaskTests(ITestOutputHelper outputHelper)
{
    private const string ProjectId = "prj_1234567890";

    [Fact]
    public async Task SynchronizationInnerAsync_Works()
    {
        await TestAsync(async (harness, pt) =>
        {
            await pt.SyncAsync(TestContext.Current.CancellationToken);

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<ProcessSynchronization>>(
                Assert.Single(await harness.PublishedAsync(cancellationToken: TestContext.Current.CancellationToken)));
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
                           var dbName = Guid.NewGuid().ToString();
                           services.AddDbContext<MainDbContext>(options =>
                           {
                               options.UseInMemoryDatabase(dbName, o => o.EnableNullChecks());
                               options.EnableDetailedErrors();
                           });
                           services.AddEventBus(builder => builder.AddInMemoryTransport().AddInMemoryTestHarness());
                       })
                       .Build();

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<MainDbContext>();
        await context.Database.EnsureCreatedAsync();

        await context.Projects.AddAsync(new Project
        {
            Id = ProjectId,
            Url = "https://dev.azure.com/dependabot/dependabot",
            Token = "token",
            Name = "dependabot",
            ProviderId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Password = "burp-bump",
        });
        await context.SaveChangesAsync();

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
