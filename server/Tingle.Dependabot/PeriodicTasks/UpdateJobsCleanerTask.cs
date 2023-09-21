using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.PeriodicTasks;

internal class UpdateJobsCleanerTask : IPeriodicTask
{
    private readonly MainDbContext dbContext;
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    public UpdateJobsCleanerTask(MainDbContext dbContext, IEventPublisher publisher, ILogger<MissedTriggerCheckerTask> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await CleanupAsync(cancellationToken);
    }

    internal virtual async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
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
            logger.UpdateJobRequestingManualResolution(jobs.Count);

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
            logger.UpdateJobRemovedOldJobs(jobs.Count, cutoff);
        }
    }
}
