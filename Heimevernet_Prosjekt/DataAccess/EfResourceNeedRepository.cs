using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.DataAccess;
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

    public IReadOnlyCollection<ResourceNeed> GetAll()
    {
        return dbContext.ResourceNeeds
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

}
