namespace SimCrewOps.PhaseEngine.Models;

public sealed class BlockEvent
{
    public BlockEventType Type { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public double LatitudeDeg { get; init; }
    public double LongitudeDeg { get; init; }
}
