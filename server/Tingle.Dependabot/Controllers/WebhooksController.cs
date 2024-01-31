using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Azure;
using Tingle.EventBus;

namespace Tingle.Dependabot.Controllers;

[ApiController]
[Route("/webhooks")]
[Authorize(AuthConstants.PolicyNameServiceHooks)]
public class WebhooksController(MainDbContext dbContext, IEventPublisher publisher, ILogger<WebhooksController> logger) : ControllerBase // TODO: unit test this
{
    [HttpPost("azure")]
    public async Task<IActionResult> PostAsync([FromBody] AzureDevOpsEvent model)
    {
        var type = model.EventType;
        logger.WebhooksReceivedEvent(type, model.NotificationId, model.SubscriptionId);

        if (type is AzureDevOpsEventType.GitPush)
        {
            var resource = JsonSerializer.Deserialize<AzureDevOpsEventCodePushResource>(model.Resource)!;
            var adoRepository = resource.Repository!;
            var adoRepositoryId = adoRepository.Id;
            var defaultBranch = adoRepository.DefaultBranch;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

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
            var resource = JsonSerializer.Deserialize<AzureDevOpsEventPullRequestResource>(model.Resource)!;
            var adoRepository = resource.Repository!;
            var prId = resource.PullRequestId;
            var status = resource.Status;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

            if (type is AzureDevOpsEventType.GitPullRequestUpdated)
            {
                logger.WebhooksPullRequestStatusUpdated(prId, adoRepository.RemoteUrl, status);

                // TODO: handle the logic for merge conflicts here using events

            }
            else if (type is AzureDevOpsEventType.GitPullRequestMerged)
            {
                logger.WebhooksPullRequestMergedStatusUpdated(prId, adoRepository.RemoteUrl, resource.MergeStatus);

                // TODO: handle the logic for updating other PRs to find merge conflicts (restart merge or attempt merge)

            }
        }
        else if (type is AzureDevOpsEventType.GitPullRequestCommentEvent)
        {
            var resource = JsonSerializer.Deserialize<AzureDevOpsEventPullRequestCommentEventResource>(model.Resource)!;
            var comment = resource.Comment!;
            var pr = resource.PullRequest!;
            var adoRepository = pr.Repository!;
            var prId = pr.PullRequestId;
            var status = pr.Status;

            // ensure project exists
            var adoProjectId = adoRepository.Project!.Id;
            var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.ProviderId == adoProjectId);
            if (project is null) return Problem(title: ErrorCodes.ProjectNotFound, statusCode: 400);

            // ensure the comment starts with @dependabot
            var content = comment.Content?.Trim();
            if (content is not null && content.StartsWith("@dependabot"))
            {
                logger.WebhooksPullRequestCommentedOn(prId, adoRepository.RemoteUrl, content);

                // TODO: handle the logic for comments here using events
            }
        }
        else
        {
            logger.WebhooksReceivedEventUnsupported(type);
        }

        return Ok();
    }
}
