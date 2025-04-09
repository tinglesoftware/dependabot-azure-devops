using System.Collections.Concurrent;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models.Management;
using Tingle.EventBus;
using Tingle.PeriodicTasks;

namespace Tingle.Dependabot.Workflow;

internal class UpdateScheduler
{
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    private ConcurrentDictionary<string, IReadOnlyList<CronScheduleTimer>> store = new();

    public UpdateScheduler(IEventPublisher publisher, IHostApplicationLifetime lifetime, ILogger<UpdateScheduler> logger)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        lifetime.ApplicationStopping.Register(() =>
        {
            // stop all timers in 1 second, max
            var cached = Interlocked.Exchange(ref store, null!);
            var timers = cached.Values.ToArray().SelectMany(l => l).ToArray();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            Task.WaitAll([.. timers.Select(t => t.StopAsync(cts.Token))]);
        });
    }

    public async Task CreateOrUpdateAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        logger.SchedulesUpdating(repositoryId: repository.Id, projectId: repository.ProjectId);
        var updates = new List<SchedulableUpdate>();
        foreach (var update in repository.Updates)
        {
            updates.Add(new(repository.Updates.IndexOf(update), update.Schedule!));
        }

        var projectId = repository.ProjectId!;
        var repositoryId = repository.Id!;
        var timers = new List<CronScheduleTimer>();
        foreach (var (index, supplied) in updates)
        {
            var schedule = supplied.GenerateCron();
            var payload = new TimerPayload(projectId, repositoryId, index);
            var timer = new CronScheduleTimer(schedule, supplied.Timezone, CustomTimerCallback, payload);
            timers.Add(timer);
        }

        // remove existing then add the new ones
        await RemoveAsync(repositoryId, cancellationToken);
        store[repositoryId] = timers;

        // start all the timers
        await Task.WhenAll(timers.Select(t => t.StartAsync(cancellationToken)));
    }

    public async Task RemoveAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        // remove existing ones
        if (store.TryGetValue(repositoryId, out var timers))
        {
            // stop all the timers
            await Task.WhenAll(timers.Select(t => t.StopAsync(cancellationToken)));

            // dispose all the timers
            foreach (var timer in timers) timer.Dispose();
        }
    }

    private async Task CustomTimerCallback(CronScheduleTimer timer, object? arg2, CancellationToken cancellationToken)
    {
        if (arg2 is not TimerPayload payload)
        {
            logger.SchedulesTimerInvalidCallbackArgument(typeof(TimerPayload).FullName, arg2?.GetType().FullName);
            return;
        }

        // publish event for the job to be run
        var evt = new TriggerUpdateJobsEvent
        {
            ProjectId = payload.ProjectId,
            RepositoryId = payload.RepositoryId,
            RepositoryUpdateId = payload.RepositoryUpdateId,
            Trigger = UpdateJobTrigger.Scheduled,
        };

        await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
    }

    private readonly record struct TimerPayload(string ProjectId, string RepositoryId, int RepositoryUpdateId);
}
