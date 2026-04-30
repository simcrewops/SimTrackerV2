using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public sealed class HttpPreflightChecker : IPreflightChecker
{
    public const string PreflightPath = "/api/pilot/preflight";

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
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri(_options.BaseUri, PreflightPath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PilotApiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Trace.TraceWarning(
                    "Preflight check failed with HTTP {0}.",
                    (int)response.StatusCode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<PreflightStatusResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            Trace.TraceWarning("Preflight check failed: {0}", ex.Message);
            return null;
        }
    }
}
