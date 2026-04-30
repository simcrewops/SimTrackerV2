using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Persistence.Models;

public sealed record PersistentRuntimeFrameResult
{
    public required RuntimeFrameResult RuntimeFrame { get; init; }
    public required SessionPersistenceResult Persistence { get; init; }

    /// <summary>True when the underlying runtime detected a reposition and auto-reset the session.</summary>
    public bool WasRepositionReset => RuntimeFrame.WasRepositionReset;
}
