namespace SimCrewOps.Persistence.Models;

public sealed record SessionRecoverySnapshot
{
    public PersistedCurrentSession? CurrentSession { get; init; }
    public IReadOnlyList<PendingCompletedSession> PendingCompletedSessions { get; init; } = Array.Empty<PendingCompletedSession>();

    public bool HasRecoverableCurrentSession => CurrentSession is not null;
    public bool HasPendingCompletedSessions => PendingCompletedSessions.Count > 0;
}
