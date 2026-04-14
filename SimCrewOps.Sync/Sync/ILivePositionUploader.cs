using SimCrewOps.Sync.Models;

namespace SimCrewOps.Sync.Sync;

public interface ILivePositionUploader
{
    Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default);
}
