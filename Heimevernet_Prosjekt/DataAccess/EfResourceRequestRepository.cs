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

    // "The first ones who say yes get the job": when the line is covered, the others still waiting get "Ikke lenger nødvendig" (help with AI)
    public AnswerResult Answer(int requestId, string ownerId, bool yes, DeliveryMethod? delivery, string? comment)
    {
        var request = dbContext.ResourceRequests
            .Include(r => r.ResourceNeedItem)
            .FirstOrDefault(r => r.Id == requestId);
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

        var now = DateTime.UtcNow;
        if (request.Status != RequestStatus.Sendt || request.AnswerBy < now)
        {
            return AnswerResult.NotOpen;
        }

        request.AnsweredAt = now;
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