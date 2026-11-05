namespace Heimevernet_Prosjekt.Models
{
    // Stores coordinates used by the map.
    public class PositionModel
    {
        // Start with empty text until a coordinate is provided.
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;
    }
}