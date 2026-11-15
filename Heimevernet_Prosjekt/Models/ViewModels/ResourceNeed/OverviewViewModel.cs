using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;

// "Oversikt": the operator's main page for one area (kommune) or all of Norway.
// Active needs first (the ones that need something on top), then "Utlånt nå" and "Må følges opp" (help with AI)
public class OverviewViewModel
{
    public const string RegionAll = "alle";

    // The chosen area: a kommune, "alle" (Hele Norge), or null = not chosen yet (the page asks)
    public string? Region { get; set; }
    public bool AllNorway => Region == RegionAll;
    public string RegionName => Region == null || AllNorway ? "Hele Norge" : Region;

    // The kommuner with active needs or resources, the most active first (for the area picker)
    public List<OverviewRegion> Regions { get; set; } = new();
    public int RegionResources { get; set; }

    // The number strip
    public int ActiveAkutt { get; set; }
    public int ActivePlanned { get; set; }
    public int Waiting { get; set; }

    // Active needs, sorted: red, then orange, then the rest
    public List<OverviewNeedRow> Needs { get; set; } = new();

    // "Utlånt nå": delivered and in use. "Må følges opp": said yes but not delivered, or the need is closed (must go back)
    public List<OverviewLoanRow> InUse { get; set; } = new();
    public List<OverviewLoanRow> NotDelivered { get; set; } = new();
    public List<OverviewLoanRow> GiveBack { get; set; } = new();
}

// One kommune in the area picker
public record OverviewRegion(string Name, int Needs, int Resources);

// One active need in the list
public class OverviewNeedRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Place { get; set; } = string.Empty;
    public NeedUrgency Urgency { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NeededFrom { get; set; }
    public double RadiusKm { get; set; }
    public bool HasPlace { get; set; }

    public int Covered { get; set; }
    public int Quantity { get; set; }
    public int Delivered { get; set; }
    public int Waiting { get; set; }
    public DateTime? NextAnswerBy { get; set; }
    public int Overdue { get; set; }
    // How many owners have not answered in time (one resource asked on two lines is one owner to call)
    public int OverdueOwners { get; set; }
    public DateTime? OverdueSince { get; set; }

    // Each line of the need, for «Mangler»
    public List<OverviewLineRow> Lines { get; set; } = new();

    public bool NobodyAsked { get; set; }
    public bool NoneNearby { get; set; }
    // Free resources of the missing types inside the radius. Null = nothing to look for (or no place)
    public int? FreeNearby { get; set; }
    public bool StartsSoon { get; set; }

    // 2 = red (must do something now), 1 = orange (starts soon), 0 = fine
    public int Level { get; set; }
    public int Missing => Math.Max(0, Quantity - Covered);
}

// One line of the need: what it asks for, how much is covered or asked, and how many are free
public class OverviewLineRow
{
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Covered { get; set; }
    public int Delivered { get; set; }
    public int Waiting { get; set; }
    // Free of this type inside the circle / anywhere. Null = not counted (covered, asked enough, or no place)
    public int? FreeInside { get; set; }
    public int? FreeAnywhere { get; set; }
    public int Missing => Math.Max(0, Quantity - Covered);
}

// One resource in "Utlånt nå" or "Må følges opp"
public class OverviewLoanRow
{
    public int RequestId { get; set; }
    public int NeedId { get; set; }
    public string NeedName { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public bool ResourceDeleted { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerPhone { get; set; }
    public string? OwnerEmail { get; set; }
    // Since when: the yes (or the start of a planned need), the need was closed, or delivered
    public DateTime? Since { get; set; }
    // Said yes over 2 hours ago and still not delivered: call the owner
    public bool Late { get; set; }
}