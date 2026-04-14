using SimCrewOps.Runtime.Models;

namespace SimCrewOps.Runtime.Runtime;

public interface ILivePositionUploader
{
    Task<bool> SendPositionAsync(LivePositionPayload payload, CancellationToken cancellationToken = default);
}
