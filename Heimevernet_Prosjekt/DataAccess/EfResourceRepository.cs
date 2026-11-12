using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Heimevernet_Prosjekt.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

// Create, read and update with EF Core - Teacher: EfResourceRepository.cs
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
            Notes = model.Notes,

            // Address and position on the map
            Address = model.Address ?? string.Empty,
            City = model.City ?? string.Empty,
            Municipality = model.Municipality ?? string.Empty,
            ZipCode = model.ZipCode ?? string.Empty,
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };

        if (model.Image != null)
        {
            resource.ImageData = ReadImage(model.Image);
            resource.ImageContentType = model.Image.ContentType;
        }

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
        
        resource.Notes = model.Notes;

        // Address and position on the map
        resource.Address = model.Address ?? string.Empty;
        resource.City = model.City ?? string.Empty;
        resource.Municipality = model.Municipality ?? string.Empty;
        resource.ZipCode = model.ZipCode ?? string.Empty;
        resource.Latitude = model.Latitude;
        resource.Longitude = model.Longitude;

        // Only replace the picture if a new one was uploaded, otherwise keep the old one
        if (model.Image != null)
        {
            resource.ImageData = ReadImage(model.Image);
            resource.ImageContentType = model.Image.ContentType;
        }

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

    // Copies the uploaded file into a byte array so it can be saved in the database - Docs: Upload files in ASP.NET Core
    // https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads
    private static byte[] ReadImage(IFormFile image)
    {
        using var memoryStream = new MemoryStream();
        image.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
    // One click on the resource page: Ledig / Opptatt / Ugyldig, without the whole edit form
    public bool SetStatus(int id, string status)
    {
        var resource = dbContext.Resources.Find(id);
        if (resource is null)
        {
            return false;
        }

        resource.Status = status;
        // A normal status clears "Ikke tilgjengelig"
        resource.UnavailableReason = null;
        resource.UnavailableUntil = null;
        dbContext.SaveChanges();
        return true;
    }
    // "Ikke tilgjengelig" with why and when it's free again (null = "vet ikke når")
    public bool SetUnavailable(int id, string? reason, DateTime? untilUtc)
    {
        var resource = dbContext.Resources.Find(id);
        if (resource is null)
        {
            return false;
        }

        resource.Status = "Ikke tilgjengelig";
        resource.UnavailableReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        resource.UnavailableUntil = untilUtc;
        dbContext.SaveChanges();
        return true;
    }

    // The resource's answers with their questions, in the question order
    public IReadOnlyList<ResourceAttributeValue> GetAnswers(int resourceId)
    {
        return dbContext.ResourceAttributeValues
            .Include(value => value.ResourceAttribute)
            .AsNoTracking()
            .Where(value => value.ResourceId == resourceId)
            .OrderBy(value => value.ResourceAttribute.SortOrder)
            .ToList();
    }

    // Saves the answers for the resource's type. Empty answers, and answers to another type's questions
    // (after the type was changed), are removed. "Nei" on a yes/no question is saved as no answer (help with AI)
    public void SaveAnswers(int resourceId, IDictionary<int, string?> answers)
    {
        var resource = dbContext.Resources.Find(resourceId);
        if (resource is null)
        {
            return;
        }

        // Only the questions of the resource's own type, so a changed form can't save anything else
        var allowed = dbContext.ResourceAttributes
            .Where(attribute => attribute.ResourceType.Name == resource.Type)
            .Select(attribute => attribute.Id)
            .ToHashSet();

        var existing = dbContext.ResourceAttributeValues.Where(value => value.ResourceId == resourceId).ToList();
        foreach (var value in existing)
        {
            if (!allowed.Contains(value.ResourceAttributeId) || !answers.TryGetValue(value.ResourceAttributeId, out var newAnswer) || string.IsNullOrWhiteSpace(newAnswer))
            {
                dbContext.ResourceAttributeValues.Remove(value);
            }
        }

        foreach (var (attributeId, answer) in answers)
        {
            if (!allowed.Contains(attributeId) || string.IsNullOrWhiteSpace(answer))
            {
                continue;
            }
            var text = answer.Trim();
            if (text.Length > 200)
            {
                text = text[..200];
            }

            var value = existing.FirstOrDefault(v => v.ResourceAttributeId == attributeId);
            if (value == null)
            {
                dbContext.ResourceAttributeValues.Add(new ResourceAttributeValue { ResourceId = resourceId, ResourceAttributeId = attributeId, Value = text });
            }
            else
            {
                value.Value = text;
            }
        }

        dbContext.SaveChanges();
    }

}