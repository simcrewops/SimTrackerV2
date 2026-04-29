using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public interface IPreflightChecker
{
    Task<PreflightStatusResponse?> CheckAsync(CancellationToken cancellationToken = default);
}
