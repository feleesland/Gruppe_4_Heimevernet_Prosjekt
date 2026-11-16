using Heimevernet_Prosjekt.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Home;

namespace Heimevernet_Prosjekt.Controllers;

public class HomeController : Controller
{
    private readonly HeimevernetDbContext _context;

    public HomeController(HeimevernetDbContext context)
    {
        _context = context;
    }

   
    //definerer en liste som en in-memory lagring
    private static List<PositionModel> positions = new List<PositionModel>();
    // The front page, with "Aktive kriser": needs that have "Info til innbyggere", for everyone (help with AI)
    // Needs without an update are not public. "Nylig løst" = closed the last 7 days
    public IActionResult Index()
    {
        // All updates, newest first per need (a small table, so it is grouped in C#)
        var updates = LatestUpdates();
        var ids = updates.Keys.ToList();
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var needs = _context.ResourceNeeds
            .Include(need => need.Items)
            .AsNoTracking()
            .Where(need => ids.Contains(need.Id) && (need.ClosedAt == null || need.ClosedAt > weekAgo))
            .ToList();

        // How many said yes per line, for "1 av 3 på plass" (admin and operator only)
        var itemIds = needs.SelectMany(need => need.Items).Select(item => item.Id).ToList();
        var accepted = _context.ResourceRequests
            .AsNoTracking()
            .Where(request => request.Status == RequestStatus.Godtatt && itemIds.Contains(request.ResourceNeedItemId))
            .GroupBy(request => request.ResourceNeedItemId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionary(x => x.Key, x => x.Count);

        var cards = needs.Select(need => new CrisisCardViewModel
        {
            Id = need.Id,
            Name = need.Name,
            Urgency = need.Urgency,
            Place = string.IsNullOrEmpty(need.Municipality) ? need.City : need.Municipality,
            CreatedAt = need.CreatedAt,
            NeededFrom = need.NeededFrom,
            NeededTo = need.NeededTo,
            ClosedAt = need.ClosedAt,
            LatestText = updates[need.Id][0].Text,
            LatestAt = updates[need.Id][0].At,
            UpdateCount = updates[need.Id].Count,
            ItemsText = string.Join(", ", need.Items.Select(item => $"{item.Quantity} × {item.Type}")),
            Quantity = need.Items.Sum(item => item.Quantity),
            Covered = need.Items.Sum(item => Math.Min(item.Quantity, accepted.GetValueOrDefault(item.Id)))
        }).ToList();

        // Akutt first, then the newest update. At most 5 active and 3 solved
        ViewData["ActiveCrises"] = cards.Where(card => card.ClosedAt == null)
            .OrderBy(card => card.Urgency).ThenByDescending(card => card.LatestAt).Take(5).ToList();
        ViewData["SolvedCrises"] = cards.Where(card => card.ClosedAt != null)
            .OrderByDescending(card => card.ClosedAt).Take(3).ToList();
        return View();
    }

    // All "Info til innbyggere" updates, newest first per need (a small table, so it is grouped in C#)
    private Dictionary<int, List<NeedUpdate>> LatestUpdates()
    {
        return _context.NeedUpdates.AsNoTracking().ToList()
            .GroupBy(update => update.ResourceNeedId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(update => update.At).ToList());
    }

    // The crises on the map for guests and users: only active needs with "Info til innbyggere",
    // and only what is public (no resources). No [Authorize], so everyone can fetch it (help with AI)
    [HttpGet]
    public IActionResult PublicCrises()
    {
        var updates = LatestUpdates();
        var ids = updates.Keys.ToList();
        var needs = _context.ResourceNeeds
            .AsNoTracking()
            .Where(need => ids.Contains(need.Id) && need.ClosedAt == null && need.Latitude != null && need.Longitude != null)
            .ToList();

        return Json(needs.Select(need => new
        {
            id = need.Id,
            name = need.Name,
            urgency = need.Urgency.ToString(),
            radiusKm = need.RadiusKm,
            latitude = need.Latitude,
            longitude = need.Longitude,
            place = string.IsNullOrEmpty(need.Municipality) ? need.City : need.Municipality,
            neededFrom = need.NeededFrom,
            createdAt = need.CreatedAt,
            latestText = updates[need.Id][0].Text,
            latestAt = updates[need.Id][0].At
        }));
    }

    [HttpGet]
    public IActionResult Map()
    {
        return View();
    }
    [HttpPost]
    public IActionResult Map(PositionModel model)
    {
        if (ModelState.IsValid)
        {
            //Legger ny posisjon til "positions" listen
            positions.Add(model);

            return View("Kart", positions);
        }
        return View();
    }


    [HttpGet]
    public async Task<IActionResult> PermanentMapMarker()
    {
        var locations = await _context.PermanentMapMarkers
            .AsNoTracking()
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                type = x.Type,
                latitude = x.Latitude,
                longitude = x.Longitude,
                description = x.Description
            })
            .ToListAsync();

        return Json(locations);
    }

    [HttpGet]
    public async Task<IActionResult> PermanentMapMarkers()
    {
        return Ok(
            await _context.PermanentMapMarkers.ToListAsync()
        );
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}
