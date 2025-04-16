using Microsoft.AspNetCore.Mvc;
using MiniValidation;
using Tingle.Dependabot;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Workflow;

namespace Microsoft.AspNetCore.Builder;

public static class UpdateJobsEndpoints
{
    public static IEndpointConventionBuilder MapUpdateJobs(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/update_jobs")
                             .WithGroupName("update_jobs")
                             .RequireAuthorization(AuthConstants.PolicyNameUpdater);

        group.MapOperation<DependabotCreatePullRequest>(DependabotOperationType.CreatePullRequest);
        group.MapOperation<DependabotUpdatePullRequest>(DependabotOperationType.UpdatePullRequest);
        group.MapOperation<DependabotClosePullRequest>(DependabotOperationType.ClosePullRequest);
        group.MapOperation<DependabotRecordUpdateJobError>(DependabotOperationType.RecordUpdateJobError);
        group.MapOperation<DependabotRecordUpdateJobUnknownError>(DependabotOperationType.RecordUpdateJobUnknownError);
        group.MapOperation<DependabotMarkAsProcessed>(DependabotOperationType.MarkAsProcessed, [HttpMethods.Patch]);
        group.MapOperation<DependabotUpdateDependencyList>(DependabotOperationType.UpdateDependencyList);
        group.MapOperation<DependabotRecordEcosystemVersions>(DependabotOperationType.RecordEcosystemVersions);
        group.MapOperation<DependabotIncrementMetric>(DependabotOperationType.IncrementMetric);

        return group;
    }

    private static void MapOperation<T>(this IEndpointRouteBuilder builder, DependabotOperationType type, string[]? methods = null)
    {
        builder.MapMethods($"{{id}}/{type.GetEnumMemberAttrValue()}",
                           methods ?? [HttpMethods.Post],
                           async ([FromRoute] string id, [FromBody] DependabotRequest<T> input, [FromServices] IScenarioStore store) =>
                           {
                               if (!MiniValidator.TryValidate(input, out var errors)) return Results.ValidationProblem(errors);
                               await store.AddAsync(id, type, input);
                               return Results.Ok();
                           });
    }
}
