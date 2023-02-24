using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class TriggerUpdateJobsEventConsumer : IEventConsumer<TriggerUpdateJobsEvent>
{
    private readonly MainDbContext dbContext;
    private readonly UpdateRunner updateRunner;
    private readonly ILogger logger;

    public TriggerUpdateJobsEventConsumer(MainDbContext dbContext, UpdateRunner updateRunner, ILogger<TriggerUpdateJobsEventConsumer> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.updateRunner = updateRunner ?? throw new ArgumentNullException(nameof(updateRunner));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<TriggerUpdateJobsEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // ensure repository exists
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            logger.LogWarning("Skipping trigger for update because repository '{Repository}' does not exist.", repositoryId);
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
                logger.LogWarning("Skipping trigger for update because repository update '{RepositoryUpdateId}' does not exist.", repositoryUpdateId);
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
            // check if there is an existing one
            var job = await dbContext.UpdateJobs.SingleOrDefaultAsync(j => j.PackageEcosystem == update.PackageEcosystem && j.Directory == update.Directory && j.EventBusId == eventBusId, cancellationToken);
            if (job is not null)
            {
                logger.LogWarning("A job for update '{RepositoryId}({UpdateId})' requested by event '{EventBusId}' already exists. Skipping it ...",
                                  repository.Id,
                                  repository.Updates.IndexOf(update),
                                  eventBusId);
            }
            else
            {
                // decide the resources based on the ecosystem
                var ecosystem = update.PackageEcosystem!.Value;
                var resources = UpdateJobResources.FromEcosystem(ecosystem);

                // create the job
                job = new UpdateJob
                {
                    Id = FlakeId.Id.Create().ToString(),

                    Created = DateTimeOffset.UtcNow,
                    Status = UpdateJobStatus.Scheduled,
                    Trigger = evt.Trigger,

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
            await updateRunner.CreateAsync(repository, update, job, cancellationToken);

            // save changes that may have been made by the updateRunner
            update.LatestJobStatus = job.Status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
