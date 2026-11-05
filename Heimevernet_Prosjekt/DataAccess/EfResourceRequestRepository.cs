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

    public int Create(int itemId, IEnumerable<int> resourceIds, string requestedById, DateTime answerByUtc)
    {
        var item = dbContext.ResourceNeedItems.Find(itemId);
        if (item is null)
        {
            return 0;
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

        var created = 0;
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
            created++;
        }

        dbContext.SaveChanges();
        return created;
    }
}