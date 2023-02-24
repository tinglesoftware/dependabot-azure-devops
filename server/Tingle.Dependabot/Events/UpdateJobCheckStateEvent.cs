namespace Tingle.Dependabot.Events;

public record UpdateJobCheckStateEvent : AbstractUpdateJobEvent { }

public record UpdateJobCollectLogsEvent : AbstractUpdateJobEvent { }

public abstract record AbstractUpdateJobEvent
{
    /// <summary>Identifier of the job.</summary>
    public required string? JobId { get; set; }
}
