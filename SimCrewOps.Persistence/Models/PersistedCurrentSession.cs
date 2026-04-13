using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Persistence.Models;

public sealed record PersistedCurrentSession
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required DateTimeOffset SavedUtc { get; init; }
    public required FlightSessionRuntimeState State { get; init; }
}
