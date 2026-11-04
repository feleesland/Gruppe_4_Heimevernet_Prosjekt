using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.DataAccess;

// Messages to admin with EF Core - same pattern as EfResourceRepository
public class EfResourceReportRepository(HeimevernetDbContext dbContext) : IResourceReportRepository
{
    public ResourceReport Create(int resourceId, string reporterId, string reporterEmail, string message)
    {
        var report = new ResourceReport
        {
            ResourceId = resourceId,
            ReporterId = reporterId,
            ReporterEmail = reporterEmail,
            Message = message.Trim()
        };
        dbContext.ResourceReports.Add(report);
        dbContext.SaveChanges();
        return report;
    }

    public IReadOnlyCollection<ResourceReport> GetOpenForResource(int resourceId)
    {
        return dbContext.ResourceReports
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId && r.ResolvedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ToList()
            .AsReadOnly();
    }

    // Joined with Resources to get the name; messages on deleted resources are not shown (help with AI)
    // Sorting happens on an anonymous type (EF Core can translate that to SQL); the record is built afterwards in memory
    public IReadOnlyCollection<ReportNotification> GetOpenNotifications(int max = 10)
    {
        return dbContext.ResourceReports
            .AsNoTracking()
            .Where(r => r.ResolvedAt == null)
            .Join(dbContext.Resources, report => report.ResourceId, resource => resource.Id,
                (report, resource) => new
                {
                    report.Id,
                    report.ResourceId,
                    ResourceName = resource.Name,
                    report.ReporterEmail,
                    report.Message,
                    report.CreatedAt
                })
            .OrderByDescending(x => x.CreatedAt)
            .Take(max)
            .ToList()
            .Select(x => new ReportNotification(x.Id, x.ResourceId, x.ResourceName, x.ReporterEmail, x.Message, x.CreatedAt))
            .ToList()
            .AsReadOnly();
    }

    public int CountOpen()
    {
        return dbContext.ResourceReports
            .Where(r => r.ResolvedAt == null)
            .Join(dbContext.Resources, report => report.ResourceId, resource => resource.Id, (report, resource) => report)
            .Count();
    }

    public IReadOnlyDictionary<int, int> CountOpenByResource()
    {
        return dbContext.ResourceReports
            .Where(r => r.ResolvedAt == null)
            .GroupBy(r => r.ResourceId)
            .Select(g => new { ResourceId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.ResourceId, x => x.Count);
    }

    public bool Resolve(int id)
    {
        var report = dbContext.ResourceReports.Find(id);
        if (report is null || report.ResolvedAt != null)
        {
            return false;
        }

        report.ResolvedAt = DateTime.UtcNow;
        dbContext.SaveChanges();
        return true;
    }
}