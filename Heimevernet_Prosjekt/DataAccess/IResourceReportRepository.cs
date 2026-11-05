using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.DataAccess;

// One open message with the resource's name, for the notification bell (🔔) next to the name
public sealed record ReportNotification(int Id, int ResourceId, string ResourceName, string ReporterEmail, string Message, DateTime CreatedAt);

// Messages from operators to admin about a resource (same pattern as IResourceRepository)
public interface IResourceReportRepository
{
    ResourceReport Create(int resourceId, string reporterId, string reporterEmail, string message);

    // Open messages on one resource (shown to admin on the resource page)
    IReadOnlyCollection<ResourceReport> GetOpenForResource(int resourceId);

    // Open messages for the bell, newest first
    IReadOnlyCollection<ReportNotification> GetOpenNotifications(int max = 10);

    // Number of open messages in total (the number on the bell)
    int CountOpen();

    // Number of open messages per resource (the "⚑ X meldinger" marker in the list)
    IReadOnlyDictionary<int, int> CountOpenByResource();

    // Admin marks the message as solved
    bool Resolve(int id);
}