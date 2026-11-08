using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;

// "Oversikt": the operator's main page - numbers, what needs attention, and the active needs
public class OverviewViewModel
{
    // The number strip
    public int ActiveNeeds { get; set; }
    public int ActiveAkutt { get; set; }
    public int Waiting { get; set; }
    public int Overdue { get; set; }
    public int OutNow { get; set; }

    public List<AttentionItem> Attention { get; set; } = new();
    public List<OverviewNeedRow> Needs { get; set; } = new();
}

// One thing the operator should do something about, with a link to the need
public record AttentionItem(int NeedId, string Icon, string Text, DateTime? When);

// One active need in the list
public class OverviewNeedRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NeedUrgency Urgency { get; set; }
    public DateTime? NeededFrom { get; set; }
    public int Covered { get; set; }
    public int Quantity { get; set; }
    public int Waiting { get; set; }
}