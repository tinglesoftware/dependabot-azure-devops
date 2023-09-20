using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Workflow;

internal class SynchronizationTask : IPeriodicTask
{
    private readonly MainDbContext dbContext;
    private readonly IEventPublisher publisher;

    public SynchronizationTask(MainDbContext dbContext, IEventPublisher publisher)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await SyncAsync(cancellationToken);
    }

    internal virtual async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        // request synchronization of the each project via events
        var projects = await dbContext.Projects.ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            var evt = new ProcessSynchronization(project.Id!, false); /* database sync should not trigger, just in case it's too many */
            await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
        }
    }
}
