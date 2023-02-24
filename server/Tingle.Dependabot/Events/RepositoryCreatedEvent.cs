using Tingle.Dependabot.Models;

namespace Tingle.Dependabot.Events;

public record RepositoryCreatedEvent : AbstractRepositoryEvent { }

public record RepositoryUpdatedEvent : AbstractRepositoryEvent { }

public record RepositoryDeletedEvent : AbstractRepositoryEvent { }

public record TriggerUpdateJobsEvent : AbstractRepositoryEvent
{
    /// <summary>
    /// Optional identifier of the repository update.
    /// When <see langword="null"/> all updates in the repository are scheduled to run.
    /// </summary>
    public int? RepositoryUpdateId { get; set; }

    /// <summary>The trigger.</summary>
    public required UpdateJobTrigger Trigger { get; set; }
}

public abstract record AbstractRepositoryEvent
{
    /// <summary>Identifier of the repository.</summary>
    public required string? RepositoryId { get; set; }
}
