namespace Heimevernet_Prosjekt.Models.Entities
{
    public class PermanentMapMarker
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string? Description { get; set; }
    }
}
