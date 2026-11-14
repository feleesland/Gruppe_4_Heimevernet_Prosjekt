using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.Models.Entities;

// A type of resource that admin can add to (e.g. "Motoriserte kjøretøy"); the name must be unique - Docs: Indexes (EF Core)
// https://learn.microsoft.com/ef/core/modeling/indexes
[Index(nameof(Name), IsUnique = true)]
public class ResourceType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Navn")]
    public string Name { get; set; } = string.Empty;

    // Shown under the dropdown so the user picks the right type
    [MaxLength(300)]
    [Display(Name = "Eksempler")]
    public string? Examples { get; set; }


    // "Ny ressurs" step by step: the category (e.g. Kjøretøy), the icon and the order. Empty on the old types
    public int? CategoryId { get; set; }
    public ResourceCategory? Category { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    // The questions the owner answers in step 2, e.g. "Hengerfeste"
    public List<ResourceAttribute> Attributes { get; set; } = new();
}