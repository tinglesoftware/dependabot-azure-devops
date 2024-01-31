using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Workflow;

internal class SynchronizationTask(MainDbContext dbContext, IEventPublisher publisher) : IPeriodicTask
{
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
