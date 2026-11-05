using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed
{
    // One line on the need page: what is needed, requests sent, and suggestions (help with AI)
    public class NeedLineViewModel
    {
        public int ItemId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }

        // How many owners have said yes
        public int Accepted { get; set; }
        public bool IsCovered => Accepted >= Quantity;

        public List<RequestRowViewModel> Requests { get; set; } = new();
        public List<SuggestionViewModel> Suggestions { get; set; } = new();
    }

    public class RequestRowViewModel
    {
        public int Id { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerPhone { get; set; }
        public RequestStatus Status { get; set; }
        public bool Unanswered { get; set; }
        public DeliveryMethod? Delivery { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime AnswerBy { get; set; }
        public bool ConfirmedByPhone { get; set; }
        public string? Comment { get; set; }
    }

    public class SuggestionViewModel
    {
        public int ResourceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? OwnerPhone { get; set; }

        // Empty when the resource has no position
        public double? DistanceKm { get; set; }
        public bool InsideCircle { get; set; }
    }
}