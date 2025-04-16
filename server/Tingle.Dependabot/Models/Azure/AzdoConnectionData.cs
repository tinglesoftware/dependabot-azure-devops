using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public record AzdoConnectionData(
    [property: JsonPropertyName("authenticatedUser")] AzdoIdentity AuthenticatedUser,
    [property: JsonPropertyName("authorizedUser")] AzdoIdentity AuthorizedUser);

public record AzdoIdentity(
    [property: JsonPropertyName("id")] string Id);
