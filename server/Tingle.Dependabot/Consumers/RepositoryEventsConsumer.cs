using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;

namespace Tingle.Dependabot.Consumers;

internal class RepositoryEventsConsumer : IEventConsumer<RepositoryCreatedEvent>, IEventConsumer<RepositoryUpdatedEvent>, IEventConsumer<RepositoryDeletedEvent>
{
    private readonly MainDbContext dbContext;
    private readonly UpdateScheduler scheduler;

    public RepositoryEventsConsumer(MainDbContext dbContext, UpdateScheduler scheduler)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

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
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == repositoryId, cancellationToken);
        await scheduler.RemoveAsync(repositoryId, cancellationToken);
    }
}
