using Heimevernet_Prosjekt.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed
{
    // The "Meld behov" form: the need (name, place, akutt/planlagt, time) and its lines (help with AI)
    // Docs: Model binding to collections - https://learn.microsoft.com/aspnet/core/mvc/models/model-binding#collections
    public class ReportNeedViewModel
    {
        // Set when the operator picks "Legg til i Flom Lund"; empty = a new need
        public int? ExistingNeedId { get; set; }

        // Required for a new need (checked in the controller, not when adding to an existing one)
        [Display(Name = "Navn")]
        [MaxLength(200, ErrorMessage = "Navnet kan ikke være lengre enn 200 tegn.")]
        public string? Name { get; set; }

        [Display(Name = "Beskrivelse")]
        [MaxLength(2000, ErrorMessage = "Beskrivelsen kan ikke være lengre enn 2000 tegn.")]
        public string? Description { get; set; }

        [Display(Name = "Akutt eller planlagt")]
        public NeedUrgency Urgency { get; set; } = NeedUrgency.Akutt;

        [Display(Name = "Radius (km)")]
        [Range(0.1, 50, ErrorMessage = "Radius må være mellom 0,1 og 50 km.")]
        public double RadiusKm { get; set; } = 2;

        // The place (same fields and map as on resources)
        [Display(Name = "Adresse")]
        [MaxLength(200)]
        public string? Address { get; set; }

        [Display(Name = "Postnummer")]
        [MaxLength(6)]
        public string? ZipCode { get; set; }

        [Display(Name = "Poststed")]
        [MaxLength(100)]
        public string? City { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Akutt: empty = "Nå", NeededTo = "Innen kl. ...". Planlagt: NeededFrom is required, NeededTo optional
        [Display(Name = "Fra")]
        public DateTime? NeededFrom { get; set; }

        [Display(Name = "Til")]
        public DateTime? NeededTo { get; set; }

        // The lines, e.g. "2 traktorer" and "1 båt"
        public List<NeedLineInput> Lines { get; set; } = new();
    }

    // One line in the form
    public class NeedLineInput
    {
        [Display(Name = "Ressurs")]
        [Required(ErrorMessage = "Velg en type.")]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        [Display(Name = "Antall")]
        [Range(1, 1000, ErrorMessage = "Antall må være mellom 1 og 1000.")]
        public int Quantity { get; set; } = 1;
    }
}

