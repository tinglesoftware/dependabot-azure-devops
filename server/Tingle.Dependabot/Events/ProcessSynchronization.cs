namespace Tingle.Dependabot.Events;

public record ProcessSynchronization
{
    public ProcessSynchronization() { } // required for deserialization

    public ProcessSynchronization(string projectId, bool trigger, string? repositoryId = null, string? repositoryProviderId = null)
    {
        ProjectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        Trigger = trigger;
        RepositoryId = repositoryId;
        RepositoryProviderId = repositoryProviderId;
    }

    /// <summary>Identifier of the project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Indicates whether we should trigger the update jobs where changes have been detected.
    /// </summary>
    public bool Trigger { get; set; }

    /// <summary>
    /// Identifier of the repository.
    /// Required if <see cref="RepositoryProviderId"/> is not supplied.
    /// </summary>
    public string? RepositoryId { get; set; }

    /// <summary>
    /// Identifier of the repository as given by the provider.
    /// Required if <see cref="RepositoryId"/> is not supplied.
    /// </summary>
    public string? RepositoryProviderId { get; set; }
}
