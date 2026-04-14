using System.Diagnostics;
using System.Net.Http.Json;
using SimCrewOps.App.Wpf.Models;
using SimCrewOps.Hosting.Models;

namespace SimCrewOps.App.Wpf.Services;

/// <summary>
/// Polls GET /api/flights/live on a 5-second interval and raises
/// <see cref="PositionsUpdated"/> with the latest list of active flights.
/// Polling is skipped when <see cref="TrackerApiSettings.LiveMapEnabled"/> is false.
/// Note: the event is raised from a background thread — subscribers must marshal
/// to the UI thread (e.g. via Dispatcher.InvokeAsync) before touching WPF elements.
/// </summary>
public sealed class LiveMapService : IAsyncDisposable
{
    private const string LiveFlightsPath = "/api/flights/live";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly bool _enabled;
    private readonly string? _currentPilotId;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public LiveMapService(HttpClient httpClient, TrackerApiSettings apiSettings)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(apiSettings);

        _httpClient = httpClient;
        _baseUri = apiSettings.BaseUri;
        _enabled = apiSettings.LiveMapEnabled;
        _currentPilotId = apiSettings.CurrentPilotId;
    }

    /// <summary>
    /// Raised on every successful (or empty) poll. The argument is the current
    /// list of active flights; an empty list means no flights or a failed fetch.
    /// </summary>
    public event EventHandler<IReadOnlyList<LiveFlight>>? PositionsUpdated;

    public bool IsRunning => _loopTask is not null;

    public void Start()
    {
        if (!_enabled || _loopTask is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_loopTask is null)
        {
            return;
        }

        await _cts!.CancelAsync().ConfigureAwait(false);

        try
        {
            await _loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        // Fire immediately on start, then every PollInterval.
        var flights = await FetchFlightsAsync(cancellationToken).ConfigureAwait(false);
        PositionsUpdated?.Invoke(this, flights);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            flights = await FetchFlightsAsync(cancellationToken).ConfigureAwait(false);
            PositionsUpdated?.Invoke(this, flights);
        }
    }

    private async Task<IReadOnlyList<LiveFlight>> FetchFlightsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var url = new Uri(_baseUri, LiveFlightsPath);
            var flights = await _httpClient
                .GetFromJsonAsync<List<LiveFlight>>(url, cancellationToken)
                .ConfigureAwait(false);

            if (flights is null)
            {
                return [];
            }

            if (!string.IsNullOrEmpty(_currentPilotId))
            {
                foreach (var flight in flights)
                {
                    flight.IsMyFlight = string.Equals(flight.PilotId, _currentPilotId, StringComparison.Ordinal);
                }
            }

            return flights;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("LiveMapService: failed to fetch live flights: {0}", ex.Message);
            return [];
        }
    }
}
