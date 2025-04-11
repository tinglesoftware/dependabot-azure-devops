using System.Runtime.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public enum AzdoProjectVisibility
{

    [EnumMember(Value = "private")]
    Private,

    [EnumMember(Value = "organization")]
    Organization,

    [EnumMember(Value = "public")]
    Public,

    [EnumMember(Value = "systemPrivate")]
    SystemPrivate,
}
