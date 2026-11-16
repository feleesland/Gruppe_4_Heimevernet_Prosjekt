using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities;

    public class PermanentMapMarker
    {
        public int Id { get; set; }

    [Required]
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

    public string IconType { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
