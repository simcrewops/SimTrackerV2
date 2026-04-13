using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Persistence.Models;

public sealed record PersistentRuntimeFrameResult
{
    public required RuntimeFrameResult RuntimeFrame { get; init; }
    public required SessionPersistenceResult Persistence { get; init; }
}
