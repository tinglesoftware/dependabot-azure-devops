using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot;

// initial setup
[JsonSerializable(typeof(List<InitialSetupService.ProjectSetupInfo>))]

// management endpoints
[JsonSerializable(typeof(Models.Management.SynchronizationRequest))]
[JsonSerializable(typeof(Models.Management.Project))]
[JsonSerializable(typeof(List<Models.Management.Repository>))]
[JsonSerializable(typeof(List<Models.Management.UpdateJob>))]

// webhook endpoints
[JsonSerializable(typeof(Models.Azure.AzureDevOpsEvent))]
[JsonSerializable(typeof(Models.Azure.AzureDevOpsEventCodePushResource))]
[JsonSerializable(typeof(Models.Azure.AzureDevOpsEventPullRequestCommentEventResource))]

// update_jobs endpoints
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotCreatePullRequest>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotUpdatePullRequest>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotClosePullRequest>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotRecordUpdateJobError>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotRecordUpdateJobUnknownError>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotMarkAsProcessed>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotUpdateDependencyList>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotRecordEcosystemVersions>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<Models.Dependabot.DependabotIncrementMetric>))]
[JsonSerializable(typeof(Models.Dependabot.DependabotRequest<JsonObject>))]

// runner
[JsonSerializable(typeof(Models.Dependabot.DependabotJobFile))]
[JsonSerializable(typeof(Models.Dependabot.DependabotProxyConfig))]

// azure devops api
[JsonSerializable(typeof(Models.Azure.AzdoConnectionData))]
[JsonSerializable(typeof(Models.Azure.AzdoProject))]
[JsonSerializable(typeof(Models.Azure.AzdoSubscription))]
[JsonSerializable(typeof(Models.Azure.AzdoSubscriptionsQuery))]
[JsonSerializable(typeof(Models.Azure.AzdoSubscriptionsQueryResponse))]
[JsonSerializable(typeof(Models.Azure.AzdoResponse<List<Models.Azure.AzdoRepository>>))]
[JsonSerializable(typeof(Models.Azure.AzdoRepositoryItem))]
[JsonSerializable(typeof(Models.Azure.AzdoResponse<List<Models.Azure.AzdoPullRequest>>))]
[JsonSerializable(typeof(Models.Azure.AzdoResponse<Models.Azure.AzdoPullRequestProperties>))]
[JsonSerializable(typeof(List<Models.Azure.AzdoRefUpdate>))]
[JsonSerializable(typeof(Models.Azure.AzdoResponse<List<Models.Azure.AzdoRefUpdateResult>>))]
[JsonSerializable(typeof(Models.Azure.AzdoPullRequestCommentThreadCreate))]
[JsonSerializable(typeof(Models.Azure.AzdoPullRequestCommentThread))]
[JsonSerializable(typeof(Models.Azure.PullRequestStoredDependencies))]

// github graphql
[JsonSerializable(typeof(Models.GitHub.GhsaGraphQlRequest))]
[JsonSerializable(typeof(Models.GitHub.GhsaGraphQlResponse))]

// shared
[JsonSerializable(typeof(Dictionary<string, Models.Dependabot.DependabotRegistry>))]

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,

    // Ignore default values to reduce the data sent after serialization
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

    // Do not indent content to reduce data usage
    WriteIndented = false,

    // Use SnakeCase because it is what the server provides
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.Unspecified,
    PropertyNameCaseInsensitive = true,

    // Convert enums to strings, those with EnumMemberAttribute must use JsonConverterAttribute
    UseStringEnumConverter = true
)]
internal partial class DependabotSerializerContext : JsonSerializerContext
{

}
