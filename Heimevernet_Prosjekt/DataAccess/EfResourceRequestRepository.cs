using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

public class EfResourceRequestRepository(HeimevernetDbContext dbContext) : IResourceRequestRepository
{
    public ResourceNeedItem? GetItem(int itemId)
    {
        return dbContext.ResourceNeedItems
            .Include(item => item.ResourceNeed)
            .AsNoTracking()
            .FirstOrDefault(item => item.Id == itemId);
    }

    public IReadOnlyCollection<ResourceRequest> GetForNeed(int needId)
    {
        return dbContext.ResourceRequests
            .AsNoTracking()
            .Where(request => request.ResourceNeedItem.ResourceNeedId == needId)
            .OrderBy(request => request.SentAt)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlySet<int> GetResourceIdsInUse()
    {
        return dbContext.ResourceRequests
            .AsNoTracking()
            .Where(request => request.Status == RequestStatus.Godtatt && request.ReturnedAt == null)
            .Select(request => request.ResourceId)
            .ToHashSet();
    }

    // Returns the resources that got a request, so their owners can be told
    public IReadOnlyList<int> Create(int itemId, IEnumerable<int> resourceIds, string requestedById, DateTime answerByUtc)
    {
        var item = dbContext.ResourceNeedItems.Find(itemId);
        if (item is null)
        {
            return Array.Empty<int>();
        }

        var ids = resourceIds.Distinct().ToList();
        var now = DateTime.UtcNow;

        // Only real resources of the right type (protects against a changed form)
        var validIds = dbContext.Resources
            .Where(resource => ids.Contains(resource.Id) && resource.Type == item.Type)
            .Select(resource => resource.Id)
            .ToHashSet();

        // Don't ask the same resource twice for the same line, unless the old request was declined, cancelled or not answered
        var alreadyAsked = dbContext.ResourceRequests
            .Where(request => request.ResourceNeedItemId == itemId &&
                              (request.Status == RequestStatus.Godtatt ||
                               (request.Status == RequestStatus.Sendt && request.AnswerBy >= now)))
            .Select(request => request.ResourceId)
            .ToHashSet();

        var created = new List<int>();
        foreach (var resourceId in ids.Where(id => validIds.Contains(id) && !alreadyAsked.Contains(id)))
        {
            dbContext.ResourceRequests.Add(new ResourceRequest
            {
                ResourceNeedItemId = itemId,
                ResourceId = resourceId,
                RequestedById = requestedById,
                SentAt = now,
                AnswerBy = answerByUtc
            });
            created.Add(resourceId);
        }

        dbContext.SaveChanges();
        return created;
    }

    // First the owner's resources, then their requests with the need and the line (two simple queries)
    public IReadOnlyList<OwnerRequest> GetForOwner(string ownerId)
    {
        var names = dbContext.Resources
            .Where(resource => resource.OwnerId == ownerId)
            .Select(resource => new { resource.Id, resource.Name })
            .ToDictionary(resource => resource.Id, resource => resource.Name);
        var ids = names.Keys.ToList();

        return dbContext.ResourceRequests
            .AsNoTracking()
            .Where(request => ids.Contains(request.ResourceId))
            .OrderByDescending(request => request.SentAt)
            .Take(50)
            .Select(request => new
            {
                request.Id,
                request.ResourceId,
                NeedId = request.ResourceNeedItem.ResourceNeedId,
                NeedName = request.ResourceNeedItem.ResourceNeed.Name,
                request.ResourceNeedItem.ResourceNeed.Address,
                request.ResourceNeedItem.ResourceNeed.City,
                request.ResourceNeedItem.ResourceNeed.Urgency,
                request.ResourceNeedItem.ResourceNeed.NeededFrom,
                LineType = request.ResourceNeedItem.Type,
                LineQuantity = request.ResourceNeedItem.Quantity,
                request.Status,
                request.SentAt,
                request.AnswerBy,
                request.Delivery,
                request.Comment
            })
            .ToList()
            .Select(x => new OwnerRequest(
                x.Id, names[x.ResourceId], x.NeedId, x.NeedName,
                string.Join(", ", new[] { x.Address, x.City }.Where(part => !string.IsNullOrWhiteSpace(part))),
                x.Urgency, x.NeededFrom, x.LineType, x.LineQuantity,
                x.Status, x.SentAt, x.AnswerBy, x.Delivery, x.Comment))
            .ToList()
            .AsReadOnly();
    }

    // The owner answers in the app: the request must be theirs, waiting, and within the deadline
    public AnswerResult Answer(int requestId, string ownerId, bool yes, DeliveryMethod? delivery, string? comment)
    {
        var request = Load(requestId);
        if (request is null)
        {
            return AnswerResult.NotFound;
        }

        // Only the owner of the resource can answer
        var resource = dbContext.Resources.Find(request.ResourceId);
        if (resource is null || resource.OwnerId != ownerId)
        {
            return AnswerResult.NotFound;
        }

        if (request.Status != RequestStatus.Sendt || request.AnswerBy < DateTime.UtcNow)
        {
            return AnswerResult.NotOpen;
        }

        return Apply(request, resource, yes, delivery, comment);
    }

    // The operator registers the owner's answer from the phone (yes or no).
    // Allowed after the deadline too, as long as nobody has answered
    public AnswerResult ConfirmByPhone(int requestId, bool yes, DeliveryMethod? delivery)
    {
        var request = Load(requestId);
        if (request is null)
        {
            return AnswerResult.NotFound;
        }

        var resource = dbContext.Resources.Find(request.ResourceId);
        if (resource is null)
        {
            return AnswerResult.NotFound;
        }

        if (request.Status != RequestStatus.Sendt)
        {
            return AnswerResult.NotOpen;
        }

        request.ConfirmedByPhone = true;
        return Apply(request, resource, yes, delivery, null);
    }

    // Undo a phone answer (a misclick): back to "Venter", as if nothing was answered
    public bool Undo(int requestId)
    {
        var request = dbContext.ResourceRequests.Find(requestId);
        if (request is null || !request.ConfirmedByPhone || request.DeliveredAt != null
            || (request.Status != RequestStatus.Godtatt && request.Status != RequestStatus.Avslatt))
        {
            return false;
        }

        var wasAccepted = request.Status == RequestStatus.Godtatt;
        request.Status = RequestStatus.Sendt;
        request.AnsweredAt = null;
        request.Delivery = null;
        request.ConfirmedByPhone = false;

        if (wasAccepted)
        {
            ReopenLine(request);
        }

        dbContext.SaveChanges();
        return true;
    }

    // Cancel a request that is waiting or accepted (not delivered yet)
    public bool Cancel(int requestId)
    {
        var request = dbContext.ResourceRequests.Find(requestId);
        if (request is null || request.DeliveredAt != null
            || (request.Status != RequestStatus.Sendt && request.Status != RequestStatus.Godtatt))
        {
            return false;
        }

        var wasAccepted = request.Status == RequestStatus.Godtatt;
        request.Status = RequestStatus.Avbrutt;

        if (wasAccepted)
        {
            ReopenLine(request);
        }

        dbContext.SaveChanges();
        return true;
    }

    public bool MarkDelivered(int requestId)
    {
        var request = dbContext.ResourceRequests.Find(requestId);
        if (request is null || request.Status != RequestStatus.Godtatt || request.DeliveredAt != null)
        {
            return false;
        }

        request.DeliveredAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        return true;
    }

    public bool MarkReturned(int requestId)
    {
        var request = dbContext.ResourceRequests.Find(requestId);
        if (request is null || request.Status != RequestStatus.Godtatt || request.ReturnedAt != null)
        {
            return false;
        }

        // Returned without being marked delivered first: count it as delivered too
        var now = DateTime.UtcNow;
        request.DeliveredAt ??= now;
        request.ReturnedAt = now;

        var resource = dbContext.Resources.Find(request.ResourceId);
        if (resource != null)
        {
            resource.Status = "Ledig";
        }

        dbContext.SaveChanges();
        return true;
    }

    // An accepted request was undone or cancelled: the resource is free again, and requests that got
    // "Ikke lenger nødvendig" without being answered are waiting again (help with AI)
    private void ReopenLine(ResourceRequest request)
    {
        var resource = dbContext.Resources.Find(request.ResourceId);
        if (resource != null)
        {
            resource.Status = "Ledig";
        }

        var others = dbContext.ResourceRequests
            .Where(r => r.ResourceNeedItemId == request.ResourceNeedItemId
                        && r.Id != request.Id
                        && r.Status == RequestStatus.IkkeLengerNodvendig
                        && r.AnsweredAt == null)
            .ToList();
        foreach (var other in others)
        {
            other.Status = RequestStatus.Sendt;
        }
    }

    public bool Extend(int requestId, int minutes)
    {
        var request = dbContext.ResourceRequests.Find(requestId);
        if (request is null || request.Status != RequestStatus.Sendt)
        {
            return false;
        }

        // From the deadline, or from now if "Ikke svart" (then it is waiting again)
        var now = DateTime.UtcNow;
        request.AnswerBy = (request.AnswerBy > now ? request.AnswerBy : now).AddMinutes(minutes);
        dbContext.SaveChanges();
        return true;
    }

    public int? GetNeedId(int requestId)
    {
        return dbContext.ResourceRequests
            .Where(request => request.Id == requestId)
            .Select(request => (int?)request.ResourceNeedItem.ResourceNeedId)
            .FirstOrDefault();
    }

    private ResourceRequest? Load(int requestId)
    {
        return dbContext.ResourceRequests
            .Include(r => r.ResourceNeedItem)
            .FirstOrDefault(r => r.Id == requestId);
    }

    // "The first ones who say yes get the job": when the line is covered, the others still waiting get "Ikke lenger nødvendig" (help with AI)
    private AnswerResult Apply(ResourceRequest request, Resource resource, bool yes, DeliveryMethod? delivery, string? comment)
    {
        request.AnsweredAt = DateTime.UtcNow;
        request.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        if (!yes)
        {
            request.Status = RequestStatus.Avslatt;
            dbContext.SaveChanges();
            return AnswerResult.Declined;
        }

        var quantity = request.ResourceNeedItem.Quantity;
        var accepted = dbContext.ResourceRequests.Count(r =>
            r.ResourceNeedItemId == request.ResourceNeedItemId && r.Status == RequestStatus.Godtatt);

        if (accepted >= quantity)
        {
            request.Status = RequestStatus.IkkeLengerNodvendig;
            dbContext.SaveChanges();
            return AnswerResult.AlreadyCovered;
        }

        request.Status = RequestStatus.Godtatt;
        request.Delivery = delivery ?? DeliveryMethod.Leverer;
        resource.Status = "Opptatt";

        // This yes covered the line: the others still waiting are no longer needed
        if (accepted + 1 >= quantity)
        {
            var others = dbContext.ResourceRequests
                .Where(r => r.ResourceNeedItemId == request.ResourceNeedItemId
                            && r.Id != request.Id
                            && r.Status == RequestStatus.Sendt)
                .ToList();
            foreach (var other in others)
            {
                other.Status = RequestStatus.IkkeLengerNodvendig;
            }
        }

        dbContext.SaveChanges();
        return AnswerResult.Accepted;
    }
}