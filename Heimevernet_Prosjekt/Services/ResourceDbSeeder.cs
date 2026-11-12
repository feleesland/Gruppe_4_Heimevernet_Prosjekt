using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.Services;

// Test resources, and the categories, types and questions from the case - Teacher: ResourceDbSeeder.cs - Docs: Data Seeding
// https://learn.microsoft.com/ef/core/modeling/data-seeding
public static class ResourceDbSeeder
{
    // Test resources for development, only added when the table is empty (a few types, placed in Kristiansand)
    public static void SeedTestResources(HeimevernetDbContext db)
    {
        if (db.Resources.Any())
        {
            return;
        }

        db.Resources.AddRange(
            new Resource { Name = "Toyota RAV4", Description = "Testressurs", Type = "Bil", City = "Kristiansand", Latitude = 58.1467, Longitude = 7.9956 },
            new Resource { Name = "Aggregat 5 kW", Description = "Testressurs", Type = "Aggregat", City = "Kristiansand", Latitude = 58.1520, Longitude = 8.0050 },
            new Resource { Name = "Grendehuset", Description = "Testressurs", Type = "Lokaler", City = "Kristiansand", Latitude = 58.1400, Longitude = 7.9800 }
        );
        db.SaveChanges();
    }
    // One test resource for every type, with example answers to its questions, spread around Kristiansand (help with AI)
    // Runs only once: if the test resources exist (also deleted ones), nothing is added
    private const string PerTypeMarker = "Testressurs – én per type";

    public static void SeedOneTestPerType(HeimevernetDbContext db)
    {
        if (db.Resources.IgnoreQueryFilters().Any(r => r.Description == PerTypeMarker))
        {
            return;
        }

        var types = db.ResourceTypes.Include(t => t.Attributes)
            .Where(t => t.CategoryId != null)
            .OrderBy(t => t.Name)
            .ToList();

        for (var i = 0; i < types.Count; i++)
        {
            // A circle around Kristiansand sentrum, about 2-4 km out, so they don't stand on top of each other
            var angle = 2 * Math.PI * i / types.Count;
            var distance = 0.02 + 0.015 * (i % 2);
            var resource = new Resource
            {
                Name = "Test: " + types[i].Name,
                Description = PerTypeMarker,
                Type = types[i].Name,
                Status = "Ledig",
                City = "Kristiansand",
                Latitude = 58.1467 + distance * Math.Sin(angle),
                Longitude = 7.9956 + distance * 2 * Math.Cos(angle)
            };
            db.Resources.Add(resource);

            // Example answers: Ja, 5, the first choice, or a short text
            foreach (var attribute in types[i].Attributes)
            {
                var value = attribute.Kind switch
                {
                    AttributeKind.YesNo => "true",
                    AttributeKind.Number => "5",
                    AttributeKind.Choice => attribute.Options?.Split(';')[0] ?? "",
                    _ => "Testsvar"
                };
                db.ResourceAttributeValues.Add(new ResourceAttributeValue { Resource = resource, ResourceAttributeId = attribute.Id, Value = value });
            }
        }

        db.SaveChanges();
    }
    // A question in short form: label, kind, options or unit
    private record Q(string Label, AttributeKind Kind, string? Options = null, string? Unit = null);

    private static Q YesNo(string label) => new(label, AttributeKind.YesNo);
    private static Q Number(string label, string? unit = null) => new(label, AttributeKind.Number, Unit: unit);
    private static Q Choice(string label, string options) => new(label, AttributeKind.Choice, Options: options);
    private static Q Text(string label) => new(label, AttributeKind.Text);

    // The old types get the new names, so resources and needs keep a type (agreed in the group)
    private static readonly (string Old, string New)[] Renames =
    {
        ("Motoriserte kjøretøy", "Bil"),
        ("Husly og bygg", "Lokaler"),
        ("Sikkerhet og medisinsk", "Førstehjelp"),
        ("Verktøy og utstyr", "Verktøy"),
        ("Materialer og forsyninger", "Sand og grus"),
    };

    // The categories, types and questions from the case. Icons from Tabler Icons - https://tabler.io/icons
    private static readonly (string Category, string Icon, (string Type, string Icon, Q[] Questions)[] Types)[] Data =
    {
        ("Kjøretøy", "car", new[]
        {
            ("Bil", "car", new[] { YesNo("Hengerfeste"), Number("Antall seter"), YesNo("Firehjulstrekk"), Choice("Førerkort som trengs", "B;BE;C") }),
            ("Traktor", "tractor", new[] { YesNo("Snøskjær eller snøfreser"), YesNo("Frontlaster"), YesNo("Tilhenger") }),
            ("ATV", "motorbike", new[] { YesNo("Tilhenger"), YesNo("Beltesett") }),
            ("Lastebil", "truck", new[] { Number("Lastevekt", "tonn"), YesNo("Tipp"), YesNo("Kran"), Choice("Førerkort som trengs", "C;CE") }),
            ("Snøskuter", "snowflake", new[] { Number("Antall seter"), YesNo("Slede") }),
            ("Båt", "sailboat", new[] { Number("Antall plasser"), Number("Lengde", "fot"), Number("Motor", "hk") }),
        }),
        ("Maskiner", "bulldozer", new[]
        {
            ("Gravemaskin", "bulldozer", new[] { Number("Vekt", "tonn"), YesNo("Har tilhenger til å frakte den") }),
            ("Hjullaster", "forklift", new[] { Number("Skuffe", "m³"), YesNo("Snøskjær") }),
            ("Skogsmaskin", "trees", new[] { Choice("Type", "Hogstmaskin;Lassbærer") }),
        }),
        ("Utstyr", "tool", new[]
        {
            ("Drone", "drone", new[] { YesNo("Med operatør"), Choice("Sertifikat", "A1/A3;A2;Spesifikk"), YesNo("Varmekamera"), Number("Flytid", "min") }),
            ("Aggregat", "bolt", new[] { Number("Effekt", "kW"), Choice("Drivstoff", "Diesel;Bensin"), Choice("Spenning", "230 V;400 V") }),
            ("Samband", "antenna", new[] { Choice("Type", "Radio;Satellittelefon;Annet"), Number("Antall enheter"), Number("Rekkevidde", "km") }),
            ("Verktøy", "tool", new[] { Text("Hva slags verktøy") }),
            ("Førstehjelp", "first-aid-kit", new[] { YesNo("Hjertestarter"), YesNo("Båre") }),
        }),
        ("Materialer", "box", new[]
        {
            ("Sand og grus", "mountain", new[] { Number("Mengde", "m³"), YesNo("Kan levere") }),
            ("Drivstoff", "gas-station", new[] { Choice("Type", "Diesel;Bensin"), Number("Mengde", "liter") }),
        }),
        ("Folk og steder", "users", new[]
        {
            ("Mannskap og frivillige", "users", new[] { YesNo("Førstehjelp"), YesNo("Søk i terreng"), Choice("Førerkort", "Ingen;B;BE;C"), YesNo("Dronesertifikat"), YesNo("Kjent i området") }),
            ("Lokaler", "building", new[] { Number("Antall personer"), YesNo("Overnatting"), YesNo("Strøm eller aggregat"), YesNo("Kjøkken"), YesNo("Universell utforming") }),
        }),
        ("Annet", "dots", new[]
        {
            ("Annet", "plus", new[] { Text("Hva er det?") }),
        }),
    };

    // Runs only once (when there are no categories yet), so admin's own changes are never overwritten (help with AI)
    public static void SeedResourceTypes(HeimevernetDbContext db)
    {
        if (db.ResourceCategories.Any())
        {
            return;
        }

        RenameOldTypes(db);

        var categoryOrder = 0;
        foreach (var (categoryName, categoryIcon, types) in Data)
        {
            var category = new ResourceCategory { Name = categoryName, Icon = categoryIcon, SortOrder = categoryOrder++ };
            db.ResourceCategories.Add(category);

            var typeOrder = 0;
            foreach (var (typeName, typeIcon, questions) in types)
            {
                // A type with this name may exist already (renamed above, or "Annet"): give it a category instead of a copy
                var type = db.ResourceTypes.Include(t => t.Attributes).FirstOrDefault(t => t.Name == typeName);
                if (type == null)
                {
                    type = new ResourceType { Name = typeName };
                    db.ResourceTypes.Add(type);
                }

                type.Category = category;
                type.Icon = typeIcon;
                type.SortOrder = typeOrder++;
                type.Examples = null; // the old examples ("Bil, traktor, ...") don't fit the new types

                if (type.Attributes.Count == 0)
                {
                    var questionOrder = 0;
                    foreach (var q in questions)
                    {
                        type.Attributes.Add(new ResourceAttribute
                        {
                            Label = q.Label,
                            Kind = q.Kind,
                            Options = q.Options,
                            Unit = q.Unit,
                            SortOrder = questionOrder++
                        });
                    }
                }
            }
        }

        db.SaveChanges();
    }

    // The type is saved as text on resources, needs and need lines, so all three get the new name.
    // IgnoreQueryFilters: also soft-deleted ones, so the history keeps the right type
    private static void RenameOldTypes(HeimevernetDbContext db)
    {
        foreach (var (oldName, newName) in Renames)
        {
            var type = db.ResourceTypes.FirstOrDefault(t => t.Name == oldName);
            if (type == null || db.ResourceTypes.Any(t => t.Name == newName))
            {
                continue;
            }

            type.Name = newName;
            foreach (var resource in db.Resources.IgnoreQueryFilters().Where(r => r.Type == oldName).ToList())
            {
                resource.Type = newName;
            }
            foreach (var need in db.ResourceNeeds.IgnoreQueryFilters().Where(n => n.Type == oldName).ToList())
            {
                need.Type = newName;
            }
            foreach (var item in db.ResourceNeedItems.IgnoreQueryFilters().Where(i => i.Type == oldName).ToList())
            {
                item.Type = newName;
            }
        }

        db.SaveChanges(); // so the new names are found below
    }
}