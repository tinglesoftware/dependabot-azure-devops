using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class RepositoryEventsConsumer(MainDbContext dbContext, UpdateScheduler scheduler) : IEventConsumer<RepositoryCreatedEvent>, IEventConsumer<RepositoryUpdatedEvent>, IEventConsumer<RepositoryDeletedEvent>
{
    public async Task ConsumeAsync(EventContext<RepositoryCreatedEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // update scheduler
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == repositoryId, cancellationToken);
        await scheduler.CreateOrUpdateAsync(repository, cancellationToken);
    }

    public async Task ConsumeAsync(EventContext<RepositoryUpdatedEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // update scheduler
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == repositoryId, cancellationToken);
        await scheduler.CreateOrUpdateAsync(repository, cancellationToken);
    }

    public async Task ConsumeAsync(EventContext<RepositoryDeletedEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // remove from scheduler
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        await scheduler.RemoveAsync(repositoryId, cancellationToken);
    }
}
