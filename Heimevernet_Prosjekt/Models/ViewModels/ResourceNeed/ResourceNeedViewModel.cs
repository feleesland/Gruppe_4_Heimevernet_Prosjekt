using System.ComponentModel.DataAnnotations;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed
{
    public class ResourceNeedViewModel
    {
        public int? Id { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
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

        // Akutt/Planlagt, radius, time and status (only shown here)
        public NeedUrgency Urgency { get; set; }
        public double RadiusKm { get; set; }
        public DateTime? NeededFrom { get; set; }
        public DateTime? NeededTo { get; set; }
        public DateTime? ClosedAt { get; set; }

        // The lines, e.g. "2 × Motoriserte kjøretøy"
        public List<ResourceNeedItem> Items { get; set; } = new();
    }
}
