using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AzdoProjectVisibility
{
    Private,
    Organization,
    Public,
    SystemPrivate,
}
