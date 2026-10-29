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
            Description = model.Description,
            Type = model.Type,
            OwnerId = model.OwnerId ?? string.Empty
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
        resource.Description = model.Description;
        resource.Type = model.Type;
        dbContext.SaveChanges();
        return true;
    }

    public bool Delete(int id)
    {
        var resource = dbContext.Resources.Find(id);
        if (resource is null)
        {
            return false;
        }

        dbContext.Resources.Remove(resource);
        dbContext.SaveChanges();
        return true;
    }

}
