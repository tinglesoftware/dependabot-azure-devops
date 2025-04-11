using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Management;

[JsonConverter(typeof(JsonStringEnumMemberConverter<UpdateJobTrigger>))]
public enum UpdateJobTrigger
{
    Scheduled = 0,

    [EnumMember(Value = "missed_schedule")]
    MissedSchedule = 1,

    Synchronization = 2,

    Manual = 3,
}

[JsonConverter(typeof(JsonStringEnumMemberConverter<UpdateJobPlatform>))]
public enum UpdateJobPlatform
{
    [EnumMember(Value = "container_apps")]
    ContainerApps = 0,

    // [EnumMember(Value = "container_instances")]
    // ContainerInstances = 1,

    // Kubernetes = 2,

    [EnumMember(Value = "docker_compose")]
    DockerCompose = 3,
}
