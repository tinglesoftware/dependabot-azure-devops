namespace Tingle.Dependabot.Models;

/// <summary>
/// Represents a model for processing a synchronization request
/// </summary>
public class SynchronizationRequest
{
    /// <summary>
    /// Indicates whether we should trigger the update jobs where changes have been detected.
    /// </summary>
    public bool Trigger { get; set; }
}
