using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Services;

// Test resources for development, only added when the table is empty (help with AI) - Teacher: ResourceDbSeeder.cs - Docs: Data Seeding
// https://learn.microsoft.com/ef/core/modeling/data-seeding
public static class ResourceDbSeeder
{
    public static void SeedTestResources(HeimevernetDbContext db)
    {
        if (db.Resources.Any())
        {
            return;
        }

        db.Resources.AddRange(
            new Resource { Name = "Name 1", Description = "Description 1", Type = "Type 1" },
            new Resource { Name = "Name 2", Description = "Description 2", Type = "Type 2" },
            new Resource { Name = "Name 3", Description = "Description 3", Type = "Type 3" }
        );
        db.SaveChanges();
    }
}