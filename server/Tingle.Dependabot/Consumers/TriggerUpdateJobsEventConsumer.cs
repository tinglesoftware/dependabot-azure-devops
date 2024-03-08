using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Consumers;

internal class TriggerUpdateJobsEventConsumer(MainDbContext dbContext, UpdateRunner updateRunner, ILogger<TriggerUpdateJobsEventConsumer> logger) : IEventConsumer<TriggerUpdateJobsEvent>
{
    public async Task ConsumeAsync(EventContext<TriggerUpdateJobsEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // ensure project exists
        var projectId = evt.ProjectId ?? throw new InvalidOperationException($"'{nameof(evt.ProjectId)}' cannot be null");
        var project = await dbContext.Projects.SingleOrDefaultAsync(r => r.Id == projectId, cancellationToken);
        if (project is null)
        {
            logger.SkippingTriggerProjectNotFound(projectId);
            return;
        }

        // ensure repository exists
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            logger.SkippingTriggerRepositoryNotFound(repositoryId: repositoryId, projectId: project.Id);
            return;
        }

        // if we have a specific update to trigger, use it otherwise, do all
        IList<RepositoryUpdate>? updates = null;
        var repositoryUpdateId = evt.RepositoryUpdateId;
        if (repositoryUpdateId is not null)
        {
            var update = repository.Updates.ElementAtOrDefault(repositoryUpdateId.Value);
            if (update is null)
            {
                logger.SkippingTriggerRepositoryUpdateNotFound(repositoryId: repositoryId, repositoryUpdateId.Value, projectId: project.Id);
                return;
            }
            updates = new[] { update, };
        }
        else
        {
            updates = repository.Updates.ToList();
        }

        // trigger each update
        var eventBusId = context.Id;
        foreach (var update in updates)
        {
            var ecosystem = update.PackageEcosystem!;

            // check if there is an existing one
            var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.PackageEcosystem == ecosystem && j.Directory == update.Directory && j.EventBusId == eventBusId, cancellationToken);
            if (job is not null)
            {
                logger.SkippingTriggerJobAlreadyExists(repositoryId: repository.Id,
                                                       repositoryUpdateId: repository.Updates.IndexOf(update),
                                                       projectId: project.Id,
                                                       eventBusId: eventBusId);
            }
            else
            {
                // decide the resources based on the ecosystem
                var resources = UpdateJobResources.FromEcosystem(ecosystem);

                // create the job
                job = new UpdateJob
                {
                    // we use this to create azure resources which have name restrictions
                    // alphanumeric, starts with a letter, does not contain "--", up to 32 characters
                    Id = $"job-{SequenceNumber.Generate()}", // sequence number is 19 chars, total is 23 chars

                    Created = DateTimeOffset.UtcNow,
                    Status = UpdateJobStatus.Scheduled,
                    Trigger = evt.Trigger,

                    ProjectId = project.Id,
                    RepositoryId = repository.Id,
                    RepositorySlug = repository.Slug,
                    EventBusId = eventBusId,

                    Commit = repository.LatestCommit,
                    PackageEcosystem = ecosystem,
                    Directory = update.Directory,
                    Resources = resources,
                    AuthKey = Guid.NewGuid().ToString("n"),

                    Start = null,
                    End = null,
                    Duration = null,
                    Log = null,
                    Error = null,
                };
                await dbContext.UpdateJobs.AddAsync(job, cancellationToken);

                // update the RepositoryUpdate
                update.LatestJobId = job.Id;
                update.LatestJobStatus = job.Status;
                update.LatestUpdate = job.Created;

                // save to the database
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // call the update runner to run the update
            await updateRunner.CreateAsync(project, repository, update, job, cancellationToken);

            // save changes that may have been made by the updateRunner
            update.LatestJobStatus = job.Status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
