using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Resource
{
    public class ResourceViewModel
    {
        // New resources do not have an ID yet.
        public int? Id { get; set; }

        // A resource must have a name.
        [Required(ErrorMessage = "Skriv inn et navn på ressursen.")]
        [MaxLength(200, ErrorMessage = "Navnet kan ikke være lengre enn 200 tegn.")]
        [Display(Name = "Navn")]
        public string Name { get; set; } = string.Empty;

        // A description is optional.
        [MaxLength(2000, ErrorMessage = "Beskrivelsen kan ikke være lengre enn 2000 tegn.")]
        [Display(Name = "Beskrivelse")]
        public string? Description { get; set; }

        // A resource must have a type, chosen from the list (checked again in ResourceHandlerController.ValidateType)
        [Required(ErrorMessage = "Velg en ressurstype.")]
        [MaxLength(100, ErrorMessage = "Typen kan ikke være lengre enn 100 tegn.")] // kan fjernes vis vi vil
        [Display(Name = "Type")]
        public string Type { get; set; } = string.Empty;
        public string? Status { get; set; } = "Ledig";

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
        [Display(Name = "Adresse")]
        public string? Address { get; set; }
        [Display(Name = "Poststed")]
        public string? City { get; set; }
        [Display(Name = "Postnummer")]
        public string? ZipCode { get; set; }

        // When the resource was registered (only shown, never typed in)
        public DateTime? CreatedAt { get; set; }

        [MaxLength(2000, ErrorMessage = "Merknadene kan ikke være lengre enn 2000 tegn.")]
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