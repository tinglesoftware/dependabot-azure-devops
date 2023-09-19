using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.EventBus.Transports.InMemory;
using Xunit;
using Xunit.Abstractions;

namespace Tingle.Dependabot.Tests.Workflow;

public class WorkflowBackgroundServiceTests
{
    private const string RepositoryId = "repo_1234567890";
    private const int UpdateId1 = 1;

    private readonly ITestOutputHelper outputHelper;

    public WorkflowBackgroundServiceTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task SynchronizationInnerAsync_Works()
    {
        await TestAsync(async (harness, context, service) =>
        {
            await service.SynchronizationInnerAsync();

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<ProcessSynchronization>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Null(inner.RepositoryId);
            Assert.Null(inner.RepositoryProviderId);
            Assert.False(inner.Trigger);
        });
    }

    [Fact]
    public async Task CheckMissedTriggerInnerAsync_MissedScheduleIsDetected()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = DateTimeOffset.Parse("2023-01-23T03:30:00+00:00");
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, service) =>
        {
            await service.CheckMissedTriggerInnerAsync(referencePoint);

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
    public async Task CheckMissedTriggerInnerAsync_MissedScheduleIsDetected_NotRun_Before()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = (DateTimeOffset?)null;
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, service) =>
        {
            await service.CheckMissedTriggerInnerAsync(referencePoint);

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
    public async Task CheckMissedTriggerInnerAsync_NoMissedSchedule()
    {
        var referencePoint = DateTimeOffset.Parse("2023-01-24T05:00:00+00:00");
        var lastUpdate0 = DateTimeOffset.Parse("2023-01-24T03:45:00+00:00");
        var lastUpdate1 = DateTimeOffset.Parse("2023-01-24T03:30:00+00:00");
        await TestAsync(lastUpdate0, lastUpdate1, async (harness, context, service) =>
        {
            await service.CheckMissedTriggerInnerAsync(referencePoint);

            // Ensure nothing was published
            Assert.Empty(await harness.PublishedAsync());
        });
    }


    [Fact]
    public async Task CleanupInnerAsync_ResolvesJobs()
    {
        await TestAsync(async (harness, context, job) =>
        {
            var targetId = Guid.NewGuid().ToString();
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddMinutes(-19),
                PackageEcosystem = "npm",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Succeeded,
            });
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddHours(-100),
                PackageEcosystem = "nuget",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Succeeded,
            });
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = targetId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddMinutes(-30),
                PackageEcosystem = "docker",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Running,
            });
            await context.SaveChangesAsync();

            await job.CleanupInnerAsync();

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<UpdateJobCheckStateEvent>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Equal(targetId, inner.JobId);
        });
    }

    [Fact]
    public async Task CleanupInnerAsync_DeletesOldJobsAsync()
    {
        await TestAsync(async (harness, context, job) =>
        {
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-80),
                PackageEcosystem = "npm",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            });
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-100),
                PackageEcosystem = "nuget",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            });
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-120),
                PackageEcosystem = "docker",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            });
            await context.SaveChangesAsync();

            await job.CleanupInnerAsync();
            Assert.Equal(1, await context.UpdateJobs.CountAsync());
        });
    }

    private Task TestAsync(Func<InMemoryTestHarness, MainDbContext, WorkflowBackgroundService, Task> executeAndVerify) => TestAsync(null, null, executeAndVerify);

    private async Task TestAsync(DateTimeOffset? lastUpdate0, DateTimeOffset? lastUpdate1, Func<InMemoryTestHarness, MainDbContext, WorkflowBackgroundService, Task> executeAndVerify)
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
            var service = ActivatorUtilities.GetServiceOrCreateInstance<WorkflowBackgroundService>(provider);

            await executeAndVerify(harness, context, service);

            // Ensure there were no publish failures
            Assert.Empty(await harness.FailedAsync());
        }
        finally
        {
            await harness.StopAsync();
        }
    }
}
