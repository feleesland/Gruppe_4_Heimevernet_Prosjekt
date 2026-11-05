using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.DataAccess;

// Requests to owners: "can your resource cover this need line?" (same pattern as IResourceRepository)
public interface IResourceRequestRepository
{
    // One need line with its need (to check access and urgency)
    ResourceNeedItem? GetItem(int itemId);

    // All requests for all lines in one need
    IReadOnlyCollection<ResourceRequest> GetForNeed(int needId);

    // Resources that are lent out right now (accepted and not returned)
    IReadOnlySet<int> GetResourceIdsInUse();

    // Sends requests for one line. Skips resources of another type and those already asked. Returns how many were sent
    int Create(int itemId, IEnumerable<int> resourceIds, string requestedById, DateTime answerByUtc);
}