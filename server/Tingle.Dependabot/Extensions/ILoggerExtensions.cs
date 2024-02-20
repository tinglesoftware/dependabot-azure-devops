using Tingle.Dependabot.Models.Azure;
using Tingle.Dependabot.Models.Management;

namespace Microsoft.Extensions.Logging;

internal static partial class ILoggerExtensions
{
    #region Webhooks (2xx)

    [LoggerMessage(200, LogLevel.Debug, "Received {EventType} notification {NotificationId} on subscription {SubscriptionId}")]
    public static partial void WebhooksReceivedEvent(this ILogger logger, AzureDevOpsEventType? eventType, int notificationId, string? subscriptionId);

    [LoggerMessage(201, LogLevel.Information, "PR {PullRequestId} in {RepositoryUrl} status updated to {PullRequestStatus}")]
    public static partial void WebhooksPullRequestStatusUpdated(this ILogger logger, int pullRequestId, string? repositoryUrl, string? pullRequestStatus);

    [LoggerMessage(202, LogLevel.Information, "PR {PullRequestId} in {RepositoryUrl} merge status changed to {MergeStatus}")]
    public static partial void WebhooksPullRequestMergedStatusUpdated(this ILogger logger, int pullRequestId, string? repositoryUrl, string? mergeStatus);

    [LoggerMessage(203, LogLevel.Information, "PR {PullRequestId} in {RepositoryUrl} was commented on: {CommentContent}")]
    public static partial void WebhooksPullRequestCommentedOn(this ILogger logger, int pullRequestId, string? repositoryUrl, string? commentContent);

    [LoggerMessage(204, LogLevel.Warning, "'{EventType}' events are not supported!")]
    public static partial void WebhooksReceivedEventUnsupported(this ILogger logger, AzureDevOpsEventType? eventType);

    #endregion

    #region Synchronizer (3xx)

    [LoggerMessage(300, LogLevel.Information, "Skipping synchronization for {ProjectId} since it last happened recently at {Synchronized}.")]
    public static partial void SkippingSyncProjectTooSoon(this ILogger logger, string? projectId, DateTimeOffset? synchronized);

    [LoggerMessage(301, LogLevel.Warning, "Skipping sync for update because project '{ProjectId}' does not exist.")]
    public static partial void SkippingSyncProjectNotFound(this ILogger logger, string? projectId);

    [LoggerMessage(302, LogLevel.Warning, "Skipping synchronization because repository '{RepositoryId}' does not exist.")]
    public static partial void SkippingSyncRepositoryNotFound(this ILogger logger, string? repositoryId);

    [LoggerMessage(303, LogLevel.Debug, "Skipping sync for {RepositoryName} in {ProjectId} because it is disabled or is a fork")]
    public static partial void SkippingSyncRepositoryDisabledOrFork(this ILogger logger, string repositoryName, string? projectId);

    [LoggerMessage(304, LogLevel.Debug, "Listing repositories in {ProjectId} ...")]
    public static partial void SyncListingRepositories(this ILogger logger, string? projectId);

    [LoggerMessage(305, LogLevel.Debug, "Found {RepositoriesCount} repositories in {ProjectId}")]
    public static partial void SyncListingRepositories(this ILogger logger, int repositoriesCount, string? projectId);

    [LoggerMessage(306, LogLevel.Information, "Deleted {RepositoriesCount} repositories that are no longer present in {ProjectId}.")]
    public static partial void SyncDeletedRepositories(this ILogger logger, int repositoriesCount, string? projectId);

    [LoggerMessage(307, LogLevel.Information, "Deleting '{RepositorySlug}' in {ProjectId} as it no longer has a configuration file.")]
    public static partial void SyncDeletingRepository(this ILogger logger, string? repositorySlug, string? projectId);

    [LoggerMessage(308, LogLevel.Information, "Configuration file for '{RepositorySlug}' in {ProjectId} is new or has been updated.")]
    public static partial void SyncConfigFileChanged(this ILogger logger, string? repositorySlug, string? projectId);

    [LoggerMessage(309, LogLevel.Warning, "Skipping '{RepositorySlug}' in {ProjectId}. The YAML file is invalid.")]
    public static partial void SyncConfigFileInvalidStructure(this ILogger logger, Exception exception, string? repositorySlug, string? projectId);

    [LoggerMessage(310, LogLevel.Warning, "Skipping '{RepositorySlug}' in {ProjectId}. The parsed configuration is invalid.")]
    public static partial void SyncConfigFileInvalidData(this ILogger logger, Exception exception, string? repositorySlug, string? projectId);

    #endregion

    #region Scheduler (4xx)

    [LoggerMessage(400, LogLevel.Debug, "Creating/Updating schedules for repository '{RepositoryId}' in project '{ProjectId}'")]
    public static partial void SchedulesUpdating(this ILogger logger, string? repositoryId, string? projectId);

    [LoggerMessage(401, LogLevel.Error, "Timer call back does not have correct argument. Expected '{ExpectedType}' but got '{ActualType}'")]
    public static partial void SchedulesTimerInvalidCallbackArgument(this ILogger logger, string? expectedType, string? actualType);

    [LoggerMessage(402, LogLevel.Warning, "Schedule was missed for {RepositoryId}({UpdateId}) in project '{ProjectId}'. Triggering now ...")]
    public static partial void ScheduleTriggerMissed(this ILogger logger, string? repositoryId, int updateId, string? projectId);

    #endregion

    #region Runner (5xx)

    [LoggerMessage(500, LogLevel.Information, "Written job definition file for {UpdateJobId} at {JobDefinitionPath}")]
    public static partial void WrittenJobDefinitionFile(this ILogger logger, string? updateJobId, string? jobDefinitionPath);

    [LoggerMessage(501, LogLevel.Information, "Created ContainerApp Job for {UpdateJobId}")]
    public static partial void CreatedContainerAppJob(this ILogger logger, string? updateJobId);

    [LoggerMessage(502, LogLevel.Information, "Started ContainerApp Job for {UpdateJobId}")]
    public static partial void StartedContainerAppJob(this ILogger logger, string? updateJobId);

    #endregion

    #region Update Jobs (6xx)

    [LoggerMessage(600, LogLevel.Warning, "Cannot update state for job '{UpdateJobId}' as it does not exist.")]
    public static partial void UpdateJobCannotUpdateStateNotFound(this ILogger logger, string? updateJobId);

    [LoggerMessage(601, LogLevel.Warning, "Cannot update state for job '{UpdateJobId}' as it is already in a terminal state.")]
    public static partial void UpdateJobCannotUpdateStateTerminalState(this ILogger logger, string? updateJobId);

    [LoggerMessage(602, LogLevel.Information, "The runner did not provide a state for job '{UpdateJobId}'.")]
    public static partial void UpdateJobRunnerNoState(this ILogger logger, string? updateJobId);

    [LoggerMessage(603, LogLevel.Warning, "Deleting job '{UpdateJobId}' as it has been pending for more than 90 minutes.")]
    public static partial void UpdateJobPendingTooLong(this ILogger logger, string? updateJobId);

    [LoggerMessage(604, LogLevel.Warning, "Cannot collect logs for job '{UpdateJobId}' as it does not exist.")]
    public static partial void UpdateJobCannotCollectLogsNotFound(this ILogger logger, string? updateJobId);

    [LoggerMessage(605, LogLevel.Warning, "Cannot collect logs for job '{UpdateJobId}' with status '{UpdateJobStatus}'.")]
    public static partial void UpdateJobCannotCollectLogsInvalidStatus(this ILogger logger, string? updateJobId, UpdateJobStatus updateJobStatus);

    [LoggerMessage(606, LogLevel.Information, "Found {UpdateJobsCount} jobs that are still pending for more than 10 min. Requesting manual resolution ...")]
    public static partial void UpdateJobRequestingManualResolution(this ILogger logger, int updateJobsCount);

    [LoggerMessage(607, LogLevel.Information, "Removed {UpdateJobsCount} jobs that older than {Cutoff}")]
    public static partial void UpdateJobRemovedOldJobs(this ILogger logger, int updateJobsCount, DateTimeOffset cutoff);

    #endregion

    #region Update Jobs (7xx)

    [LoggerMessage(700, LogLevel.Warning, "Skipping trigger for update because project '{ProjectId}' does not exist.")]
    public static partial void SkippingTriggerProjectNotFound(this ILogger logger, string projectId);

    [LoggerMessage(701, LogLevel.Warning, "Skipping trigger for update because repository '{RepositoryId}' in project '{ProjectId}' does not exist.")]
    public static partial void SkippingTriggerRepositoryNotFound(this ILogger logger, string repositoryId, string? projectId);

    [LoggerMessage(702, LogLevel.Warning, "Skipping trigger for update because repository update '{RepositoryId}({RepositoryUpdateId})' in project '{ProjectId}' does not exist.")]
    public static partial void SkippingTriggerRepositoryUpdateNotFound(this ILogger logger, string repositoryId, int repositoryUpdateId, string? projectId);

    [LoggerMessage(703, LogLevel.Warning, "A job for update '{RepositoryId}({RepositoryUpdateId})' in project '{ProjectId}' requested by event '{EventBusId}' already exists. Skipping it ...")]
    public static partial void SkippingTriggerJobAlreadyExists(this ILogger logger, string? repositoryId, int repositoryUpdateId, string? projectId, string? eventBusId);

    #endregion
}
