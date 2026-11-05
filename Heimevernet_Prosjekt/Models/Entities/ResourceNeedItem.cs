using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities
{

    // One line in a need, e.g. "2 traktorer". Requests to owners will point to a line
    public class ResourceNeedItem
    {
        public int Id { get; set; }

        // Which need the line belongs to
        public int ResourceNeedId { get; set; }
        public ResourceNeed ResourceNeed { get; set; } = null!;

        // Same fixed list as resources (ResourceTypes)
        [Required, MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        [Range(1, 1000)]
        public int Quantity { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
