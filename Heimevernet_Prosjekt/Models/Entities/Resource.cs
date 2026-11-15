using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities
{
    public class Resource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "Ledig";
        // "Ikke tilgjengelig": why, and when it is free again (UTC). Null = "vet ikke når"
        [MaxLength(200)]
        public string? UnavailableReason { get; set; }

        public DateTime? UnavailableUntil { get; set; }

        // When the resource can be used (help with AI): always, at fixed times (e.g. 16:00-08:00 Mon-Fri) or in one period.
        // Norwegian time. Checked in Services/ResourceAvailabilityService.cs
        public AvailabilityKind AvailabilityKind { get; set; } = AvailabilityKind.Always;

        public TimeSpan? AvailableFromTime { get; set; }

        public TimeSpan? AvailableToTime { get; set; }

        // The weekdays as bits: Monday = 1, Tuesday = 2 ... Sunday = 64. 31 = Mon-Fri, 127 = every day
        public int AvailableDays { get; set; } = 127;

        public DateTime? AvailableFromDate { get; set; }

        public DateTime? AvailableToDate { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        // The municipality from Kartverket ("KRISTIANSAND"), the region in the filters and the print-out
        [MaxLength(100)]
        public string Municipality { get; set; } = string.Empty;
        public string OwnerTelephoneNumber { get; set; } = string.Empty;
        public string OwnerId {  get; set; } = string.Empty;
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

        // Free text from the owner, e.g. where the keys are
        [MaxLength(2000)]
        public string? Notes { get; set; }

        // Picture of the resource, saved in the database - Docs: Upload files in ASP.NET Core
        // https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads
        public byte[]? ImageData { get; set; }
        public string? ImageContentType { get; set; }
    }
}
