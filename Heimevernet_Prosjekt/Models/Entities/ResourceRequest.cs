using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities
{
    // A request to the owner of one resource for one need line, from "sent" to "returned" (help with AI)
    public class ResourceRequest
    {
        public int Id { get; set; }

        // Which line in the need, e.g. "2 × Motoriserte kjøretøy"
        public int ResourceNeedItemId { get; set; }
        public ResourceNeedItem ResourceNeedItem { get; set; } = null!;

        // Which resource is asked for. Only the id, same pattern as ResourceReport,
        // so the request is kept in the history even if the resource is soft-deleted
        public int ResourceId { get; set; }

        // Who sent it (operator or admin)
        public string RequestedById { get; set; } = string.Empty;

        public RequestStatus Status { get; set; } = RequestStatus.Sendt;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // The deadline. After this a request that is still Sendt shows as "Ikke svart"
        public DateTime AnswerBy { get; set; }

        // The owner's answer
        public DateTime? AnsweredAt { get; set; }
        public DeliveryMethod? Delivery { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        // The operator got the yes on the phone and registered it
        public bool ConfirmedByPhone { get; set; }

        // Step 5: delivered and returned
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReturnedAt { get; set; }

        // "Ikke svart": still Sendt after the deadline (not stored, worked out when shown)
        public bool IsUnanswered(DateTime nowUtc) => Status == RequestStatus.Sendt && AnswerBy < nowUtc;
    }
}
