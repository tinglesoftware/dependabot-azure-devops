using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoConnectionData
{
    [JsonPropertyName("authenticatedUser")]
    public required AzdoIdentity AuthenticatedUser { get; set; }

    [JsonPropertyName("authorizedUser")]
    public required AzdoIdentity AuthorizedUser { get; set; }
}

public class AzdoIdentity
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
}
