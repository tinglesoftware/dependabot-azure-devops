using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Azure;
using Tingle.Dependabot.Models.Management;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Tingle.Dependabot.Workflow;

public interface IAzureDevOpsProvider
{
    Task<AzdoConnectionData> GetConnectionDataAsync(Project project, CancellationToken cancellationToken = default);
    Task<AzdoConnectionData> GetConnectionDataAsync(AzureDevOpsProjectUrl url, string token, CancellationToken cancellationToken = default);

    Task<AzdoProject> GetProjectAsync(Project project, CancellationToken cancellationToken);
    Task<AzdoProject> GetProjectAsync(AzureDevOpsProjectUrl url, string token, CancellationToken cancellationToken);

    Task<List<string>> CreateOrUpdateSubscriptionsAsync(Project project, CancellationToken cancellationToken = default);

    Task<List<AzdoRepository>> GetRepositoriesAsync(Project project, CancellationToken cancellationToken);
    Task<AzdoRepository> GetRepositoryAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken);
    Task<string?> GetDefaultBranchAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken);
    Task<AzdoRepositoryItem?> GetConfigurationFileAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken = default);

    Task<List<AzdoPullRequest>> GetActivePullRequestsAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken = default);
    Task<PullRequestProperties?> GetPullRequestPropertiesAsync(Project project, string repositoryIdOrName, int pullRequestId, CancellationToken cancellationToken = default);

    Task AbandonPullRequestAsync(Project project, string repositoryIdOrName, int pullRequestId, string? comment, CancellationToken cancellationToken = default);
}

internal class AzureDevOpsProvider(HttpClient httpClient, IOptions<WorkflowOptions> optionsAccessor) : IAzureDevOpsProvider
{
    // Possible/allowed paths for the configuration files in a repository.
    private static readonly IReadOnlyList<string> ConfigurationFilePaths = [
        ".azuredevops/dependabot.yml",
        ".azuredevops/dependabot.yaml",

        ".github/dependabot.yml",
        ".github/dependabot.yaml",
    ];

    private static readonly (string, string)[] SubscriptionEventTypes =
    [
        ("git.push", "1.0"),
        ("git.pullrequest.updated", "1.0"),
        ("git.pullrequest.merged", "1.0"),
        ("ms.vss-code.git-pullrequest-comment-event", "2.0"),
    ];

    /**
    * Pull request property names used to store metadata about the pull request.
    * https://learn.microsoft.com/en-us/rest/api/azure/devops/git/pull-request-properties
    */
    private const string PrPropertyDependabotPackageManager = "Dependabot.PackageManager";
    private const string PrPropertyDependabotDependencies = "Dependabot.Dependencies";

    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

    public Task<AzdoConnectionData> GetConnectionDataAsync(Project project, CancellationToken cancellationToken = default)
        => GetConnectionDataAsync(project.Url, project.Token, cancellationToken);
    public async Task<AzdoConnectionData> GetConnectionDataAsync(AzureDevOpsProjectUrl url, string token, CancellationToken cancellationToken = default)
    {
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/_apis/connectionData",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(token, request, SC.Default.AzdoConnectionData, cancellationToken);
    }

    public Task<AzdoProject> GetProjectAsync(Project project, CancellationToken cancellationToken)
        => GetProjectAsync(project.Url, project.Token, cancellationToken);
    public async Task<AzdoProject> GetProjectAsync(AzureDevOpsProjectUrl url, string token, CancellationToken cancellationToken)
    {
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/_apis/projects/{url.ProjectIdOrName}",
            Query = "?api-version=7.1",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(token, request, SC.Default.AzdoProject, cancellationToken);
    }

    public async Task<List<string>> CreateOrUpdateSubscriptionsAsync(Project project, CancellationToken cancellationToken = default)
    {
        // if there is no webhook endpoint, we don't need to do anything
        var webhookUrl = options.WebhookEndpoint;
        if (webhookUrl is null) return [];

        // prepare the query
        var projectId = project.ProviderId ?? throw new InvalidOperationException("ProviderId for the project cannot be null");
        var query = new AzdoSubscriptionsQuery
        {
            PublisherId = "tfs",
            PublisherInputFilters =
            [
                new AzdoSubscriptionsQueryInputFilter
                {
                    Conditions =
                    [
                        new AzdoSubscriptionsQueryInputFilterCondition
                        {
                            InputId = "projectId",
                            Operator = AzdoSubscriptionsQueryInputFilterOperator.Equals,
                            InputValue = projectId,
                        },
                    ],
                },
            ],

            ConsumerId = "webHooks",
            ConsumerActionId = "httpRequest",
        };

        // fetch the subscriptions
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/_apis/hooks/subscriptionsquery",
            Query = "?api-version=7.1",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(query, SC.Default.AzdoSubscriptionsQuery), };
        var subscriptions = (await SendAsync(project.Token, request, SC.Default.AzdoSubscriptionsQueryResponse, cancellationToken)).Results;

        // iterate each subscription checking if creation or update is required
        var ids = new List<string>();
        foreach (var (eventType, resourceVersion) in SubscriptionEventTypes)
        {
            // find an existing one
            AzdoSubscription? existing = null;
            foreach (var sub in subscriptions)
            {
                if (sub.EventType == eventType
                    && sub.ConsumerInputs.TryGetValue("url", out var rawUrl)
                    && webhookUrl == new Uri(rawUrl)) // comparing with Uri ensure we don't have to deal with slashes and default ports
                {
                    existing = sub;
                    break;
                }
            }

            // if we have an existing one, update it, otherwise create a new one

            if (existing is not null)
            {
                // publisherId, consumerId, and consumerActionId cannot be updated
                existing.EventType = eventType;
                existing.ResourceVersion = resourceVersion;
                existing.PublisherInputs = MakeTfsPublisherInputs(eventType, projectId);
                existing.ConsumerInputs = MakeWebHooksConsumerInputs(project, webhookUrl);
                uri = new UriBuilder(uri) { Path = $"{url.OrganizationName}/_apis/hooks/subscriptions/{existing.Id}", }.Uri;
                request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(existing, SC.Default.AzdoSubscription), };
                existing = await SendAsync(project.Token, request, SC.Default.AzdoSubscription, cancellationToken);
            }
            else
            {
                existing = new AzdoSubscription
                {
                    EventType = eventType,
                    ResourceVersion = resourceVersion,

                    PublisherId = "tfs",
                    PublisherInputs = MakeTfsPublisherInputs(eventType, projectId),
                    ConsumerId = "webHooks",
                    ConsumerActionId = "httpRequest",
                    ConsumerInputs = MakeWebHooksConsumerInputs(project, webhookUrl),
                };
                uri = new UriBuilder(uri) { Path = $"{url.OrganizationName}/_apis/hooks/subscriptions", }.Uri;
                request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(existing, SC.Default.AzdoSubscription), };
                existing = await SendAsync(project.Token, request, SC.Default.AzdoSubscription, cancellationToken);
            }

            // track the identifier of the subscription
            ids.Add(existing.Id!);
        }

        return ids;
    }

    public async Task<List<AzdoRepository>> GetRepositoriesAsync(Project project, CancellationToken cancellationToken)
    {
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories",
            Query = "?api-version=7.1",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(project.Token, request, SC.Default.AzdoResponseListAzdoRepository, cancellationToken);
    }
    public async Task<AzdoRepository> GetRepositoryAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken)
    {
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}",
            Query = "?api-version=7.1",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(project.Token, request, SC.Default.AzdoRepository, cancellationToken);
    }
    public async Task<string?> GetDefaultBranchAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken)
    {
        var repository = await GetRepositoryAsync(project, repositoryIdOrName, cancellationToken);
        return NormalizeBranchName(repository.DefaultBranch);
    }
    public async Task<AzdoRepositoryItem?> GetConfigurationFileAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken = default)
    {
        var url = project.Url;

        // Try all known paths
        foreach (var path in ConfigurationFilePaths)
        {
            try
            {
                var uri = new UriBuilder
                {
                    Scheme = url.Scheme,
                    Host = url.Hostname,
                    Port = url.Port ?? -1,
                    Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/items",
                    Query = $"?path={path}&includeContent=true&latestProcessedChange=true&api-version=7.1"
                }.Uri;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var item = await SendAsync(project.Token, request, SC.Default.AzdoRepositoryItem, cancellationToken);
                if (item is not null) return item;
            }
            catch (HttpRequestException hre) when (hre.StatusCode is System.Net.HttpStatusCode.NotFound) { }
        }

        return null;
    }

    public async Task<List<AzdoPullRequest>> GetActivePullRequestsAsync(Project project, string repositoryIdOrName, CancellationToken cancellationToken = default)
    {
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/pullrequests",
            Query = $"?searchCriteria.creatorId={project.UserId}&searchCriteria.status=Active&api-version=7.1"
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync(project.Token, request, SC.Default.AzdoResponseListAzdoPullRequest, cancellationToken);
    }
    public async Task<PullRequestProperties?> GetPullRequestPropertiesAsync(Project project, string repositoryIdOrName, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/pullrequests/{pullRequestId}/properties",
            Query = $"?api-version=7.1"
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var response = await SendAsync(project.Token, request, SC.Default.AzdoResponseAzdoPullRequestProperties, cancellationToken);

        // parse the properties
        string? packageManager = null;
        PullRequestStoredDependencies? dependencies = null;
        foreach (var (key, value) in response.Value)
        {
            var inner = value.Value;
            if (key == PrPropertyDependabotPackageManager) packageManager = inner;
            else if (key == PrPropertyDependabotDependencies)
                dependencies = JsonSerializer.Deserialize(inner, SC.Default.PullRequestStoredDependencies);
        }
        if (packageManager is null || dependencies is null) return null;
        return new PullRequestProperties(packageManager, dependencies);
    }

    public async Task AbandonPullRequestAsync(Project project, string repositoryIdOrName, int pullRequestId, string? comment, CancellationToken cancellationToken = default)
    {
        // Add a comment to the pull request, if supplied
        var url = project.Url;
        HttpRequestMessage? request = null;
        Uri? uri = null;
        if (comment is not null)
        {
            uri = new UriBuilder
            {
                Scheme = url.Scheme,
                Host = url.Hostname,
                Port = url.Port ?? -1,
                Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/pullrequests/{pullRequestId}/threads",
                Query = $"?api-version=7.1"
            }.Uri;
            var thread = new AzdoPullRequestCommentThreadCreate(
                Status: AzdoPullRequestCommentThreadStatus.Closed,
                Comments: [new AzdoPullRequestCommentThreadComment(
                Content: comment,
                CommentType: AzdoPullRequestCommentThreadCommentType.System,
                Author: new AzdoIdentity(Id: project.UserId)
            )]
            );
            request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(thread, SC.Default.AzdoPullRequestCommentThreadCreate),
            };
            await SendAsync(project.Token, request, SC.Default.AzdoPullRequestCommentThread, cancellationToken);
        }

        // Abandon the pull request
        uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/pullrequests/{pullRequestId}",
            Query = $"?api-version=7.1"
        }.Uri;
        request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(new System.Text.Json.Nodes.JsonObject
            {
                ["status"] = "abandoned",
                ["closedBy"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["id"] = project.UserId,
                },
            }, SC.Default.JsonObject),
        };
        var abandonedPullRequest = await SendAsync(project.Token, request, SC.Default.AzdoPullRequest, cancellationToken);
        if (!string.Equals(abandonedPullRequest.Status, "abandoned", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Failed to abandon pull request, status was not updated");
        }

        // Delete the source branch if required
        uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/{url.ProjectIdOrName}/_apis/git/repositories/{repositoryIdOrName}/refs",
            Query = $"?api-version=7.1"
        }.Uri;
        var refUpdate = new AzdoRefUpdate(
            IsLocked: false,
            Name: abandonedPullRequest.SourceRefName,
            NewObjectId: "0000000000000000000000000000000000000000",
            OldObjectId: abandonedPullRequest.LastMergeSourceCommit.CommitId
        );
        request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create([refUpdate], SC.Default.ListAzdoRefUpdate),
        };
        var result = await SendAsync(project.Token, request, SC.Default.AzdoResponseListAzdoRefUpdateResult, cancellationToken);
        if (result.Value[0].Success != true) throw new Exception("Failed to delete the source branch");
    }

    private async Task<T> SendAsync<T>(string token, HttpRequestMessage request, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(jsonTypeInfo, cancellationToken))!;
    }

    internal static string? NormalizeBranchName(string? branch)
    {
        // Strip the 'refs/heads/' prefix from the branch name, if present
        return branch is not null && branch.StartsWith("refs/heads/") ? branch["refs/heads/".Length..] : branch;
    }

    internal static Dictionary<string, string> MakeTfsPublisherInputs(string type, string projectId)
    {
        // possible inputs are available via an authenticated request to
        // https://dev.azure.com/{organization}/_apis/hooks/publishers/tfs

        // always include the project identifier, to restrict events from that project
        var result = new Dictionary<string, string> { ["projectId"] = projectId, };

        if (type is "git.pullrequest.updated")
        {
            result["notificationType"] = "StatusUpdateNotification";
        }

        if (type is "git.pullrequest.merged")
        {
            result["mergeResult"] = "Conflicts";
        }

        return result;
    }
    internal static Dictionary<string, string> MakeWebHooksConsumerInputs(Project project, Uri webhookUrl)
    {
        return new Dictionary<string, string>
        {
            // possible inputs are available via an authenticated request to
            // https://dev.azure.com/{organization}/_apis/hooks/consumers/webHooks

            ["detailedMessagesToSend"] = "none",
            ["messagesToSend"] = "none",
            ["url"] = webhookUrl.ToString(),
            ["basicAuthUsername"] = project.Id,
            ["basicAuthPassword"] = project.Password,
        };
    }
}
