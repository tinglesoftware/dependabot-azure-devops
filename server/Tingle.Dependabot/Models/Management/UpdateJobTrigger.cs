using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Management;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum UpdateJobTrigger
{
    Scheduled = 0,

    [EnumMember(Value = "missed_schedule")]
    MissedSchedule = 1,

    Synchronization = 2,

    Manual = 3,
}

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum UpdateJobPlatform
{
    [EnumMember(Value = "container_apps")]
    ContainerApps = 0,

    // [EnumMember(Value = "container_instances")]
    // ContainerInstances = 1,

    // Kubernetes = 2,

    DockerCompose = 3,
}
