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
            new Resource { Name = "Aggregat 5 kW", Description = "Testressurs", Type = "Aggregat", City = "Kristiansand", Latitude = 58.1520, Longitude = 8.0050 }
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
        ("Samband", "Sambandsutstyr"),
        ("Materialer og forsyninger", "Sand og grus"),
    };

    // The categories, types and questions from the case, in 9 categories (agreed in the group). Icons from Tabler Icons - https://tabler.io/icons
    // More types (Varebil, Sandsekker, ...) are added later by admin on the Ressurstyper page, not here
    private static readonly (string Category, string Icon, (string Type, string Icon, Q[] Questions)[] Types)[] Data =
    {
        ("Kjøretøy", "car", new[]
        {
            ("Bil", "car", new[] { YesNo("Hengerfeste"), Number("Antall seter"), YesNo("Firehjulstrekk") }),
            ("Lastebil", "truck", new[] { Number("Lastevekt", "tonn"), YesNo("Tipp"), YesNo("Kran"), Choice("Førerkort som trengs", "C;CE") }),
        }),
        ("Snø og terreng", "snowflake", new[]
        {
            ("Snøskuter", "snowflake", new[] { Number("Antall seter"), YesNo("Slede") }),
            ("ATV", "motorbike", new[] { YesNo("Tilhenger"), YesNo("Beltesett") }),
            ("Beltevogn", "tank", new[] { Number("Antall plasser"), YesNo("Tilhenger") }),
            ("Snøfreser", "shovel", new[] { Choice("Type", "Håndført;Traktormontert"), Number("Arbeidsbredde", "cm") }),
        }),
        ("Skog og landbruk", "trees", new[]
        {
            ("Traktor", "tractor", new[] { YesNo("Snøskjær eller snøfreser"), YesNo("Frontlaster"), YesNo("Tilhenger") }),
            ("Skogsmaskin", "trees", new[] { Choice("Type", "Hogstmaskin;Lassbærer") }),
        }),
        ("Anleggsmaskiner", "bulldozer", new[]
        {
            ("Gravemaskin", "bulldozer", new[] { Number("Vekt", "tonn"), YesNo("Har tilhenger til å frakte den") }),
            ("Hjullaster", "forklift", new[] { Number("Skuffe", "m³"), YesNo("Snøskjær") }),
        }),
        ("Sjø", "sailboat", new[]
        {
            ("Båt", "sailboat", new[] { Number("Antall plasser"), Number("Lengde", "fot"), Number("Motor", "hk") }),
        }),
        ("Teknisk utstyr", "bolt", new[]
        {
            ("Aggregat", "bolt", new[] { Number("Effekt", "kW"), Choice("Drivstoff", "Diesel;Bensin"), Choice("Spenning", "230 V;400 V") }),
            ("Sambandsutstyr", "antenna", new[] { Choice("Type", "Radio;Satellittelefon;Annet"), Number("Antall enheter"), Number("Rekkevidde", "km") }),
            ("Drone", "drone", new[] { YesNo("Med operatør"), Choice("Sertifikat", "A1/A3;A2;Spesifikk"), YesNo("Varmekamera"), Number("Flytid", "min") }),
        }),
        ("Verktøy og førstehjelp", "tool", new[]
        {
            ("Verktøy", "tool", new[] { Text("Hva slags verktøy") }),
            ("Førstehjelp", "first-aid-kit", new[] { YesNo("Hjertestarter"), YesNo("Båre") }),
        }),
        ("Materialer og forsyninger", "box", new[]
        {
            ("Sand og grus", "mountain", new[] { Number("Mengde", "m³"), YesNo("Kan levere") }),
            ("Drivstoff", "gas-station", new[] { Choice("Type", "Diesel;Bensin"), Number("Mengde", "liter") }),
        }),
        ("Annet", "dots", new[]
        {
            ("Annet", "plus", new[] { Text("Hva er det?") }),
        }),
    };

    // Runs only once (when there are no categories yet), so admin's own changes are never overwritten (help with AI)
    public static void SeedResourceTypes(HeimevernetDbContext db)
    {
        // Takes away "Folk og steder" from databases that already have it
        RemovePeopleAndPlaces(db);
        // Moves the types into the 9 categories on databases that have the old ones
        RegroupCategories(db);
        // "Kjøretøy på vei" is called just "Kjøretøy" on databases that already have it
        RenameRoadVehicles(db);
        // Example names for each type, and "Førerkort som trengs" away from Bil, on databases that already have the types
        UpdateTypeDetails(db);

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
                type.Examples = NameExamples.GetValueOrDefault(typeName); // the hint in "Navn" in Ny ressurs
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
    // "Folk og steder" is taken away: the group was told that people and shelters have strict rules we shouldn't handle.
    // Runs at every start, but does nothing once the category is gone. The resources are soft-deleted (kept, but hidden);
    // the answers, questions, types and the category are deleted (help with AI)
    private static readonly string[] RemovedTypes = { "Mannskap og frivillige", "Lokaler" };

    private static void RemovePeopleAndPlaces(HeimevernetDbContext db)
    {
        if (!db.ResourceCategories.Any(c => c.Name == "Folk og steder"))
        {
            return;
        }

        foreach (var resource in db.Resources.Where(r => RemovedTypes.Contains(r.Type)).ToList())
        {
            resource.IsDeleted = true;
            resource.DeletedAt = DateTime.UtcNow;
        }

        // The answers must go first: a question that has answers can't be deleted (Restrict in HeimevernetDbContext)
        db.ResourceAttributeValues.RemoveRange(db.ResourceAttributeValues.IgnoreQueryFilters()
            .Where(v => RemovedTypes.Contains(v.ResourceAttribute.ResourceType.Name)));

        // The questions are deleted together with their type (Cascade)
        db.ResourceTypes.RemoveRange(db.ResourceTypes.Where(t => RemovedTypes.Contains(t.Name)));
        db.ResourceCategories.RemoveRange(db.ResourceCategories.Where(c => c.Name == "Folk og steder"));
        db.SaveChanges();
    }

    // Moves the types into the 9 categories in Data, renames Samband to Sambandsutstyr and adds the new types
    // (Beltevogn, Snøfreser) with their questions. Agreed in the group.
    // Runs once: when "Snø og terreng" exists nothing happens, so admin's own changes are kept later (help with AI)
    private static void RegroupCategories(HeimevernetDbContext db)
    {
        if (!db.ResourceCategories.Any() || db.ResourceCategories.Any(c => c.Name == "Snø og terreng"))
        {
            return;
        }

        RenameOldTypes(db);

        var categoryOrder = 0;
        foreach (var (categoryName, categoryIcon, types) in Data)
        {
            // A category with the same name is reused (e.g. "Annet"), the others are made new
            var category = db.ResourceCategories.FirstOrDefault(c => c.Name == categoryName);
            if (category == null)
            {
                category = new ResourceCategory { Name = categoryName };
                db.ResourceCategories.Add(category);
            }
            category.Icon = categoryIcon;
            category.SortOrder = categoryOrder++;

            var typeOrder = 0;
            foreach (var (typeName, typeIcon, questions) in types)
            {
                // A new type gets its questions. The types that exist keep theirs, and the answers on resources
                var type = db.ResourceTypes.Include(t => t.Attributes).FirstOrDefault(t => t.Name == typeName);
                if (type == null)
                {
                    type = new ResourceType { Name = typeName, Icon = typeIcon };
                    var questionOrder = 0;
                    foreach (var q in questions)
                    {
                        type.Attributes.Add(new ResourceAttribute { Label = q.Label, Kind = q.Kind, Options = q.Options, Unit = q.Unit, SortOrder = questionOrder++ });
                    }
                    db.ResourceTypes.Add(type);
                }
                type.Category = category;
                type.SortOrder = typeOrder++;
            }
        }

        // The old categories are empty now and are deleted
        var names = Data.Select(d => d.Category).ToList();
        db.ResourceCategories.RemoveRange(db.ResourceCategories.Where(c => !names.Contains(c.Name)));
        db.SaveChanges();
    }

    // "Kjøretøy på vei" is called just "Kjøretøy" (agreed in the group). Does nothing once it is renamed (help with AI)
    private static void RenameRoadVehicles(HeimevernetDbContext db)
    {
        var category = db.ResourceCategories.FirstOrDefault(c => c.Name == "Kjøretøy på vei");
        if (category == null)
        {
            return;
        }

        category.Name = "Kjøretøy";
        db.SaveChanges();
    }

    // An example name for each type, shown as the hint in "Navn" in step 3 of Ny ressurs (saved in ResourceType.Examples)
    private static readonly Dictionary<string, string> NameExamples = new()
    {
        ["Bil"] = "Toyota RAV4",
        ["Lastebil"] = "Volvo FH med kran",
        ["Snøskuter"] = "Lynx 59 Ranger",
        ["ATV"] = "Polaris Sportsman 570",
        ["Beltevogn"] = "Hägglunds BV206",
        ["Snøfreser"] = "Honda HSS 760",
        ["Traktor"] = "John Deere 6120M",
        ["Skogsmaskin"] = "Ponsse Buffalo",
        ["Gravemaskin"] = "CAT 308",
        ["Hjullaster"] = "Volvo L60H",
        ["Båt"] = "Askeladden C61",
        ["Aggregat"] = "Honda EU22i 2,2 kW",
        ["Sambandsutstyr"] = "4 stk VHF-radio",
        ["Drone"] = "DJI Mavic 3 Thermal",
        ["Verktøy"] = "Motorsag og stige",
        ["Førstehjelp"] = "Hjertestarter og båre",
        ["Sand og grus"] = "10 m³ pukk",
        ["Drivstoff"] = "1000 liter diesel",
        ["Annet"] = "Hestehenger",
    };

    // Gives each type an example name (only where it has none, so admin's own text is kept), and takes
    // "Førerkort som trengs" away from Bil (agreed in the group: almost all cars need only B) (help with AI)
    private static void UpdateTypeDetails(HeimevernetDbContext db)
    {
        foreach (var type in db.ResourceTypes.Where(t => t.Examples == null || t.Examples == "").ToList())
        {
            type.Examples = NameExamples.GetValueOrDefault(type.Name);
        }

        var licence = db.ResourceAttributes.FirstOrDefault(a => a.Label == "Førerkort som trengs" && a.ResourceType.Name == "Bil");
        if (licence != null)
        {
            // The answers must go first (Restrict), then the question
            db.ResourceAttributeValues.RemoveRange(db.ResourceAttributeValues.IgnoreQueryFilters()
                .Where(v => v.ResourceAttributeId == licence.Id));
            db.ResourceAttributes.Remove(licence);
        }

        db.SaveChanges();
    }
}
