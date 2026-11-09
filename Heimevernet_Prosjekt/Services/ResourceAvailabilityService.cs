using Heimevernet_Prosjekt.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Heimevernet_Prosjekt.Services;

// Every minute: resources that were "Ikke tilgjengelig" until a time that has passed become "Ledig" again (help with AI)
// Docs: Background tasks with hosted services - https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services
public class ResourceAvailabilityService(IServiceScopeFactory scopeFactory, ILogger<ResourceAvailabilityService> logger) : BackgroundService
{
    public const string Unavailable = "Ikke tilgjengelig";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                // The DbContext is scoped, so the background service makes its own scope
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HeimevernetDbContext>();
                var now = DateTime.UtcNow;

                var backAgain = await db.Resources
                    .Where(r => r.Status == Unavailable && r.UnavailableUntil != null && r.UnavailableUntil <= now)
                    .ToListAsync(stoppingToken);

                foreach (var resource in backAgain)
                {
                    resource.Status = "Ledig";
                    resource.UnavailableReason = null;
                    resource.UnavailableUntil = null;
                }

                if (backAgain.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // E.g. the database isn't ready yet at startup: try again next minute
                logger.LogWarning(ex, "Kunne ikke oppdatere tilgjengelighet på ressurser");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}