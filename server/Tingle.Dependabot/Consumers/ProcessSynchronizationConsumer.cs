using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class ProcessSynchronizationConsumer(MainDbContext dbContext, ISynchronizer synchronizer, ILogger<ProcessSynchronizationConsumer> logger) : IEventConsumer<ProcessSynchronization>
{
    public async Task ConsumeAsync(EventContext<ProcessSynchronization> context, CancellationToken cancellationToken = default)
    {
        var evt = context.Event;
        var trigger = evt.Trigger;

        // ensure project exists
        var projectId = evt.ProjectId ?? throw new InvalidOperationException($"'{nameof(evt.ProjectId)}' cannot be null");
        var project = await dbContext.Projects.SingleOrDefaultAsync(r => r.Id == projectId, cancellationToken);
        if (project is null)
        {
            logger.SkippingSyncProjectNotFound(projectId);
            return;
        }

        if (evt.RepositoryId is not null)
        {
            // ensure repository exists
            var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
            var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.ProjectId == project.Id && r.Id == repositoryId, cancellationToken);
            if (repository is null)
            {
                logger.SkippingSyncRepositoryNotFound(repositoryId);
                return;
            }

            await synchronizer.SynchronizeAsync(project, repository, trigger, cancellationToken);
        }
        else if (evt.RepositoryProviderId is not null)
        {
            await synchronizer.SynchronizeAsync(project, repositoryProviderId: evt.RepositoryProviderId, trigger, cancellationToken);
        }
        else
        {
            await synchronizer.SynchronizeAsync(project, evt.Trigger, cancellationToken);
        }
    }
}
