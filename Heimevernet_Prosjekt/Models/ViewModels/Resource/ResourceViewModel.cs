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

        // The owner's contact info, read from the account (AspNetUsers) - not saved on the resource
        public string? OwnerName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhone { get; set; }
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

        // Free text from the owner, e.g. where the keys are
        [MaxLength(2000)]
        [Display(Name = "Merknader")]
        public string? Notes { get; set; }

        // The picture the user uploads in the form (only used when saving) - Docs: Upload files in ASP.NET Core
        // https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads
        [Display(Name = "Bilde")]
        public IFormFile? Image { get; set; }

        // True if the resource already has a picture (used to show it)
        public bool HasImage { get; set; }
        // Whether the logged-in user can edit or delete (used to show or hide the buttons) - Docs: View-based authorization
        // https://learn.microsoft.com/aspnet/core/security/authorization/views
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}