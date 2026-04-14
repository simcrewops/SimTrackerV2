using System.Diagnostics;
using System.Net.Http.Json;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public sealed class WebSyncService : ILivePositionUploader
{
    internal const string TrackerPositionPath = "/api/tracker/position";

    private static readonly HttpClient _httpClient = new();

    private readonly SimCrewOpsApiUploaderOptions _options;

    public WebSyncService(SimCrewOpsApiUploaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!_options.LiveSyncEnabled || string.IsNullOrWhiteSpace(_options.PilotApiToken))
        {
            return false;
        }

        var positionPayload = new PositionPayload
        {
            Token = _options.PilotApiToken,
            Callsign = payload.BidId ?? string.Empty,
            Latitude = payload.Latitude,
            Longitude = payload.Longitude,
            Altitude = payload.AltitudeFt,
            Heading = payload.HeadingMagnetic,
            GroundSpeed = payload.GroundSpeedKts,
            Phase = payload.Phase,
            FlightId = payload.BidId,
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            var uri = new Uri(_options.BaseUri, TrackerPositionPath);
            using var response = await _httpClient
                .PostAsJsonAsync(uri, positionPayload, timeoutCts.Token)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebSyncService] Position sync failed: {ex.Message}");
            return false;
        }
    }
}
