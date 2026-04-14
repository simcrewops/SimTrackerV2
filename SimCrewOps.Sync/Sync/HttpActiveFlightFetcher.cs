using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

/// <summary>
/// Calls GET /api/tracker/next-trip with the pilot's static API token
/// and deserialises the response into an <see cref="ActiveFlightResponse"/>.
///
/// Returns null on auth failure, network error, or when the pilot has no upcoming flight.
/// </summary>
public sealed class HttpActiveFlightFetcher : IActiveFlightFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly SimCrewOpsApiUploaderOptions _options;

    public HttpActiveFlightFetcher(HttpClient httpClient, SimCrewOpsApiUploaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<ActiveFlightResponse?> FetchAsync(CancellationToken cancellationToken = default)
    {
        var requestUri = new Uri(_options.BaseUri, "/api/tracker/next-trip");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PilotApiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content
                .ReadFromJsonAsync<ActiveFlightResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            // Backend returns { source: null } when nothing is queued — treat as "no flight".
            return string.IsNullOrEmpty(result?.Source) ? null : result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Network failure or bad JSON — silently return null, UI will show "no flight".
            return null;
        }
    }
}
