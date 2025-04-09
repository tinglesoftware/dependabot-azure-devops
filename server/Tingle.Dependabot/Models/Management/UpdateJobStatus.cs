using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Management;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum UpdateJobStatus
{
    Scheduled = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}
