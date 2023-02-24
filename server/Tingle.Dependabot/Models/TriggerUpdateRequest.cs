using System.ComponentModel.DataAnnotations;

namespace Tingle.Dependabot.Models;

/// <summary>
/// Represents a model for triggering an update job.
/// </summary>
public class TriggerUpdateRequest
{
    /// <summary>
    /// Index of the repository update.
    /// </summary>
    [Required]
    public int? Id { get; set; }
}
