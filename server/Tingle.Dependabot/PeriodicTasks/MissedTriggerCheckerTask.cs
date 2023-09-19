using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.PeriodicTasks;

internal class MissedTriggerCheckerTask : IPeriodicTask
{
    private readonly MainDbContext dbContext;
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    public MissedTriggerCheckerTask(MainDbContext dbContext, IEventPublisher publisher, ILogger<MissedTriggerCheckerTask> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await CheckAsync(DateTimeOffset.UtcNow, cancellationToken);
    }

    internal virtual async Task CheckAsync(DateTimeOffset referencePoint, CancellationToken cancellationToken = default)
    {
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
}
