namespace SimCrewOps.Hosting.Models;

public sealed record TrackerApiSettings
{
    public Uri BaseUri { get; init; } = new("https://simcrewops.com", UriKind.Absolute);
    public string SimSessionsPath { get; init; } = "/api/sim-sessions";
    public string? PilotApiToken { get; init; }
    public string TrackerVersion { get; init; } = "dev";
}
