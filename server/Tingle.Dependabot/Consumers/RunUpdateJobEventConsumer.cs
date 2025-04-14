using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Consumers;

internal class RunUpdateJobEventConsumer(MainDbContext dbContext, UpdateRunner runner, ILogger<RunUpdateJobEventConsumer> logger) : IEventConsumer<RunUpdateJobEvent>
{
    public async Task ConsumeAsync(EventContext<RunUpdateJobEvent> context, CancellationToken cancellationToken)
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
        var repositoryUpdateId = evt.RepositoryUpdateId;
        var update = repository.Updates.ElementAtOrDefault(repositoryUpdateId);
        if (update is null)
        {
            logger.SkippingTriggerRepositoryUpdateNotFound(repositoryId: repositoryId, repositoryUpdateId, projectId: project.Id);
            return;
        }

        // trigger each update
        var eventBusId = context.Id;
        var ecosystem = update.PackageEcosystem;

        // check if there is an existing one
        var job = await (from j in dbContext.UpdateJobs
                         where j.PackageEcosystem == ecosystem
                         where j.Directory == update.Directory
                         where j.Directories == update.Directories
                         where j.EventBusId == eventBusId
                         select j).SingleOrDefaultAsync(cancellationToken);
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
            var jobId = $"{SequenceNumber.Generate()}";
            job = new UpdateJob
            {
                Id = jobId,
                Created = DateTimeOffset.UtcNow,
                Status = UpdateJobStatus.Running,
                Trigger = evt.Trigger,

                ProjectId = project.Id,
                RepositoryId = repository.Id,
                RepositorySlug = repository.Slug,
                EventBusId = eventBusId,

                Commit = repository.LatestCommit,
                PackageEcosystem = ecosystem,
                Directory = update.Directory,
                Directories = update.Directories,
                Resources = resources,
                AuthKey = Keygen.Create(32, Keygen.OutputFormat.Base62),

                ProxyImage = null,
                UpdaterImage = null,
                LogsPath = null,
                FlameGraphPath = null,

                Start = null,
                End = null,
                Duration = null,
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
        var updaterContext = new UpdaterContext
        {
            Project = project,
            Repository = repository,
            Update = update,
            Job = job,

            UpdatingPullRequest = false, // TODO: fix this
            UpdateDependencyGroupName = null, // TODO: fix this
            UpdateDependencyNames = [], // TODO: fix this
        };
        await runner.CreateAsync(updaterContext, cancellationToken);

        // save changes made by the runner
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
