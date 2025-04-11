using System.Runtime.Serialization;

namespace Tingle.Dependabot.Models.Management;

public enum UpdateJobTrigger
{
    Scheduled = 0,

    [EnumMember(Value = "missed_schedule")]
    MissedSchedule = 1,

    Synchronization = 2,

    Manual = 3,
}

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
