using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Services;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

public class EfResourceNeedRepository(HeimevernetDbContext dbContext) : IResourceNeedRepository
{

    public ResourceNeed Create(ResourceNeedViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var resourceneed = new ResourceNeed
        {
            Name = model.Name,
            Description = model.Description,
            Type = model.Type,
            OwnerId = model.OwnerId ?? string.Empty,

            // Address and position on the map
            Address = model.Address ?? string.Empty,
            City = model.City ?? string.Empty,
            ZipCode = model.ZipCode ?? string.Empty,
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };

        dbContext.ResourceNeeds.Add(resourceneed);
        dbContext.SaveChanges();
        return resourceneed;
    }

    public ResourceNeed? GetById(int id)
    {
        return dbContext.ResourceNeeds.Find(id);
    }

    // Include: the list shows the lines too - Docs: Eager loading
    public IReadOnlyCollection<ResourceNeed> GetAll()
    {
        return dbContext.ResourceNeeds
            .Include(need => need.Items)
            .AsNoTracking()
            .ToList()
            .AsReadOnly();
    }

    public bool Update(int id, ResourceNeedViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var resourceneed = dbContext.ResourceNeeds.Find(id);
        if (resourceneed is null)
        {
            return false;
        }

        resourceneed.Name = model.Name;
        resourceneed.Description = model.Description;
        resourceneed.Type = model.Type;

        // Address and position on the map
        resourceneed.Address = model.Address ?? string.Empty;
        resourceneed.City = model.City ?? string.Empty;
        resourceneed.ZipCode = model.ZipCode ?? string.Empty;
        resourceneed.Latitude = model.Latitude;
        resourceneed.Longitude = model.Longitude;
        dbContext.SaveChanges();
        return true;
    }

    // Soft delete: the resource need is marked as deleted, but kept in the database - Docs: Global Query Filters
    // https://learn.microsoft.com/ef/core/querying/filters
    public bool Delete(int id)
    {
        var resourceneed = dbContext.ResourceNeeds.Find(id);
        if (resourceneed is null)
        {
            return false;
        }

        resourceneed.IsDeleted = true;
        resourceneed.DeletedAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        return true;
    }

    // When a user is deleted: remove the link to the person, but keep the resource needs (help with AI)
    public int RemoveOwner(string ownerId)
    {
        var resourceneeds = dbContext.ResourceNeeds
            .IgnoreQueryFilters()
            .Where(r => r.OwnerId == ownerId)
            .ToList();

        foreach (var resourceneed in resourceneeds)
        {
            resourceneed.OwnerId = string.Empty;
            resourceneed.OwnerRemoved = true;
        }

        dbContext.SaveChanges();
        return resourceneeds.Count;
    }

    // Soft delete all resource neds from one user (e.g. a user who enters false information)
    public int DeleteAllByOwner(string ownerId)
    {
        var resourceneeds = dbContext.ResourceNeeds
            .Where(r => r.OwnerId == ownerId)
            .ToList();

        foreach (var resourceneed in resourceneeds)
        {
            resourceneed.IsDeleted = true;
            resourceneed.DeletedAt = DateTime.UtcNow;
        }

        dbContext.SaveChanges();
        return resourceneeds.Count;
    }

    // "Meld behov": the need and all its lines are saved in one SaveChanges, so either everything or nothing is saved
    // Docs: Saving related data - https://learn.microsoft.com/ef/core/saving/related-data
    public ResourceNeed CreateWithItems(ReportNeedViewModel model, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var need = new ResourceNeed
        {
            Name = model.Name?.Trim() ?? string.Empty,
            Description = model.Description?.Trim() ?? string.Empty,
            // The old Type column still exists; it gets the first line's type until the old pages are replaced
            Type = model.Lines.FirstOrDefault()?.Type ?? string.Empty,
            OwnerId = ownerId,

            Address = model.Address ?? string.Empty,
            City = model.City ?? string.Empty,
            ZipCode = model.ZipCode ?? string.Empty,
            Latitude = model.Latitude,
            Longitude = model.Longitude,

            Urgency = model.Urgency,
            RadiusKm = model.RadiusKm,
            NeededFrom = model.NeededFrom,
            NeededTo = model.NeededTo,

            Items = model.Lines
                .Select(line => new ResourceNeedItem { Type = line.Type, Quantity = line.Quantity })
                .ToList()
        };

        dbContext.ResourceNeeds.Add(need);
        dbContext.SaveChanges();
        return need;
    }

    public bool AddItems(int needId, IEnumerable<NeedLineInput> lines)
    {
        var need = dbContext.ResourceNeeds.Find(needId);
        if (need is null || need.ClosedAt != null)
        {
            return false;
        }

        foreach (var line in lines)
        {
            dbContext.ResourceNeedItems.Add(new ResourceNeedItem
            {
                ResourceNeedId = needId,
                Type = line.Type,
                Quantity = line.Quantity
            });
        }

        dbContext.SaveChanges();
        return true;
    }

    // Include loads the lines together with the need - Docs: Eager loading
    // https://learn.microsoft.com/ef/core/querying/related-data/eager
    public ResourceNeed? GetWithItems(int id)
    {
        return dbContext.ResourceNeeds
            .Include(need => need.Items)
            .AsNoTracking()
            .FirstOrDefault(need => need.Id == id);
    }

    // The database finds the active needs with a position; the distance is checked in C# with GeoDistance
    public IReadOnlyCollection<ResourceNeed> GetActiveAt(double latitude, double longitude)
    {
        return dbContext.ResourceNeeds
            .Include(need => need.Items)
            .AsNoTracking()
            .Where(need => need.ClosedAt == null && need.Latitude != null && need.Longitude != null)
            .ToList()
            .Select(need => new
            {
                Need = need,
                Distance = GeoDistance.Km(latitude, longitude, need.Latitude!.Value, need.Longitude!.Value)
            })
            .Where(x => x.Distance <= x.Need.RadiusKm)
            .OrderBy(x => x.Distance)
            .Select(x => x.Need)
            .ToList()
            .AsReadOnly();
    }
    // Close the need: it disappears from the map and stops taking new requests.
    // Requests still waiting for an answer are withdrawn so owners don't answer for nothing.
    // Resources that were lent out keep their status until they are marked as returned.
    public bool Close(int id)
    {
        var need = dbContext.ResourceNeeds.Find(id);
        if (need is null || need.ClosedAt != null)
        {
            return false;
        }

        need.ClosedAt = DateTime.UtcNow;

        var waiting = dbContext.ResourceRequests
            .Where(r => r.ResourceNeedItem.ResourceNeedId == id && r.Status == RequestStatus.Sendt)
            .ToList();
        foreach (var request in waiting)
        {
            request.Status = RequestStatus.Avbrutt;
        }

        dbContext.SaveChanges();
        return true;
    }

}
