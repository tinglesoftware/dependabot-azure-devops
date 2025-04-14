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

namespace Tingle.Dependabot.Tests.PeriodicTasks;

public class MissedTriggerCheckerTaskTests(ITestOutputHelper outputHelper)
{
    private const string ProjectId = "prj_1234567890";
    private const string RepositoryId = "repo_1234567890";
    private const int UpdateId1 = 1;

    [Fact]
    public async Task CheckAsync_MissedScheduleIsDetected()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = DateTimeOffset.Parse("2023-01-23T03:30:00+00:00");
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, pt) =>
        {
            await pt.CheckAsync(referencePoint, TestContext.Current.CancellationToken);

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<RunUpdateJobEvent>>(
                Assert.Single(await harness.PublishedAsync(cancellationToken: TestContext.Current.CancellationToken)));
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
            await pt.CheckAsync(referencePoint, TestContext.Current.CancellationToken);

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<RunUpdateJobEvent>>(
                Assert.Single(await harness.PublishedAsync(cancellationToken: TestContext.Current.CancellationToken)));
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
            await pt.CheckAsync(referencePoint, TestContext.Current.CancellationToken);

            // Ensure nothing was published
            Assert.Empty(await harness.PublishedAsync(cancellationToken: TestContext.Current.CancellationToken));
        });
    }

    private async Task TestAsync(DateTimeOffset? lastUpdate0, DateTimeOffset? lastUpdate1, Func<InMemoryTestHarness, MainDbContext, MissedTriggerCheckerTask, Task> executeAndVerify)
    {
        using var dbFixture = new DbFixture();

        var host = Host.CreateDefaultBuilder()
                       .ConfigureLogging(builder => builder.AddXUnit(outputHelper))
                       .ConfigureServices((context, services) =>
                       {
                           services.AddDbContext<MainDbContext>(options =>
                           {
                               options.UseSqlite(dbFixture.ConnectionString);
                               options.EnableDetailedErrors();
                           });
                           services.AddEventBus(builder => builder.AddInMemoryTransport().AddInMemoryTestHarness());
                       })
                       .Build();

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<MainDbContext>();
        await context.Database.MigrateAsync();

        await context.Projects.AddAsync(new Project
        {
            Id = ProjectId,
            Url = "https://dev.azure.com/dependabot/dependabot",
            Token = "token",
            UserId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Name = "dependabot",
            ProviderId = "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
            Password = "burp-bump",
            AutoApprove = new(),
            AutoComplete = new(),
        });
        await context.Repositories.AddAsync(new Repository
        {
            Id = RepositoryId,
            ProjectId = ProjectId,
            ProviderId = Guid.NewGuid().ToString(),
            Name = "test-repo",
            ConfigFileContents = "",
            Updates =
            [
                new RepositoryUpdate
                {
                    PackageEcosystem = "npm",
                    Directory = "/",
                    Directories = null,
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
                    Directory = null,
                    Directories = ["/legacy"],
                    Schedule = new DependabotUpdateSchedule
                    {
                        Interval = DependabotScheduleInterval.Daily,
                        Time = new(3, 30),
                    },
                    LatestUpdate = lastUpdate1,
                },
            ],
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
