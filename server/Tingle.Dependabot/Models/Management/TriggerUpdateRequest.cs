namespace Tingle.Dependabot.Models.Management;

/// <summary>
/// Represents a model for triggering an update job.
/// </summary>
public class TriggerUpdateRequest
{
    /// <summary>
    /// Index of the repository update.
    /// </summary>
    public required int Id { get; set; }
}
