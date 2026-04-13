namespace SimCrewOps.Persistence.Models;

public sealed record SessionPersistenceResult
{
    public bool CurrentSessionSaved { get; init; }
    public bool CurrentSessionCleared { get; init; }
    public PendingCompletedSession? QueuedCompletedSession { get; init; }
}
