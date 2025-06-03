using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.GitHub;

public record GhsaGraphQlRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("variables")] GhsaGraphQlRequestVariables Variables);

public record GhsaGraphQlRequestVariables(
    [property: JsonPropertyName("ecosystem")] string Ecosystem,
    [property: JsonPropertyName("package")] string Package);

public record GhsaGraphQlResponse(
    [property: JsonPropertyName("data")] GhsaGraphQlData? Data,
    [property: JsonPropertyName("errors")] List<GhsaGraphQlError>? Errors);

public record GhsaGraphQlError(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonExtensionData] Dictionary<string, object> Extensions);

public record GhsaGraphQlData(
    [property: JsonPropertyName("securityVulnerabilities")] GhsaVulnerabilitiesWrapper SecurityVulnerabilities);

public record GhsaVulnerabilitiesWrapper(
    [property: JsonPropertyName("nodes")] List<GhsaSecurityVulnerabilityNode> Nodes);

public record GhsaSecurityVulnerabilityNode(
    [property: JsonPropertyName("advisory")] GitHubSecurityAdvisory Advisory,
    [property: JsonPropertyName("vulnerableVersionRange")] string VulnerableVersionRange,
    [property: JsonPropertyName("firstPatchedVersion")] GhsaPatchedVersion FirstPatchedVersion);

public record GhsaPatchedVersion(
    [property: JsonPropertyName("identifier")] string Identifier);
