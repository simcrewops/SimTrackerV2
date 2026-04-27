using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;
using SimCrewOps.Runtime.Runtime;
using SimCrewOps.Tracking.Models;

namespace SimCrewOps.Persistence.Persistence;

public sealed class PersistentRuntimeCoordinator
{
    private readonly RuntimeCoordinator _runtimeCoordinator;
    private readonly IFlightSessionStore _flightSessionStore;

    private bool _completedSessionQueued;

    public PersistentRuntimeCoordinator(
        RuntimeCoordinator runtimeCoordinator,
        IFlightSessionStore flightSessionStore)
    {
        ArgumentNullException.ThrowIfNull(runtimeCoordinator);
        ArgumentNullException.ThrowIfNull(flightSessionStore);

        _runtimeCoordinator = runtimeCoordinator;
        _flightSessionStore = flightSessionStore;
    }

    public async Task<PersistentRuntimeFrameResult> ProcessFrameAsync(
        TelemetryFrame telemetryFrame,
        CancellationToken cancellationToken = default)
    {
        var runtimeFrame = await _runtimeCoordinator
            .ProcessFrameAsync(telemetryFrame, cancellationToken)
            .ConfigureAwait(false);

        var persistence = await PersistRuntimeStateAsync(runtimeFrame.State, cancellationToken).ConfigureAwait(false);

        return new PersistentRuntimeFrameResult
        {
            RuntimeFrame = runtimeFrame,
            Persistence = persistence,
        };
    }

    public async Task<SessionRecoverySnapshot> GetRecoverySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var currentSession = await _flightSessionStore.LoadCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
        var pendingCompletedSessions = await _flightSessionStore.ListCompletedSessionsAsync(cancellationToken).ConfigureAwait(false);

        return new SessionRecoverySnapshot
        {
            CurrentSession = currentSession,
            PendingCompletedSessions = pendingCompletedSessions,
        };
    }

    /// <summary>
    /// Hot-swaps the live position uploader on the underlying coordinator.
    /// Called when the pilot API token is saved in settings without restarting the app.
    /// </summary>
    public void UpdateLivePositionUploader(ILivePositionUploader? uploader)
        => _runtimeCoordinator.UpdateLivePositionUploader(uploader);

    /// <summary>UTC timestamp of the last live-position upload that the server accepted.</summary>
    public DateTimeOffset? LastSuccessfulUploadUtc => _runtimeCoordinator.LastSuccessfulUploadUtc;

    /// <summary>
    /// Updates the flight session context with data fetched from the web app
    /// (departure/arrival ICAO, flight number, etc.). No-op if a flight is already in progress.
    /// </summary>
    public FlightSessionContext CurrentContext
        => _runtimeCoordinator.CurrentContext;

    public void UpdateContext(FlightSessionContext context)
        => _runtimeCoordinator.UpdateContext(context);

    /// <summary>
    /// Updates only the aircraft type and category from a SimConnect detection event.
    /// Bypasses the blocks-off guard — see <see cref="RuntimeCoordinator.UpdateAircraftType"/>.
    /// </summary>
    public void UpdateAircraftType(string aircraftType, string aircraftCategory)
        => _runtimeCoordinator.UpdateAircraftType(aircraftType, aircraftCategory);

    public void Restore(FlightSessionRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _runtimeCoordinator.Restore(state);
        _completedSessionQueued = state.IsComplete;
    }

    public Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        _completedSessionQueued = false;
        return _flightSessionStore.ClearCurrentSessionAsync(cancellationToken);
    }

    /// <summary>
    /// Clears the persisted session and resets the coordinator to a clean
    /// Preflight state, preserving the current flight context.  Called when
    /// a completed session is detected at the start of a new SimConnect
    /// connection so the new flight is not stuck in the previous Arrival phase.
    /// </summary>
    public async Task ResetForNewSessionAsync(CancellationToken cancellationToken = default)
    {
        _completedSessionQueued = false;
        await _flightSessionStore.ClearCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
        _runtimeCoordinator.ResetForNewSession();
    }

    private async Task<SessionPersistenceResult> PersistRuntimeStateAsync(
        FlightSessionRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!state.IsComplete)
        {
            _completedSessionQueued = false;
            await _flightSessionStore.SaveCurrentSessionAsync(state, cancellationToken).ConfigureAwait(false);

            return new SessionPersistenceResult
            {
                CurrentSessionSaved = true,
            };
        }

        if (_completedSessionQueued)
        {
            return new SessionPersistenceResult();
        }

        var queuedCompletedSession = await _flightSessionStore
            .QueueCompletedSessionAsync(state, cancellationToken)
            .ConfigureAwait(false);

        await _flightSessionStore.ClearCurrentSessionAsync(cancellationToken).ConfigureAwait(false);
        _completedSessionQueued = true;

        return new SessionPersistenceResult
        {
            CurrentSessionCleared = true,
            QueuedCompletedSession = queuedCompletedSession,
        };
    }
}
