using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.Home
{
    // One crisis on the front page ("Aktive kriser" / "Nylig løst"). Everyone sees the name, place and the latest
    // "Info til innbyggere"; ItemsText, Covered and Quantity are only shown to admin and operator (help with AI)
    public class CrisisCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NeedUrgency Urgency { get; set; }
        public string Place { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? NeededFrom { get; set; }
        public DateTime? NeededTo { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string LatestText { get; set; } = string.Empty;
        public DateTime LatestAt { get; set; }
        public int UpdateCount { get; set; }
        public string ItemsText { get; set; } = string.Empty;
        public int Covered { get; set; }
        public int Quantity { get; set; }
    }
}