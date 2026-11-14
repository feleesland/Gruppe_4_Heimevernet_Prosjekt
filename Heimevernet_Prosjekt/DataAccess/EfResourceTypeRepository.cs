using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

// Resource types with EF Core - same pattern as EfResourceRepository
public class EfResourceTypeRepository(HeimevernetDbContext dbContext) : IResourceTypeRepository
{
    public IReadOnlyCollection<ResourceType> GetAll()
    {
        // "Annet" last, the rest alphabetically
        return dbContext.ResourceTypes
            .AsNoTracking()
            .OrderBy(t => t.Name == "Annet")
            .ThenBy(t => t.Name)
            .ToList()
            .AsReadOnly();
    }

    public ResourceType? GetById(int id)
    {
        return dbContext.ResourceTypes.Find(id);
    }

    public bool Exists(string name)
    {
        return dbContext.ResourceTypes.Any(t => t.Name == name);
    }

    public ResourceType Create(string name, string? examples)
    {
        var type = new ResourceType { Name = name.Trim(), Examples = examples?.Trim() };
        dbContext.ResourceTypes.Add(type);
        dbContext.SaveChanges();
        return type;
    }

    // Renaming also updates every resource that uses the old name, so none of them lose their type (help with AI)
    public bool Update(int id, string name, string? examples)
    {
        var type = dbContext.ResourceTypes.Find(id);
        if (type is null)
        {
            return false;
        }

        var oldName = type.Name;
        type.Name = name.Trim();
        type.Examples = examples?.Trim();

        if (oldName != type.Name)
        {
            var resources = dbContext.Resources
                .IgnoreQueryFilters() // also soft-deleted resources, so the history keeps the right type
                .Where(r => r.Type == oldName)
                .ToList();
            foreach (var resource in resources)
            {
                resource.Type = type.Name;
            }
        }

        dbContext.SaveChanges();
        return true;
    }

    // How many resources use this type (also soft-deleted ones) - shown on the Ressurstyper page
    public int CountResourcesUsing(string name)
    {
        return dbContext.Resources.IgnoreQueryFilters().Count(r => r.Type == name);
    }

    // Resources keep the old name; they are marked "Gammel type" until the owner picks a new type
    public bool Delete(int id)
    {
        var type = dbContext.ResourceTypes.Find(id);
        if (type == null)
        {
            return false;
        }

        dbContext.ResourceTypes.Remove(type);
        dbContext.SaveChanges();
        return true;
    }

    // "Ny ressurs": the categories with their types and questions, in the order admin chose. Hidden questions are left out
    // Docs: Eager loading (Include/ThenInclude) - https://learn.microsoft.com/ef/core/querying/related-data/eager
    public IReadOnlyList<ResourceCategory> GetCategories()
    {
        var categories = dbContext.ResourceCategories
            .Include(category => category.Types)
            .ThenInclude(type => type.Attributes)
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ToList();

        foreach (var category in categories)
        {
            category.Types = category.Types.OrderBy(type => type.SortOrder).ToList();
            foreach (var type in category.Types)
            {
                type.Attributes = type.Attributes.Where(a => !a.IsHidden).OrderBy(a => a.SortOrder).ToList();
            }
        }
        return categories;
    }
}