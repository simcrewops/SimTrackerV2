namespace SimCrewOps.Sync.Sync;

/// <summary>
/// Thread-safe mutable holder for the tracker API key returned by the bootstrap endpoint
/// (<c>GET /api/tracker/next-trip</c>).
///
/// The key is <c>null</c> until the first successful bootstrap response that includes one.
/// Uploaders check this store on every request and prefer the tracker API key over the static
/// pilot Bearer token when one is available — this lets the webapp validate a
/// per-session/scoped credential on <c>/api/tracker/position</c> and <c>/api/sim-sessions</c>.
/// </summary>
public sealed class TrackerApiKeyStore
{
    private volatile string? _apiKey;

    /// <summary>
    /// The most recently received tracker API key, or <c>null</c> when no key has been
    /// returned by the bootstrap endpoint yet.
    /// </summary>
    public string? ApiKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }
}
