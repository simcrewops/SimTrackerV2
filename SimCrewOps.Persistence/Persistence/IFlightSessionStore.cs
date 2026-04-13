using SimCrewOps.Persistence.Models;
using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Persistence.Persistence;

public interface IFlightSessionStore
{
    Task SaveCurrentSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default);
    Task<PersistedCurrentSession?> LoadCurrentSessionAsync(CancellationToken cancellationToken = default);
    Task ClearCurrentSessionAsync(CancellationToken cancellationToken = default);
    Task<PendingCompletedSession> QueueCompletedSessionAsync(FlightSessionRuntimeState state, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingCompletedSession>> ListCompletedSessionsAsync(CancellationToken cancellationToken = default);
    Task<bool> RemoveCompletedSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
