using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Services;

// Test resources for development, only added when the table is empty - Teacher: ResourceDbSeeder.cs - Docs: Data Seeding
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

    // The standard resource types, only added when the table is empty, so admin's changes are never overwritten
    public static void SeedResourceTypes(HeimevernetDbContext db)
    {
        if (db.ResourceTypes.Any())
        {
            return;
        }

        db.ResourceTypes.AddRange(
            new ResourceType { Name = "Verktøy og utstyr", Examples = "Håndverktøy, elektroverktøy, aggregat, småutstyr" },
            new ResourceType { Name = "Motoriserte kjøretøy", Examples = "Bil, traktor, lastebil, ATV, sitteklipper" },
            new ResourceType { Name = "Husly og bygg", Examples = "Telt, skur, hytte, drivhus, lager" },
            new ResourceType { Name = "Materialer og forsyninger", Examples = "Trelast, drivstoff, jord, byggevarer" },
            new ResourceType { Name = "Sikkerhet og medisinsk", Examples = "Førstehjelpsutstyr, brannslukker, verneutstyr, nødutstyr" },
            new ResourceType { Name = "Annet", Examples = "Det som ikke passer i de andre typene" }
        );
        db.SaveChanges();
    }
}