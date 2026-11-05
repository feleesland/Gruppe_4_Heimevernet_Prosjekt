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

    // Sends requests for one line. Skips resources of another type and those already asked.
    // Returns the resources that got a request (so their owners can be told)
    IReadOnlyList<int> Create(int itemId, IEnumerable<int> resourceIds, string requestedById, DateTime answerByUtc);

    // The owner's requests, newest first (for "Mine forespørsler" and the bell)
    IReadOnlyList<OwnerRequest> GetForOwner(string ownerId);

    // The owner answers. Checks that the request is theirs and still open; the first ones who say yes get the job
    AnswerResult Answer(int requestId, string ownerId, bool yes, DeliveryMethod? delivery, string? comment);
}

// One request as the owner sees it
public record OwnerRequest(
    int Id, string ResourceName, int NeedId, string NeedName, string NeedPlace,
    NeedUrgency Urgency, DateTime? NeededFrom, string LineType, int LineQuantity,
    RequestStatus Status, DateTime SentAt, DateTime AnswerBy, DeliveryMethod? Delivery, string? Comment)
{
    // Waiting for the owner's answer, and the deadline has not passed
    public bool IsOpen(DateTime nowUtc) => Status == RequestStatus.Sendt && AnswerBy >= nowUtc;
}

public enum AnswerResult { Accepted, Declined, AlreadyCovered, NotOpen, NotFound }