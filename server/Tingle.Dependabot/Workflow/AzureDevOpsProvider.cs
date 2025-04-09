using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using Tingle.Dependabot.Models.Azure;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

public class AzureDevOpsProvider(HttpClient httpClient, IOptions<WorkflowOptions> optionsAccessor)
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

    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

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
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(query), };
        var subscriptions = (await SendAsync<AzdoSubscriptionsQueryResponse>(project.Token!, request, cancellationToken)).Results;

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
                request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(existing), };
                existing = await SendAsync<AzdoSubscription>(project.Token!, request, cancellationToken);
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
                request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(existing), };
                existing = await SendAsync<AzdoSubscription>(project.Token!, request, cancellationToken);
            }

            // track the identifier of the subscription
            ids.Add(existing.Id!);
        }

        return ids;
    }

    public async Task<AzdoProject> GetProjectAsync(Project project, CancellationToken cancellationToken)
    {
        var url = project.Url;
        var uri = new UriBuilder
        {
            Scheme = url.Scheme,
            Host = url.Hostname,
            Port = url.Port ?? -1,
            Path = $"{url.OrganizationName}/_apis/projects/{url.ProjectIdOrName}",
            Query = "?api-version=7.1",
        }.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        return await SendAsync<AzdoProject>(project.Token!, request, cancellationToken);
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
        var data = await SendAsync<AzdoListResponse<AzdoRepository>>(project.Token!, request, cancellationToken);
        return data.Value;
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
        return await SendAsync<AzdoRepository>(project.Token!, request, cancellationToken);
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
                var item = await SendAsync<AzdoRepositoryItem>(project.Token!, request, cancellationToken);
                if (item is not null) return item;
            }
            catch (HttpRequestException hre) when (hre.StatusCode is System.Net.HttpStatusCode.NotFound) { }
        }

        return null;
    }

    private async Task<T> SendAsync<T>(string token, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken))!;
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
            ["basicAuthUsername"] = project.Id!,
            ["basicAuthPassword"] = project.Password!,
        };
    }
}
