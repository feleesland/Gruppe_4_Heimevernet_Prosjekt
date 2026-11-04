using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Heimevernet_Prosjekt.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

public class EfResourceRepository(HeimevernetDbContext dbContext) : IResourceRepository
{

    public Resource Create(ResourceViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var resource = new Resource
        {
            Name = model.Name,
            // Store an empty string when no description is entered.
            Description = model.Description ?? string.Empty,
            Type = model.Type,
            Status = model.Status ?? "Ledig",
            OwnerId = model.OwnerId ?? string.Empty,

            // Address and position on the map
            Address = model.Address ?? string.Empty,
            City = model.City ?? string.Empty,
            ZipCode = model.ZipCode ?? string.Empty,
            Latitude = model.Latitude,
            Longitude = model.Longitude
        }; 

        dbContext.Resources.Add(resource);
        dbContext.SaveChanges();
        return resource;
    }

    public Resource? GetById(int id)
    {
        return dbContext.Resources.Find(id);
    }

    public IReadOnlyCollection<Resource> GetAll()
    {
        return dbContext.Resources
            .AsNoTracking()
            .ToList()
            .AsReadOnly();
    }

    public bool Update(int id, ResourceViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var resource = dbContext.Resources.Find(id);
        if (resource is null)
        {
            return false;
        }

        resource.Name = model.Name;
        // Store an empty string when no description is entered.
        resource.Description = model.Description ?? string.Empty;
        resource.Type = model.Type;
        resource.Status = model.Status ?? resource.Status;
        
        // Address and position on the map
        resource.Address = model.Address ?? string.Empty;
        resource.City = model.City ?? string.Empty;
        resource.ZipCode = model.ZipCode ?? string.Empty;
        resource.Latitude = model.Latitude;
        resource.Longitude = model.Longitude;
        dbContext.SaveChanges();
        return true;
    }

    // Soft delete: the resource is marked as deleted, but kept in the database - Docs: Global Query Filters
    // https://learn.microsoft.com/ef/core/querying/filters
    public bool Delete(int id)
    {
        var resource = dbContext.Resources.Find(id);
        if (resource is null)
        {
            return false;
        }

        resource.IsDeleted = true;
        resource.DeletedAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        return true;
    }

    // When a user is deleted: remove the link to the person, but keep the resources (help with AI)
    public int RemoveOwner(string ownerId)
    {
        var resources = dbContext.Resources
            .IgnoreQueryFilters()
            .Where(r => r.OwnerId == ownerId)
            .ToList();

        foreach (var resource in resources)
        {
            resource.OwnerId = string.Empty;
            resource.OwnerTelephoneNumber = string.Empty;
            resource.OwnerRemoved = true;
        }

        dbContext.SaveChanges();
        return resources.Count;
    }

    // Soft delete all resources from one user (e.g. a user who enters false information)
    public int DeleteAllByOwner(string ownerId)
    {
        var resources = dbContext.Resources
            .Where(r => r.OwnerId == ownerId)
            .ToList();

        foreach (var resource in resources)
        {
            resource.IsDeleted = true;
            resource.DeletedAt = DateTime.UtcNow;
        }

        dbContext.SaveChanges();
        return resources.Count;
    }

}
