using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.PeriodicTasks;

internal class UpdateJobsCleanerTask(MainDbContext dbContext, ILogger<MissedTriggerCheckerTask> logger) : IPeriodicTask
{
    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await DeleteHangingAsync(cancellationToken);
        await DeleteOldJobsAsync(cancellationToken);
    }

    internal virtual async Task DeleteHangingAsync(CancellationToken cancellationToken = default)
    {
        // delete jobs that have been running for over 180 minutes
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-180);
        var jobs = await (from j in dbContext.UpdateJobs
                          where j.Created <= cutoff
                          where j.Status == UpdateJobStatus.Running
                          orderby j.Created ascending
                          select j).Take(100).ToListAsync(cancellationToken);

        foreach (var job in jobs) dbContext.Remove(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.UpdateJobRemovedStaleJobs(jobs.Count, cutoff);
    }

    internal virtual async Task DeleteOldJobsAsync(CancellationToken cancellationToken = default)
    {
        // delete old jobs
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var jobs = await (from j in dbContext.UpdateJobs
                          where j.Created <= cutoff
                          orderby j.Created ascending
                          select j).Take(100).ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            // remove associated files
            job.DeleteFiles();

            // remove the record
            dbContext.UpdateJobs.Remove(job);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.UpdateJobRemovedOldJobs(jobs.Count, cutoff);
    }
}
