namespace SimCrewOps.Sync.Sync;

public sealed record SimCrewOpsApiUploaderOptions
{
    public Uri BaseUri { get; init; } = new("https://simcrewops.com", UriKind.Absolute);
    public string SimSessionsPath { get; init; } = "/api/sim-sessions";
    public required string PilotApiToken { get; init; }
    public string TrackerVersion { get; init; } = "dev";
}
