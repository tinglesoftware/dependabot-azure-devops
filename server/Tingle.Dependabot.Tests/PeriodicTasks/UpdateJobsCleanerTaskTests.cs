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

public class UpdateJobsCleanerTaskTests
{
    private const string ProjectId = "prj_1234567890";
    private const string RepositoryId = "repo_1234567890";

    private readonly ITestOutputHelper outputHelper;

    public UpdateJobsCleanerTaskTests(ITestOutputHelper outputHelper)
    {
        this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
    }

    [Fact]
    public async Task CleanupAsync_ResolvesJobs()
    {
        await TestAsync(async (harness, context, pt) =>
        {
            var targetId = Guid.NewGuid().ToString();
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
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
                ProjectId = ProjectId,
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
                ProjectId = ProjectId,
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

            await pt.CleanupAsync();

            // Ensure the message was published
            var evt_context = Assert.IsType<EventContext<UpdateJobCheckStateEvent>>(Assert.Single(await harness.PublishedAsync()));
            var inner = evt_context.Event;
            Assert.NotNull(inner);
            Assert.Equal(targetId, inner.JobId);
        });
    }

    [Fact]
    public async Task CleanupAsync_DeletesOldJobsAsync()
    {
        await TestAsync(async (harness, context, pt) =>
        {
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
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
                ProjectId = ProjectId,
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
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-120),
                PackageEcosystem = "docker",
                Directory = "/",
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            });
            await context.SaveChangesAsync();

            await pt.CleanupAsync();
            Assert.Equal(1, await context.UpdateJobs.CountAsync());
        });
    }

    private async Task TestAsync(Func<InMemoryTestHarness, MainDbContext, UpdateJobsCleanerTask, Task> executeAndVerify)
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
        await context.Repositories.AddAsync(new Repository
        {
            Id = RepositoryId,
            ProjectId = ProjectId,
            ProviderId = Guid.NewGuid().ToString(),
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
                },
            },
        });
        await context.SaveChangesAsync();

        var harness = provider.GetRequiredService<InMemoryTestHarness>();
        await harness.StartAsync();

        try
        {
            var pt = ActivatorUtilities.GetServiceOrCreateInstance<UpdateJobsCleanerTask>(provider);

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
