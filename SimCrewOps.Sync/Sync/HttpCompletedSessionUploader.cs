using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimCrewOps.Persistence.Models;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

// Internal shape of the POST /api/sim-sessions 201 response body.
file sealed record SimSessionCreatedResponse
{
    public string? Id { get; init; }

    [JsonPropertyName("postFlight")]
    public PostFlightStatus? PostFlight { get; init; }
}

public sealed class HttpCompletedSessionUploader : ICompletedSessionUploader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly SimCrewOpsApiUploaderOptions _options;
    private readonly SimSessionUploadRequestMapper _requestMapper;
    private readonly TrackerApiKeyStore? _apiKeyStore;

    public HttpCompletedSessionUploader(
        HttpClient httpClient,
        SimCrewOpsApiUploaderOptions options,
        SimSessionUploadRequestMapper? requestMapper = null,
        TrackerApiKeyStore? apiKeyStore = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _requestMapper = requestMapper ?? new SimSessionUploadRequestMapper();
        _apiKeyStore = apiKeyStore;
    }

    public async Task<CompletedSessionUploadResult> UploadAsync(
        PendingCompletedSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var requestBody = _requestMapper.Map(session, _options.TrackerVersion);
        var requestUri = BuildRequestUri();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(requestBody),
            };
            // Prefer the per-session tracker API key (from bootstrap) when available.
            var authToken = _apiKeyStore?.ApiKey ?? _options.PilotApiToken;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                // Parse the response body to extract post-flight status (strike issued, grounding).
                PostFlightStatus? postFlight = null;
                try
                {
                    var body = await response.Content
                        .ReadFromJsonAsync<SimSessionCreatedResponse>(JsonOptions, cancellationToken)
                        .ConfigureAwait(false);
                    postFlight = body?.PostFlight;
                }
                catch (JsonException)
                {
                    // Body parse failure is non-fatal — the upload itself succeeded.
                }

                return new CompletedSessionUploadResult
                {
                    Status = SessionUploadStatus.Success,
                    StatusCode = (int)response.StatusCode,
                    PostFlightStatus = postFlight,
                };
            }

            var responseBody = response.Content is null
                ? null
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new CompletedSessionUploadResult
            {
                Status = ClassifyFailure(response.StatusCode),
                StatusCode = (int)response.StatusCode,
                ErrorMessage = BuildErrorMessage(response.StatusCode, responseBody),
            };
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            return new CompletedSessionUploadResult
            {
                Status = SessionUploadStatus.RetryableFailure,
                ErrorMessage = ex.Message,
            };
        }
    }

    private Uri BuildRequestUri()
    {
        if (Uri.TryCreate(_options.SimSessionsPath, UriKind.RelativeOrAbsolute, out var absoluteUri) &&
            absoluteUri.IsAbsoluteUri &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri;
        }

        return new Uri(_options.BaseUri, _options.SimSessionsPath);
    }

    private static SessionUploadStatus ClassifyFailure(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        if (numeric == 408 || numeric == 429 || numeric >= 500)
        {
            return SessionUploadStatus.RetryableFailure;
        }

        return SessionUploadStatus.PermanentFailure;
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var prefix = $"Upload failed with HTTP {(int)statusCode}.";
        return string.IsNullOrWhiteSpace(responseBody)
            ? prefix
            : $"{prefix} {responseBody.Trim()}";
    }
}
