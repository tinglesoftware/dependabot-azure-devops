using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tingle.Dependabot.Events;
using Tingle.EventBus;

namespace Tingle.Dependabot;

internal class AzureDevOpsEventHandler
{
    private readonly IEventPublisher publisher;
    private readonly ILogger logger;

    public AzureDevOpsEventHandler(IEventPublisher publisher, ILogger<AzureDevOpsEventHandler> logger)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task HandleAsync(AzureDevOpsEvent model, CancellationToken cancellationToken = default)
    {
        var type = model.EventType;
        logger.LogDebug("Received {EventType} notification {NotificationId} on subscription {SubscriptionId}",
                        type,
                        model.NotificationId,
                        model.SubscriptionId);

        if (type is AzureDevOpsEventType.GitPush)
        {
            var resource = JsonSerializer.Deserialize<AzureDevOpsEventCodePushResource>(model.Resource)!;
            var adoRepository = resource.Repository!;
            var adoRepositoryId = adoRepository.Id;
            var defaultBranch = adoRepository.DefaultBranch;

            // if the updates are not the default branch, then we ignore them
            var updatedReferences = resource.RefUpdates!.Select(ru => ru.Name).ToList();
            if (updatedReferences.Contains(defaultBranch, StringComparer.OrdinalIgnoreCase))
            {
                // request synchronization of the repository
                var evt = new ProcessSynchronization(true, repositoryProviderId: adoRepositoryId);
                await publisher.PublishAsync(evt, cancellationToken: cancellationToken);
            }
        }
        else if (type is AzureDevOpsEventType.GitPullRequestUpdated or AzureDevOpsEventType.GitPullRequestMerged)
        {
            var resource = JsonSerializer.Deserialize<AzureDevOpsEventPullRequestResource>(model.Resource)!;
            var adoRepository = resource.Repository!;
            var prId = resource.PullRequestId;
            var status = resource.Status;

            if (type is AzureDevOpsEventType.GitPullRequestUpdated)
            {
                logger.LogInformation("PR {PullRequestId} in {RepositoryUrl} status updated to {PullRequestStatus}",
                                      prId,
                                      adoRepository.RemoteUrl,
                                      status);

                // TODO: handle the logic for merge conflicts here using events

            }
            else if (type is AzureDevOpsEventType.GitPullRequestMerged)
            {
                logger.LogInformation("Merge status {MergeStatus} for PR {PullRequestId} in {RepositoryUrl}",
                                      resource.MergeStatus,
                                      prId,
                                      adoRepository.RemoteUrl);

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

            logger.LogInformation("PR {PullRequestId} in {RepositoryUrl} was commented on: {Content}",
                                  prId,
                                  adoRepository.RemoteUrl,
                                  comment.Content);

            // TODO: handle the logic for comments here using events
        }
        else
        {
            logger.LogWarning("'{EventType}' events are not supported!", type);
        }
    }
}
