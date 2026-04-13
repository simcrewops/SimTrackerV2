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

    public Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        _completedSessionQueued = false;
        return _flightSessionStore.ClearCurrentSessionAsync(cancellationToken);
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
