using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.PeriodicTasks;

internal class MissedTriggerCheckerTask(MainDbContext dbContext, IEventPublisher publisher, ILogger<MissedTriggerCheckerTask> logger) : IPeriodicTask
{
    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await CheckAsync(DateTimeOffset.UtcNow, cancellationToken);
    }

    internal virtual async Task CheckAsync(DateTimeOffset referencePoint, CancellationToken cancellationToken = default)
    {
        var projects = await dbContext.Projects.ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            var repositories = await dbContext.Repositories.Where(r => r.ProjectId == project.Id).ToListAsync(cancellationToken);

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
                        logger.ScheduleTriggerMissed(repositoryId: repository.Id, updateId: repository.Updates.IndexOf(update), projectId: project.Id);

                        // publish event for the job to be run
                        var evt = new TriggerUpdateJobsEvent
                        {
                            ProjectId = repository.ProjectId,
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
}
