using SimCrewOps.Persistence.Models;
using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public interface ICompletedSessionUploader
{
    Task<CompletedSessionUploadResult> UploadAsync(
        PendingCompletedSession session,
        CancellationToken cancellationToken = default);
}
