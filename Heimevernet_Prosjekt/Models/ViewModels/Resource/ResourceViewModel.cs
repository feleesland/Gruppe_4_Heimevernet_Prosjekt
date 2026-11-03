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
    }
}