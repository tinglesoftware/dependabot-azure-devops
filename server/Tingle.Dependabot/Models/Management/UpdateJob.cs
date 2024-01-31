using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
    [Required]
    [JsonIgnore] // only for internal use
    public string? ProjectId { get; set; }

    /// <summary>Identifier of the repository.</summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? RepositoryId { get; set; }

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
    public string? PackageEcosystem { get; set; }

    /// <summary>Identifier of the repository update.</summary>
    [Required]
    public string? Directory { get; set; }

    /// <summary>Resources provisioned for the update.</summary>
    [Required]
    public UpdateJobResources? Resources { get; set; }

    /// <summary>Image used for the updater.</summary>
    public string? UpdaterImage { get; set; }

    /// <summary>
    /// Authorization key for the job.
    /// Used by the updater to make API calls.
    /// </summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? AuthKey { get; set; }

    /// <summary>When the job started.</summary>
    public DateTimeOffset? Start { get; set; }

    /// <summary>When the job ended.</summary>
    public DateTimeOffset? End { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public long? Duration { get; set; }

    /// <summary>Detailed log output.</summary>
    public string? Log { get; set; }

    /// <summary>Error recorded by the job, if any.</summary>
    public UpdateJobError? Error { get; set; }

    [Timestamp]
    public byte[]? Etag { get; set; }
}

public class UpdateJobError
{
    [Required]
    public string? Type { get; set; }
    public JsonNode? Detail { get; set; }
}
