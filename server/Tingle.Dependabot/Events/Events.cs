using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Events;

public record ProjectCreatedEvent : AbstractProjectEvent { }
public record ProjectUpdatedEvent : AbstractProjectEvent { }
public record ProjectDeletedEvent : AbstractProjectEvent { }

public record RepositoryCreatedEvent : AbstractRepositoryEvent { }
public record RepositoryUpdatedEvent : AbstractRepositoryEvent { }
public record RepositoryDeletedEvent : AbstractRepositoryEvent { }

public record RunUpdateJobEvent : AbstractRepositoryEvent
{
    /// <summary>
    /// Identifier of the repository update.
    /// When <see langword="null"/> all updates in the repository are scheduled to run.
    /// </summary>
    public required int RepositoryUpdateId { get; set; }

    /// <summary>The trigger.</summary>
    public required UpdateJobTrigger Trigger { get; set; }

    /// <summary>
    /// Name of the dependency group to be refreshed.
    /// This is only set when we detect merge conflicts and trigger a refresh.
    /// </summary>
    public string? DependencyGroupToRefresh { get; set; } // TODO: find where to set this
}

public abstract record AbstractRepositoryEvent : AbstractProjectEvent
{
    /// <summary>Identifier of the repository.</summary>
    public required string? RepositoryId { get; set; }
}

public abstract record AbstractProjectEvent
{
    /// <summary>Identifier of the project.</summary>
    public required string? ProjectId { get; set; }
}
