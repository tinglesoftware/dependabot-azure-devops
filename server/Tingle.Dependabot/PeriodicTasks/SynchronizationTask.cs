using Tingle.Dependabot.Events;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Workflow;

internal class SynchronizationTask : IPeriodicTask
{
    private readonly IEventPublisher publisher;

    public SynchronizationTask(IEventPublisher publisher)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task ExecuteAsync(PeriodicTaskExecutionContext context, CancellationToken cancellationToken)
    {
        await SyncAsync(cancellationToken);
    }

    internal virtual async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        // request synchronization of the whole project via events
        var evt = new ProcessSynchronization(false); /* database sync should not trigger, just in case it's too many */
        await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
    }
}
