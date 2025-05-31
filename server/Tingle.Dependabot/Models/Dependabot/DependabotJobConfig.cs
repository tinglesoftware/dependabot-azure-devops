using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

// JobFile  is the payload passed to file updater containers.
public record DependabotJobFile(
    [property: JsonPropertyName("job")] DependabotJobConfig Job);

public record DependabotJobConfig(
    [property: JsonPropertyName("package-manager")] string PackageManager,
    [property: JsonPropertyName("allowed-updates")] IReadOnlyList<DependabotAllowed>? AllowedUpdates,
    [property: JsonPropertyName("debug")] bool? Debug,
    [property: JsonPropertyName("dependency-groups")] IReadOnlyList<DependabotGroup>? DependencyGroups,
    [property: JsonPropertyName("dependencies")] IReadOnlyList<string> Dependencies,
    [property: JsonPropertyName("dependency-group-to-refresh")] string? DependencyGroupToRefresh,
    [property: JsonPropertyName("existing-pull-requests")] IReadOnlyList<DependabotExistingPR[]>? ExistingPullRequests,
    [property: JsonPropertyName("existing-group-pull-requests")] IReadOnlyList<DependabotExistingGroupPR>? ExistingGroupPullRequests,
    [property: JsonPropertyName("experiments")] DependabotExperiment? Experiments,
    [property: JsonPropertyName("ignore-conditions")] IReadOnlyList<DependabotCondition>? IgnoreConditions,
    [property: JsonPropertyName("lockfile-only")] bool? LockfileOnly,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [property: JsonPropertyName("requirements-update-strategy")] string? RequirementsUpdateStrategy,
    [property: JsonPropertyName("security-advisories")] IReadOnlyList<DependabotSecurityAdvisory>? SecurityAdvisories,
    [property: JsonPropertyName("security-updates-only")] bool? SecurityUpdatesOnly,
    [property: JsonPropertyName("source")] DependabotSource Source,
    [property: JsonPropertyName("update-subdependencies")] bool? UpdateSubdependencies,
    [property: JsonPropertyName("updating-a-pull-request")] bool? UpdatingAPullRequest,
    [property: JsonPropertyName("vendor-dependencies")] bool? VendorDependencies,
    [property: JsonPropertyName("reject-external-code")] bool? RejectExternalCode,
    [property: JsonPropertyName("repo-private")] bool? RepoPrivate,
    [property: JsonPropertyName("commit-message-options")] DependabotCommitOptions? CommitMessageOptions,
    [property: JsonPropertyName("credentials-metadata")] IReadOnlyList<DependabotCredential> CredentialsMetadata,
    [property: JsonPropertyName("max-updater-run-time")] int? MaxUpdaterRunTime = null);

public record DependabotSource(
    [property: JsonPropertyName("provider")] string? Provider,
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("directory")] string? Directory,
    [property: JsonPropertyName("directories")] List<string>? Directories,
    [property: JsonPropertyName("branch")] string? Branch,
    [property: JsonPropertyName("commit")] string? Commit,
    [property: JsonPropertyName("hostname")] string? Hostname, // Must be provided if Hostname is
    [property: JsonPropertyName("api-endpoint")] string? APIEndpoint); // Must be provided if Hostname is

public record DependabotExistingPR(
    [property: JsonPropertyName("dependency-name")] string DependencyName,
    [property: JsonPropertyName("dependency-version")] string? DependencyVersion,
    [property: JsonPropertyName("directory")] string? Directory);

public record DependabotExistingGroupPR(
    [property: JsonPropertyName("dependency-group-name")] string DependencyGroupName,
    [property: JsonPropertyName("dependencies")] List<DependabotExistingPR> Dependencies);

public record DependabotAllowed(
    [property: JsonPropertyName("dependency-name")] string? DependencyName = null,
    [property: JsonPropertyName("dependency-type")] string? DependencyType = null,
    [property: JsonPropertyName("update-type")] string? UpdateType = null);

public record DependabotGroup(
    [property: JsonPropertyName("name")] string? GroupName,
    [property: JsonPropertyName("applies-to")] string? AppliesTo,
    [property: JsonPropertyName("rules")] JsonObject? Rules);

public record DependabotCondition(
    [property: JsonPropertyName("dependency-name")] string DependencyName,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("update-types")] IReadOnlyList<string>? UpdateTypes,
    [property: JsonPropertyName("updated-at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("version-requirement")] string? VersionRequirement);

public record DependabotSecurityAdvisory(
    [property: JsonPropertyName("dependency-name")] string DependencyName,
    [property: JsonPropertyName("affected-versions")] List<string> AffectedVersions,
    [property: JsonPropertyName("patched-versions")] List<string> PatchedVersions,
    [property: JsonPropertyName("unaffected-versions")] List<string> UnaffectedVersions);

public record DependabotDependency(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("previous-requirements")] List<DependabotRequirement>? PreviousRequirements,
    [property: JsonPropertyName("previous-version")] string? PreviousVersion,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("requirements")] List<DependabotRequirement>? Requirements,
    [property: JsonPropertyName("removed")] bool? Removed,
    [property: JsonPropertyName("directory")] string? Directory);

public record DependabotRequirement(
    [property: JsonPropertyName("file")] string? File,
    [property: JsonPropertyName("groups")] JsonArray? Groups,
    [property: JsonPropertyName("metadata")] JsonObject? Metadata,
    [property: JsonPropertyName("requirement")] string? Requirement,
    [property: JsonPropertyName("source")] DependabotRequirementSource? Source,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("previous-version")] string? PreviousVersion);

public class DependabotRequirementSource : Dictionary<string, object>
{
    public DependabotRequirementSource() { }
}

public class DependabotExperiment : Dictionary<string, object>
{
    public DependabotExperiment() { }
    public DependabotExperiment(IEqualityComparer<string>? comparer) : base(comparer) { }
}

public record DependabotCommitOptions(
    [property: JsonPropertyName("prefix")] string? Prefix,
    [property: JsonPropertyName("prefix-development")] string? PrefixDevelopment,
    [property: JsonPropertyName("include-scope")] bool? IncludeScope);
