using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Workflow;

internal class WorkflowBackgroundService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger logger;

    public WorkflowBackgroundService(IServiceProvider serviceProvider, ILogger<WorkflowBackgroundService> logger)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var t_synch = SynchronizationAsync(stoppingToken);
        var t_missed = CheckMissedTriggerAsync(stoppingToken);
        var t_cleanup = CleanupAsync(stoppingToken);

        await Task.WhenAll(t_synch, t_missed, t_cleanup);
    }

    internal virtual async Task SynchronizationAsync(CancellationToken cancellationToken = default)
    {
        var timer = new PeriodicTimer(TimeSpan.FromHours(6));

        while (!cancellationToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(cancellationToken);
            await SynchronizationInnerAsync(cancellationToken);
        }
    }

    internal virtual async Task SynchronizationInnerAsync(CancellationToken cancellationToken = default)
    {
        // request synchronization of the whole project via events
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = new ProcessSynchronization(false); /* database sync should not trigger, just in case it's too many */
        await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
    }

    internal virtual Task CheckMissedTriggerAsync(CancellationToken cancellationToken = default) => CheckMissedTriggerAsync(DateTimeOffset.UtcNow, cancellationToken);
    internal virtual async Task CheckMissedTriggerAsync(DateTimeOffset referencePoint, CancellationToken cancellationToken = default)
    {
        var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (!cancellationToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(cancellationToken);
            await CheckMissedTriggerInnerAsync(referencePoint, cancellationToken);
        }
    }
    internal virtual async Task CheckMissedTriggerInnerAsync(DateTimeOffset referencePoint, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<MainDbContext>();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var repositories = await dbContext.Repositories.ToListAsync(cancellationToken);

        foreach (var repository in repositories)
        {
            foreach (var update in repository.Updates)
            {
                var schedule = (CronSchedule)update.Schedule!.GenerateCron();
                var timezone = TimeZoneInfo.FindSystemTimeZoneById(update.Schedule.Timezone);

                // check if we missed an execution
                var latestUpdate = update.LatestUpdate;
                var missed = latestUpdate is null; // when null, it was missed
                if (latestUpdate != null)
                {
                    var nextFromLast = schedule.GetNextOccurrence(latestUpdate.Value, timezone);
                    if (nextFromLast is null) continue;

                    var nextFromReference = schedule.GetNextOccurrence(referencePoint, timezone);
                    if (nextFromReference is null) continue;

                    missed = nextFromLast.Value <= referencePoint; // when next is in the past, it was missed

                    // for daily schedules, only check if the next is more than 12 hours away
                    if (missed && update.Schedule.Interval is DependabotScheduleInterval.Daily)
                    {
                        missed = (nextFromReference.Value - referencePoint).Hours > 12;
                    }
                }

                // if we missed an execution, trigger one
                if (missed)
                {
                    logger.LogWarning("Schedule was missed for {RepositoryId}({UpdateId}). Triggering now", repository.Id, repository.Updates.IndexOf(update));

                    // publish event for the job to be run
                    var evt = new TriggerUpdateJobsEvent
                    {
                        RepositoryId = repository.Id,
                        RepositoryUpdateId = repository.Updates.IndexOf(update),
                        Trigger = UpdateJobTrigger.MissedSchedule,
                    };

                    await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
                }
            }
        }
    }

    internal virtual async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(15)); // change to once per hour once we move to jobs in Azure ContainerApps

        while (!cancellationToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(cancellationToken);
            await CleanupInnerAsync(cancellationToken);
        }
    }
    internal virtual async Task CleanupInnerAsync(CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<MainDbContext>();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        // resolve pending jobs

        // Change this to 3 hours once we have figured out how to get events from Azure
        var oldest = DateTimeOffset.UtcNow.AddMinutes(-10); // older than 10 minutes
        var jobs = await (from j in dbContext.UpdateJobs
                          where j.Created <= oldest
                          where j.Status == UpdateJobStatus.Scheduled || j.Status == UpdateJobStatus.Running
                          orderby j.Created ascending
                          select j).Take(100).ToListAsync(cancellationToken);

        if (jobs.Count > 0)
        {
            logger.LogInformation("Found {Count} jobs that are still pending for more than 10 min. Requesting manual resolution ...", jobs.Count);

            var events = jobs.Select(j => new UpdateJobCheckStateEvent { JobId = j.Id, }).ToList();
            await publisher.PublishAsync<UpdateJobCheckStateEvent>(events, cancellationToken: cancellationToken);
        }

        // delete old jobs
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        jobs = await (from j in dbContext.UpdateJobs
                      where j.Created <= cutoff
                      orderby j.Created ascending
                      select j).Take(100).ToListAsync(cancellationToken);
        if (jobs.Count > 0)
        {
            dbContext.UpdateJobs.RemoveRange(jobs);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed {Count} jobs that older than {Cutoff}", jobs.Count, cutoff);
        }
    }
}
