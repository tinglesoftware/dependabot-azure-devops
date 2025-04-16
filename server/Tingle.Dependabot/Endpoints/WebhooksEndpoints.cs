using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniValidation;
using System.Text.Json;
using Tingle.Dependabot;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Azure;
using Tingle.EventBus;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Microsoft.AspNetCore.Builder;

public static class WebhooksEndpoints
{
    public static IEndpointConventionBuilder MapWebhooks(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/webhooks")
                             .WithGroupName("webhooks")
                             .RequireAuthorization(AuthConstants.PolicyNameServiceHooks);
        group.MapPost("azure", HandleAzureAsync);
        return group;
    }

    private static async Task<IResult> HandleAzureAsync([FromBody] AzureDevOpsEvent model,
                                                        [FromServices] MainDbContext dbContext,
                                                        [FromServices] IEventPublisher publisher,
                                                        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(WebhooksEndpoints).FullName!);
        if (!MiniValidator.TryValidate(model, out var errors)) return Results.ValidationProblem(errors);

        var type = model.EventType;
        logger.WebhooksReceivedEvent(type, model.NotificationId, model.SubscriptionId?.Replace(Environment.NewLine, ""));

        if (type is AzureDevOpsEventType.GitPush)
        {
            var resource = JsonSerializer.Deserialize(model.Resource, SC.Default.AzureDevOpsEventCodePushResource)!;
            var adoRepository = resource.Repository!;
            var adoRepositoryId = adoRepository.Id;
            var defaultBranch = adoRepository.DefaultBranch;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

            // if the updates are not the default branch, then we ignore them
            var updatedReferences = resource.RefUpdates!.Select(ru => ru.Name).ToList();
            if (updatedReferences.Contains(defaultBranch, StringComparer.OrdinalIgnoreCase))
            {
                // request synchronization of the repository
                var evt = new ProcessSynchronization(project.Id!, trigger: true, repositoryProviderId: adoRepositoryId);
                await publisher.PublishAsync(evt);
            }
        }
        else if (type is AzureDevOpsEventType.GitPullRequestUpdated or AzureDevOpsEventType.GitPullRequestMerged)
        {
            var resource = JsonSerializer.Deserialize(model.Resource, SC.Default.AzdoPullRequest)!;
            var adoRepository = resource.Repository;
            var prId = resource.PullRequestId;
            var status = resource.Status;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

            var repoSlug = project.Url.MakeRepositorySlug(adoRepository.Name);
            if (type is AzureDevOpsEventType.GitPullRequestUpdated)
            {
                logger.WebhooksPullRequestStatusUpdated(prId, repoSlug, status);

                // TODO: handle the logic for merge conflicts here using events

            }
            else if (type is AzureDevOpsEventType.GitPullRequestMerged)
            {
                logger.WebhooksPullRequestMergedStatusUpdated(prId, repoSlug, resource.MergeStatus);

                // TODO: handle the logic for updating other PRs to find merge conflicts (restart merge or attempt merge)

            }
        }
        else if (type is AzureDevOpsEventType.GitPullRequestCommentEvent)
        {
            var resource = JsonSerializer.Deserialize(model.Resource, SC.Default.AzureDevOpsEventPullRequestCommentEventResource)!;
            var comment = resource.Comment!;
            var pr = resource.PullRequest!;
            var adoRepository = pr.Repository!;
            var prId = pr.PullRequestId;
            var status = pr.Status;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Results.Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);
            var repoSlug = project.Url.MakeRepositorySlug(adoRepository.Name);

            // ensure the comment starts with @dependabot
            var content = comment.Content?.Trim();
            if (content is not null && content.StartsWith("@dependabot"))
            {
                logger.WebhooksPullRequestCommentedOn(prId, repoSlug, content);

                // TODO: handle the logic for comments here using events
            }
        }
        else
        {
            logger.WebhooksReceivedEventUnsupported(type);
        }

        return Results.Ok();
    }
}
