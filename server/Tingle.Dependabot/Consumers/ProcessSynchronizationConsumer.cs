using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class ProcessSynchronizationConsumer : IEventConsumer<ProcessSynchronization>
{
    private readonly MainDbContext dbContext;
    private readonly Synchronizer synchronizer;
    private readonly ILogger logger;

    public ProcessSynchronizationConsumer(MainDbContext dbContext, Synchronizer synchronizer, ILogger<ProcessSynchronizationConsumer> logger)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConsumeAsync(EventContext<ProcessSynchronization> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;

        var trigger = evt.Trigger;

        if (evt.RepositoryId is not null)
        {
            // ensure repository exists
            var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
            var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
            if (repository is null)
            {
                logger.LogWarning("Skipping synchronization because repository '{Repository}' does not exist.", repositoryId);
                return;
            }

            await synchronizer.SynchronizeAsync(repository, trigger, cancellationToken);
        }
        else if (evt.RepositoryProviderId is not null)
        {
            await synchronizer.SynchronizeAsync(repositoryProviderId: evt.RepositoryProviderId, trigger, cancellationToken);
        }
        else
        {
            await synchronizer.SynchronizeAsync(evt.Trigger, cancellationToken);
        }
    }
}
