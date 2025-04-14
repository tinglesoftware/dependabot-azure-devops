using Microsoft.AspNetCore.Mvc;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Workflow;

namespace Microsoft.AspNetCore.Builder;

public static class UpdateJobsEndpoints
{
    public static IEndpointConventionBuilder MapUpdateJobs(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/update_jobs")
                             .WithGroupName("update_jobs");

        group.MapOperation<DependabotCreatePullRequest>("create_pull_request");
        group.MapOperation<DependabotUpdatePullRequest>("update_pull_request");
        group.MapOperation<DependabotClosePullRequest>("close_pull_request");
        group.MapOperation<DependabotRecordUpdateJobError>("record_update_job_error");
        group.MapOperation<DependabotRecordUpdateJobUnknownError>("record_update_job_unknown_error");
        group.MapOperation<DependabotMarkAsProcessed>("mark_as_processed", [HttpMethods.Patch]);
        group.MapOperation<DependabotUpdateDependencyList>("update_dependency_list");
        group.MapOperation<DependabotRecordEcosystemVersions>("record_ecosystem_versions");
        group.MapOperation<DependabotIncrementMetric>("increment_metric");

        return group;
    }

    private static void MapOperation<T>(this IEndpointRouteBuilder builder, string operation, string []? methods = null)
    {
        builder.MapMethods($"{{id}}/{operation}", methods ?? [HttpMethods.Post],
                           async ([FromRoute] string id, [FromBody] DependabotRequest<T> input, [FromServices] ScenarioStore store) =>
                           {
                               await store.AddAsync(id, operation, input);
                               return Results.Ok();
                           });
    }
}
