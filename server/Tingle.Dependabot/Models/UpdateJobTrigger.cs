using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum UpdateJobTrigger
{
    Scheduled = 0,

    [EnumMember(Value = "missed_schedule")]
    MissedSchedule = 1,

    Synchronization = 2,

    Manual = 3,
}
