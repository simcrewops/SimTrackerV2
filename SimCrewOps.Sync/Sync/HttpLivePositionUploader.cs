using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;

namespace SimCrewOps.Sync.Sync;

public sealed class HttpLivePositionUploader : ILivePositionUploader
{
    internal const string TrackerPositionPath = "/api/tracker/position";

    private readonly HttpClient _httpClient;
    private readonly SimCrewOpsApiUploaderOptions _options;
    private readonly TrackerApiKeyStore? _apiKeyStore;

    public HttpLivePositionUploader(
        HttpClient httpClient,
        SimCrewOpsApiUploaderOptions options,
        TrackerApiKeyStore? apiKeyStore = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _apiKeyStore = apiKeyStore;
    }

    public async Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseUri, TrackerPositionPath))
            {
                Content = JsonContent.Create(payload),
            };
            // Prefer the per-session tracker API key (from bootstrap) when available.
            var authToken = _apiKeyStore?.ApiKey ?? _options.PilotApiToken;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }

            var responseBody = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            Trace.TraceWarning(
                "Tracker live position upload failed with HTTP {0}. {1}",
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(responseBody) ? string.Empty : responseBody.Trim());
            return false;
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning("Tracker live position upload failed: {0}", ex.Message);
            return false;
        }
    }
}
