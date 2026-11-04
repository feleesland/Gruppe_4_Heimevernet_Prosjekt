using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Resource
{
    public class ResourceViewModel
    {
        // New resources do not have an ID yet.
        public int? Id { get; set; }

        // A resource must have a name.
        [Required(ErrorMessage = "Please enter a resource name.")]
        [MaxLength(200, ErrorMessage = "The name cannot exceed 200 characters.")]
        public string Name { get; set; } = string.Empty;

        // A description is optional.
        [MaxLength(2000, ErrorMessage = "The description cannot exceed 2000 characters.")]
        public string? Description { get; set; }

        // A resource must have a type.
        [Required(ErrorMessage = "Please enter a resource type.")]
        [MaxLength(100, ErrorMessage = "The type cannot exceed 100 characters.")]
        public string Type { get; set; } = string.Empty;
        public string? OwnerId { get; set; }
        public bool OwnerRemoved { get; set; }

        // Position on the map
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Address (used to find the position on the map)
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? ZipCode { get; set; }
      
        // When the resource was registered (only shown, never typed in)
        public DateTime? CreatedAt { get; set; }
    }
}