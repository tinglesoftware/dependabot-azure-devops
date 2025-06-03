using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Dependabot;

public record DependabotRequest<T>(
    [property: JsonPropertyName("data")] T Data);

public record DependabotUpdateDependencyList(
    [property: JsonPropertyName("dependencies")] List<DependabotDependency>? Dependencies,
    [property: JsonPropertyName("dependency_files")] List<string>? DependencyFiles);

public record DependabotCreatePullRequest(
    [property: JsonPropertyName("base-commit-sha")] string BaseCommitSha,
    [property: JsonPropertyName("dependencies")] List<DependabotDependency> Dependencies,
    [property: JsonPropertyName("updated-dependency-files")] List<DependabotDependencyFile> DependencyFiles,
    [property: JsonPropertyName("pr-title")] string PrTitle,
    [property: JsonPropertyName("pr-body")] string PrBody,
    [property: JsonPropertyName("commit-message")] string CommitMessage,
    [property: JsonPropertyName("dependency-group")] JsonObject? DependencyGroup);

public record DependabotUpdatePullRequest(
    [property: JsonPropertyName("base-commit-sha")] string BaseCommitSha,
    [property: JsonPropertyName("dependency-names")] List<string> DependencyNames,
    [property: JsonPropertyName("updated-dependency-files")] List<DependabotDependencyFile> DependencyFiles,
    [property: JsonPropertyName("pr-title")] string PrTitle,
    [property: JsonPropertyName("pr-body")] string PrBody,
    [property: JsonPropertyName("commit-message")] string CommitMessage,
    [property: JsonPropertyName("dependency-group")] JsonObject? DependencyGroup);

public record DependabotDependencyFile(
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("content_encoding")] string? ContentEncoding,
    [property: JsonPropertyName("deleted")] bool? Deleted,
    [property: JsonPropertyName("directory")] string? Directory,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("operation")] string? Operation,
    [property: JsonPropertyName("support_file")] bool? SupportFile,
    [property: JsonPropertyName("symlink_target")] string? SymlinkTarget,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("mode")] string? Mode);

public record DependabotClosePullRequest(
    [property: JsonPropertyName("dependency-names")] List<string> DependencyNames,
    [property: JsonPropertyName("reason")] string Reason);

public record DependabotMarkAsProcessed(
    [property: JsonPropertyName("base-commit-sha")] string? BaseCommitSha);

public record DependabotRecordEcosystemVersions(
    [property: JsonPropertyName("ecosystem_versions")] JsonObject? EcosystemVersions);

public record DependabotRecordUpdateJobError(
    [property: JsonPropertyName("error-type")] string ErrorType,
    [property: JsonPropertyName("error-details")] JsonObject? ErrorDetails);

public record DependabotRecordUpdateJobUnknownError(
    [property: JsonPropertyName("error-type")] string ErrorType,
    [property: JsonPropertyName("error-details")] JsonObject? ErrorDetails);

public record DependabotIncrementMetric(
    [property: JsonPropertyName("metric")] string? Metric,
    [property: JsonPropertyName("tags")] JsonObject? Tags);

[JsonConverter(typeof(JsonStringEnumMemberConverter<DependabotOperationType>))]
public enum DependabotOperationType
{
    [EnumMember(Value = "create_pull_request")] CreatePullRequest,
    [EnumMember(Value = "update_pull_request")] UpdatePullRequest,
    [EnumMember(Value = "close_pull_request")] ClosePullRequest,
    [EnumMember(Value = "record_update_job_error")] RecordUpdateJobError,
    [EnumMember(Value = "record_update_job_unknown_error")] RecordUpdateJobUnknownError,
    [EnumMember(Value = "mark_as_processed")] MarkAsProcessed,
    [EnumMember(Value = "update_dependency_list")] UpdateDependencyList,
    [EnumMember(Value = "record_ecosystem_versions")] RecordEcosystemVersions,
    [EnumMember(Value = "increment_metric")] IncrementMetric,
}
