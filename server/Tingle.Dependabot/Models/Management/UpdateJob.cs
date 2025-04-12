﻿using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.Dependabot.Workflow;
using Tingle.Extensions.Primitives;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Management;

// This class independent of one-to-many relationships for detached and prolonged tracking.
// The records are cleaned up on a schedule.
public class UpdateJob
{
    [Key, MaxLength(50)]
    public string? Id { get; set; }

    public DateTimeOffset Created { get; set; }

    /// <summary>Status of the update job.</summary>
    public UpdateJobStatus Status { get; set; }

    /// <summary>Trigger for the update job.</summary>
    public UpdateJobTrigger Trigger { get; set; }

    /// <summary>Identifier of the project.</summary>
    [JsonIgnore] // only for internal use
    public required string ProjectId { get; set; }

    /// <summary>Identifier of the repository.</summary>
    [JsonIgnore] // only for internal use
    public required string RepositoryId { get; set; }

    /// <summary>Slug of the repository.</summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? RepositorySlug { get; set; }

    /// <summary>Identifier of the event on the EventBus, if any.</summary>
    [JsonIgnore] // only for internal use
    public string? EventBusId { get; set; }

    /// <summary>
    /// Commit SHA of the configuration file used for the update.
    /// </summary>
    /// <example>1dabbdfa71465a6eb6c0b44be9f3f6461b4b35e2</example>
    [MaxLength(50)]
    public string? Commit { get; set; }

    /// <summary>Ecosystem for the update.</summary>
    [JsonIgnore] // only for internal use
    public required string PackageEcosystem { get; set; }

    /// <summary>Directory targeted by the repository update.</summary>
    public string? Directory { get; set; }

    /// <summary>Directories targeted by the repository update.</summary>
    public List<string>? Directories { get; set; }

    /// <summary>Resources provisioned for the update.</summary>
    public required UpdateJobResources Resources { get; set; }

    /// <summary>Image used for the proxy.</summary>
    public DockerImage? ProxyImage { get; set; }

    /// <summary>Image used for the updater.</summary>
    public DockerImage? UpdaterImage { get; set; }

    /// <summary>
    /// Authorization key for the job.
    /// Used by the updater to make API calls.
    /// </summary>
    [JsonIgnore] // only for internal use
    public required string AuthKey { get; set; }

    /// <summary>When the job started.</summary>
    public DateTimeOffset? Start { get; set; }

    /// <summary>When the job ended.</summary>
    public DateTimeOffset? End { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public long? Duration { get; set; }

    /// <summary>Detailed log output.</summary>
    [JsonIgnore]
    public string? Log { get; set; }

    /// <summary>Error recorded by the job, if any.</summary>
    public UpdateJobError? Error { get; set; }

    [Timestamp]
    public Etag? Etag { get; set; } // TODO: remove nullability once we reset the migrations

    public string ResourceName => $"dependabot-{Id}";
    public string ResourceNameProxy => $"{ResourceName}-proxy";
}

public class UpdateJobError
{
    public required string Type { get; set; }
    public JsonNode? Detail { get; set; }
}

[JsonConverter(typeof(JsonStringEnumMemberConverter<UpdateJobTrigger>))]
public enum UpdateJobTrigger
{
    Scheduled = 0,

    [EnumMember(Value = "missed_schedule")]
    MissedSchedule = 1,

    Synchronization = 2,

    Manual = 3,
}

public enum UpdateJobStatus
{
    Scheduled = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

public class UpdateJobResources
{
    // the minimum is 0.25vCPU and 0.5GB but we need more because a lot is happening in the container
    private static readonly UpdateJobResources Default = new(cpu: 0.5, memory: 1);

    public UpdateJobResources() { } // required for deserialization

    public UpdateJobResources(double cpu, double memory)
    {
        // multiplication by 100 to avoid the approximation
        if (memory * 100 % (0.1 * 100) != 0)
        {
            throw new ArgumentException("The memory requirement should be in increments of 0.1.", nameof(memory));
        }

        Cpu = cpu;
        Memory = memory;
    }

    /// <summary>CPU units provisioned.</summary>
    /// <example>0.25</example>
    public double Cpu { get; set; }

    /// <summary>Memory provisioned in GB.</summary>
    /// <example>1.2</example>
    public double Memory { get; set; }

    public static UpdateJobResources FromEcosystem(string ecosystem)
    {
        return ecosystem switch
        {
            "npm" => Default * 2,
            "yarn" => Default * 2,
            "pnpm" => Default * 2,
            _ => Default,
        };
    }

    public static UpdateJobResources operator *(UpdateJobResources resources, double factor) => new(resources.Cpu * factor, resources.Memory * factor);
    public static UpdateJobResources operator /(UpdateJobResources resources, double factor) => new(resources.Cpu / factor, resources.Memory / factor);
}
