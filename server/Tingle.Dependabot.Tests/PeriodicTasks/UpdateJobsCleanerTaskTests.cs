using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.PeriodicTasks;
using Xunit;

namespace Tingle.Dependabot.Tests.PeriodicTasks;

public class UpdateJobsCleanerTaskTests(ITestOutputHelper outputHelper)
{
    private const string ProjectId = "prj_1234567890";
    private const string RepositoryId = "repo_1234567890";

    [Fact]
    public async Task DeleteHangingAsync_Works()
    {
        await TestAsync(async (context, pt) =>
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
                PackageManager = "npm_and_yarn",
                Directory = "/",
                Directories = null,
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Succeeded,
            }, TestContext.Current.CancellationToken);
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddHours(-100),
                PackageEcosystem = "nuget",
                PackageManager = "nuget",
                Directory = "/",
                Directories = null,
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Running,
            }, TestContext.Current.CancellationToken);
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = targetId,
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddMinutes(-30),
                PackageEcosystem = "docker",
                PackageManager = "docker",
                Directory = null,
                Directories = ["**/*"],
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
                Status = UpdateJobStatus.Succeeded,
            }, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await pt.DeleteHangingAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, await context.UpdateJobs.CountAsync(TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task DeleteOldJobsAsync_Works()
    {
        await TestAsync(async (context, pt) =>
        {
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-80),
                PackageEcosystem = "npm",
                PackageManager = "npm_and_yarn",
                Directory = "/",
                Directories = null,
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            }, TestContext.Current.CancellationToken);
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-100),
                PackageEcosystem = "nuget",
                PackageManager = "nuget",
                Directory = "/",
                Directories = null,
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            }, TestContext.Current.CancellationToken);
            await context.UpdateJobs.AddAsync(new UpdateJob
            {
                Id = Guid.NewGuid().ToString(),
                ProjectId = ProjectId,
                RepositoryId = RepositoryId,
                RepositorySlug = "test-repo",
                Created = DateTimeOffset.UtcNow.AddDays(-120),
                PackageEcosystem = "docker",
                PackageManager = "docker",
                Directory = null,
                Directories = ["**/*"],
                Resources = new(0.25, 0.2),
                AuthKey = Guid.NewGuid().ToString("n"),
            }, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await pt.DeleteOldJobsAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, await context.UpdateJobs.CountAsync(TestContext.Current.CancellationToken));
        });
    }

    private async Task TestAsync(Func<MainDbContext, UpdateJobsCleanerTask, Task> executeAndVerify)
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
                },
            ],
        });
        await context.SaveChangesAsync();

        var pt = ActivatorUtilities.GetServiceOrCreateInstance<UpdateJobsCleanerTask>(provider);
        await executeAndVerify(context, pt);
    }
}
