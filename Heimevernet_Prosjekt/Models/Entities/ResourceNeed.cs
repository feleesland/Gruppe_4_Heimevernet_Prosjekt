namespace Heimevernet_Prosjekt.Models.Entities
{
    public class ResourceNeed
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        // The municipality from Kartverket ("KRISTIANSAND"), used as the region in the filters. Empty on old needs (then City is used)
        public string Municipality { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public bool OwnerRemoved { get; set; }

        // Soft delete: the resource is hidden, but kept so we know it existed and was used - Docs: Global Query Filters
        // https://learn.microsoft.com/ef/core/querying/filters
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        //For å få det opp på kart
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // When the resource was registered
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Akutt or Planlagt - decides colour on the map and the answer deadline
        public NeedUrgency Urgency { get; set; } = NeedUrgency.Akutt;

        // The circle on the map; a new need on a spot inside it is offered "Legg til i ..."
        public double RadiusKm { get; set; } = 2;

        // When it is needed. Empty NeededFrom = now (Akutt)
        public DateTime? NeededFrom { get; set; }
        public DateTime? NeededTo { get; set; }

        // Empty = active. Set when the operator closes the need, it then shows in Historikk
        public DateTime? ClosedAt { get; set; }

        // The lines: "2 traktorer", "1 båt" - Docs: Relationships (one-to-many)
        // https://learn.microsoft.com/ef/core/modeling/relationships/one-to-many
        public List<ResourceNeedItem> Items { get; set; } = new();
    }
}
