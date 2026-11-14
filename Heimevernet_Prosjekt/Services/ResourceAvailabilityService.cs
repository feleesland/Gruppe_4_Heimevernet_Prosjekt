using Heimevernet_Prosjekt.DataAccess;
using Microsoft.EntityFrameworkCore;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Services;

// Every minute: resources that were "Ikke tilgjengelig" until a time that has passed become "Ledig" again (help with AI)
// It also works out fixed times and periods (IsAvailableAt / NextAvailableText at the bottom), used by the map and the list
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

    // ---- Fixed times and periods (help with AI) ----
    // Not saved as a status like above, because "16:00-08:00 Mon-Fri" repeats: it is worked out every time the map or the list is shown.
    // Norwegian time - Docs: TimeZoneInfo.ConvertTimeFromUtc - https://learn.microsoft.com/dotnet/api/system.timezoneinfo.converttimefromutc
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
    private static readonly string[] DayNames = ["søn", "man", "tir", "ons", "tor", "fre", "lør"];

    // The bit for one weekday: Monday = 1, Tuesday = 2 ... Sunday = 64 (DayOfWeek starts with Sunday = 0)
    public static int DayBit(DayOfWeek day) => 1 << (((int)day + 6) % 7);

    // Is the resource inside its times at this moment? Always = yes
    public static bool IsAvailableAt(Resource resource, DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Oslo);
        if (resource.AvailabilityKind == AvailabilityKind.Period)
        {
            return (resource.AvailableFromDate == null || local.Date >= resource.AvailableFromDate.Value.Date)
                && (resource.AvailableToDate == null || local.Date <= resource.AvailableToDate.Value.Date);
        }
        if (resource.AvailabilityKind != AvailabilityKind.Weekly)
        {
            return true;
        }

        var from = resource.AvailableFromTime ?? TimeSpan.Zero;
        var to = resource.AvailableToTime ?? TimeSpan.Zero;
        var time = local.TimeOfDay;
        var today = (resource.AvailableDays & DayBit(local.DayOfWeek)) != 0;
        if (from == to) return today;                                   // the whole day
        if (from < to) return today && time >= from && time < to;       // e.g. 08:00-16:00
        // Over midnight, e.g. 16:00-08:00: the evening belongs to today, the early morning to the day before
        if (time >= from) return today;
        return time < to && (resource.AvailableDays & DayBit(local.AddDays(-1).DayOfWeek)) != 0;
    }

    // When it is free next, as short text: "16:00", "i morgen 08:00", "man 08:00" or "20.11". Null = not within a week, or never
    public static string? NextAvailableText(Resource resource, DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Oslo);
        if (resource.AvailabilityKind == AvailabilityKind.Period)
        {
            var startDate = resource.AvailableFromDate?.Date;
            return startDate > local.Date ? startDate.Value.ToString("dd.MM") : null;
        }
        if (resource.AvailabilityKind != AvailabilityKind.Weekly)
        {
            return null;
        }

        // The next start time on a chosen weekday, at most a week ahead
        var from = resource.AvailableFromTime ?? TimeSpan.Zero;
        for (var i = 0; i <= 7; i++)
        {
            var day = local.Date.AddDays(i);
            var start = day + from;
            if (start > local && (resource.AvailableDays & DayBit(day.DayOfWeek)) != 0)
            {
                var clock = start.ToString("HH:mm");
                return i == 0 ? clock : i == 1 ? "i morgen " + clock : DayNames[(int)day.DayOfWeek] + " " + clock;
            }
        }
        return null;
    }
}