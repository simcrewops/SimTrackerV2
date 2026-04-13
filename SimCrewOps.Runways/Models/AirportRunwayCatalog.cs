namespace SimCrewOps.Runways.Models;

public sealed record AirportRunwayCatalog
{
    public required string AirportIcao { get; init; }
    public required IReadOnlyList<RunwayEnd> Runways { get; init; }
    public RunwayDataSource DataSource { get; init; }
}
