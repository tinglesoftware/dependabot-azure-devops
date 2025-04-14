using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

internal class ScenarioStore(ILogger<ScenarioStore> logger)
{
    private readonly DependabotConcurrentDictionary<string, (SemaphoreSlim, List<ScenarioOutput>)> store = new();

    static Task<(SemaphoreSlim, List<ScenarioOutput>)> StoreItemCreator(string jobId, CancellationToken ct)
        => Task.FromResult((new SemaphoreSlim(1), new List<ScenarioOutput>()));

    public async Task AddAsync(string id, string type, object input, CancellationToken cancellationToken = default)
    {
        var (limiter, outputs) = await store.GetOrAddAsync(id, StoreItemCreator, cancellationToken);
        await limiter.WaitAsync(cancellationToken);
        outputs.Add(new(type, input));
        limiter.Release();
    }

    public async Task ApplyAsync(ScenarioApplicationContext context, CancellationToken cancellationToken = default)
    {
        var job = context.Job;
        var scenarios = await RemoveAsync(job.Id, cancellationToken);

        // TODO: handle the scenarios that have been output

        // for record_update_job_error
        // job.Error = new UpdateJobError
        // {
        //     Type = model.Data!.ErrorType,
        //     Detail = model.Data.ErrorDetails,
        // };

        // for record_update_job_unknown_error
        // job.Error = new UpdateJobError
        // {
        //     Type = model.Data!.ErrorType,
        //     Detail = model.Data.ErrorDetails,
        // };

        // for update_dependency_list
        // if (update is not null)
        // {
        //     update.Files = model.Data.DependencyFiles ?? [];
        // }
    }

    private async Task<List<ScenarioOutput>> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        if (store.Remove(id, out var item))
        {
            var (limiter, outputs) = await item.WaitAsync(cancellationToken);
            limiter.Dispose();
            return outputs;
        }
        return [];
    }
}

public record ScenarioOutput(string Type, object Expect);

public readonly struct ScenarioApplicationContext
{
    public required Project Project { get; init; }
    public required Repository Repository { get; init; }
    public required RepositoryUpdate Update { get; init; }
    public required UpdateJob Job { get; init; }
}
