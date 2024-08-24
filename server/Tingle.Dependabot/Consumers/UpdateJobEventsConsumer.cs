using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class UpdateJobEventsConsumer(MainDbContext dbContext, UpdateRunner updateRunner, ILogger<UpdateJobEventsConsumer> logger) : IEventConsumer<UpdateJobCheckStateEvent>, IEventConsumer<UpdateJobCollectLogsEvent>
{
    public async Task ConsumeAsync(EventContext<UpdateJobCheckStateEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // find the job
        var jobId = evt.JobId;
        var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.UpdateJobCannotUpdateStateNotFound(jobId);
            return;
        }

        // skip jobs in a terminal state (useful when reprocessed events)
        if (job.Status is UpdateJobStatus.Succeeded or UpdateJobStatus.Failed)
        {
            logger.UpdateJobCannotUpdateStateTerminalState(job.Id);
            return;
        }

        // get the state from the runner
        var state = await updateRunner.GetStateAsync(job, cancellationToken);
        if (state is null)
        {
            logger.UpdateJobRunnerNoState(job.Id);

            // delete the job if we have been waiting for over 180 minutes and still do not have state
            var diff = DateTimeOffset.UtcNow - job.Created;
            if (diff > TimeSpan.FromMinutes(180))
            {
                logger.UpdateJobPendingTooLong(job.Id);

                // delete the run
                await updateRunner.DeleteAsync(job, cancellationToken);

                // delete from the database
                dbContext.UpdateJobs.Remove(job);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // calculate duration
        var (status, start, end) = state.Value;
        TimeSpan? duration = null;
        if (start is not null && end is not null)
        {
            var diff = end.Value - start.Value;
            duration = diff;
        }

        // update the job
        job.Status = status;
        job.Start = start;
        job.End = end;
        job.Duration = duration is null ? null : Convert.ToInt64(Math.Ceiling(duration.Value.TotalMilliseconds));

        // update the Repository with status of the latest job for the update, if it exists
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == job.RepositoryId, cancellationToken);
        if (repository is not null)
        {
            var update = (from u in repository.Updates
                          where u.PackageEcosystem == job.PackageEcosystem
                          where u.Directory == job.Directory
                          where u.Directories == job.Directories
                          select u).SingleOrDefault();
            if (update is not null && update.LatestJobId == job.Id)
            {
                update.LatestJobStatus = job.Status;
            }
        }

        // save to the database
        await dbContext.SaveChangesAsync(cancellationToken);

        // logs are sometimes not available immediately, we usually need at least 2 minutes after completion time
        // we publish an event in the future to pull the logs then delete the run
        var scheduleTime = end?.AddMinutes(2.5f); // extra half-minute for buffer
        if (scheduleTime < DateTimeOffset.UtcNow) scheduleTime = null; // no need to schedule in the past
        await context.PublishAsync(new UpdateJobCollectLogsEvent { JobId = job.Id, }, scheduleTime, cancellationToken);
    }

    public async Task ConsumeAsync(EventContext<UpdateJobCollectLogsEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // find the job
        var jobId = evt.JobId;
        var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            logger.UpdateJobCannotCollectLogsNotFound(jobId);
            return;
        }

        // ensure the job succeeded or failed
        if (job.Status is not UpdateJobStatus.Succeeded and not UpdateJobStatus.Failed)
        {
            logger.UpdateJobCannotCollectLogsInvalidStatus(job.Id, job.Status);
            return;
        }

        // pull the log and update the database
        job.Log = await updateRunner.GetLogsAsync(job, cancellationToken);
        if (string.IsNullOrWhiteSpace(job.Log)) job.Log = null; // reduces allocations later and unnecessary serialization
        await dbContext.SaveChangesAsync(cancellationToken);

        // delete the run
        await updateRunner.DeleteAsync(job, cancellationToken);
    }
}
