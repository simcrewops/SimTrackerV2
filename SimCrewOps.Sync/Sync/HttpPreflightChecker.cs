using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

/// <summary>
/// Calls GET /api/pilot/preflight with the pilot's static Bearer token and returns
/// the current grounding / crew-rest status.  Returns null on auth failure, network
/// error, or when the server returns a non-success status (treated as "no block").
/// </summary>
public interface IPreflightChecker
{
    Task<PreflightStatusResponse?> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class HttpPreflightChecker : IPreflightChecker
{
    internal const string PreflightPath = "/api/pilot/preflight";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly SimCrewOpsApiUploaderOptions _options;

    public HttpPreflightChecker(HttpClient httpClient, SimCrewOpsApiUploaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<PreflightStatusResponse?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var requestUri = new Uri(_options.BaseUri, PreflightPath);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PilotApiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content
                .ReadFromJsonAsync<PreflightStatusResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Network failure or bad JSON — silently return null; don't block the tracker.
            return null;
        }
    }
}
