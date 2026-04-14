using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.Hosting.Hosting;

/// <summary>
/// Polls GET /api/tracker/live-flights every 5 seconds and raises <see cref="PositionsUpdated"/>
/// with the latest fleet positions for rendering on the live map canvas.
/// </summary>
public sealed class LiveMapService : IAsyncDisposable
{
    internal const string LiveFlightsPath = "/api/tracker/live-flights";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string? _pilotApiToken;

    private CancellationTokenSource? _loopCancellationSource;
    private Task? _loopTask;

    public LiveMapService(HttpClient httpClient, Uri baseUri, string? pilotApiToken = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUri);

        _httpClient = httpClient;
        _baseUri = baseUri;
        _pilotApiToken = pilotApiToken;
    }

    /// <summary>Raised on the thread-pool whenever a fresh fleet snapshot arrives.</summary>
    public event EventHandler<IReadOnlyList<LiveFlight>>? PositionsUpdated;

    public bool IsRunning => _loopTask is not null;

    public void Start()
    {
        if (_loopTask is not null)
            return;

        _loopCancellationSource = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCancellationSource.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is null)
            return;

        _loopCancellationSource!.Cancel();

        try
        {
            await _loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_loopCancellationSource.IsCancellationRequested)
        {
        }
        finally
        {
            _loopCancellationSource.Dispose();
            _loopCancellationSource = null;
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _httpClient.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        // Poll immediately on start, then every PollInterval.
        await PollAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await PollAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, LiveFlightsPath));

            if (!string.IsNullOrWhiteSpace(_pilotApiToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _pilotApiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var flights = await response.Content
                .ReadFromJsonAsync<List<LiveFlight>>(cancellationToken)
                .ConfigureAwait(false);

            if (flights is not null)
                PositionsUpdated?.Invoke(this, flights.AsReadOnly());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("LiveMapService poll failed: {0}", ex.Message);
        }
    }
}
