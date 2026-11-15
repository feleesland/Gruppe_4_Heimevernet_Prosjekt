using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;

// "Oversikt": the operator's main page. Active needs first (the ones that need something on top),
// then resources out now and requests to follow up (help with AI)
public class OverviewViewModel
{
    // The number strip
    public int ActiveAkutt { get; set; }
    public int ActivePlanned { get; set; }
    public int Waiting { get; set; }
    public int OutNow { get; set; }

    // Active needs, sorted: red, then orange, then the rest
    public List<OverviewNeedRow> Needs { get; set; } = new();

    // "Ute nå": said yes but not delivered, need closed (must go back), and in use
    public List<OverviewLoanRow> NotDelivered { get; set; } = new();
    public List<OverviewLoanRow> GiveBack { get; set; } = new();
    public List<OverviewLoanRow> InUse { get; set; } = new();

    // "Forespørsler": deadline passed (per need), waiting, and said no the last 24 hours (per need)
    public List<OverviewRequestGroup> Overdue { get; set; } = new();
    public List<OverviewRequestRow> WaitingList { get; set; } = new();
    public List<OverviewRequestGroup> Declined { get; set; } = new();
}

// One active need in the list
public class OverviewNeedRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public NeedUrgency Urgency { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? NeededFrom { get; set; }
    public double RadiusKm { get; set; }

    public int Covered { get; set; }
    public int Quantity { get; set; }
    public int Delivered { get; set; }
    public int Waiting { get; set; }
    public DateTime? NextAnswerBy { get; set; }
    public int Overdue { get; set; }

    public bool NobodyAsked { get; set; }
    public bool NoneNearby { get; set; }
    // Free resources of the missing types inside the radius. Null = nothing to look for (or no place)
    public int? FreeNearby { get; set; }
    public bool StartsSoon { get; set; }

    // 2 = red (must do something now), 1 = orange (starts soon), 0 = fine
    public int Level { get; set; }
    public int Missing => Math.Max(0, Quantity - Covered);
}

// One resource in the "Ute nå" box
public class OverviewLoanRow
{
    public int RequestId { get; set; }
    public int NeedId { get; set; }
    public string NeedName { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerPhone { get; set; }
    // Since when: the yes (or the start of a planned need), the need was closed, or delivered
    public DateTime? Since { get; set; }
    // Said yes over 2 hours ago and still not delivered: call the owner
    public bool Late { get; set; }
}

// One request waiting for an answer
public record OverviewRequestRow(int RequestId, int NeedId, string NeedName, string ResourceName, DateTime AnswerBy);

// Several requests in the same need (deadline passed, or said no)
public record OverviewRequestGroup(int NeedId, string NeedName, List<string> ResourceNames, DateTime When);