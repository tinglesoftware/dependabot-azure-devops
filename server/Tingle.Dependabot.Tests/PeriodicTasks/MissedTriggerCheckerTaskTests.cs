using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.PeriodicTasks;
using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory;
using Xunit;
using Xunit.Abstractions;

namespace Tingle.Dependabot.Tests.PeriodicTasks;

public class MissedTriggerCheckerTaskTests
{
    private const string RepositoryId = "repo_1234567890";
    private const int UpdateId1 = 1;

    private readonly ITestOutputHelper outputHelper;

    public MissedTriggerCheckerTaskTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task CheckAsync_MissedScheduleIsDetected()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = DateTimeOffset.Parse("2023-01-23T03:30:00+00:00");
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, pt) =>
        {
            await pt.CheckAsync(referencePoint);

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<TriggerUpdateJobsEvent>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Equal(RepositoryId, inner.RepositoryId);
            Assert.Equal(UpdateId1, inner.RepositoryUpdateId);
            Assert.Equal(UpdateJobTrigger.MissedSchedule, inner.Trigger);
        });
    }

    [Fact]
    public async Task CheckAsync_MissedScheduleIsDetected_NotRun_Before()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = (DateTimeOffset?)null;
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, pt) =>
        {
            await pt.CheckAsync(referencePoint);

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<TriggerUpdateJobsEvent>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Equal(RepositoryId, inner.RepositoryId);
            Assert.Equal(UpdateId1, inner.RepositoryUpdateId);
            Assert.Equal(UpdateJobTrigger.MissedSchedule, inner.Trigger);
        });
    }

    [Fact]
    public async Task CheckAsync_NoMissedSchedule()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = DateTimeOffset.Parse("2023-01-24T03:30:00+00:00");
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, pt) =>
        {
            await pt.CheckAsync(referencePoint);

            // Ensure nothing was published
            Assert.Empty(await harness.PublishedAsync());
        });
    }

    private async Task TestAsync(DateTimeOffset? lastUpdate0, DateTimeOffset? lastUpdate1, Func<InMemoryTestHarness, MainDbContext, MissedTriggerCheckerTask, Task> executeAndVerify)
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

        await context.Repositories.AddAsync(new Repository
        {
            Id = RepositoryId,
            Name = "test-repo",
            ConfigFileContents = "",
            Updates = new List<RepositoryUpdate>
            {
                new RepositoryUpdate
                {
                    PackageEcosystem = "npm",
                    Directory = "/",
                    Schedule = new DependabotUpdateSchedule
                    {
                        Interval = DependabotScheduleInterval.Daily,
                        Time = new(3, 45),
                    },
                    LatestUpdate = lastUpdate0,
                },
                new RepositoryUpdate
                {
                    PackageEcosystem = "npm",
                    Directory = "/legacy",
                    Schedule = new DependabotUpdateSchedule
                    {
                        Interval = DependabotScheduleInterval.Daily,
                        Time = new(3, 30),
                    },
                    LatestUpdate = lastUpdate1,
                },
            },
        });
        await context.SaveChangesAsync();

        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync();

        try
        {
            var pt = ActivatorUtilities.GetServiceOrCreateInstance<MissedTriggerCheckerTask>(provider);

            await executeAndVerify(harness, context, pt);

            // Ensure there were no publish failures
            Assert.Empty(await harness.FailedAsync());
        }
        finally
        {
            await harness.StopAsync();
        }
    }
}
