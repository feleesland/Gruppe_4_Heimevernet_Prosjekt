using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Security.Authorization;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers;

// Suggestions for the search in the menu: the user's own resources + places from Kartverket - Docs: Resource-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
[Authorize]
public class SuggestionsController(IResourceRepository resourceRepository, IAuthorizationService authorizationService,
    KartverketService kartverket) : Controller
{
    // lat/lng = the user's position (optional, for "Min posisjon" later), so the closest places come first
    [HttpGet]
    public async Task<IActionResult> Index(string? q, double? lat, double? lng)
    {
        // Wait until at least 2 letters are typed
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Json(new { resources = Array.Empty<object>(), places = Array.Empty<PlaceSuggestion>() });
        }
        q = q.Trim();

        // Resources: same search fields and the same access check as the list.
        // Best matches first (exact name, then name that starts with the text), max 5 in the list
        var matches = new List<Resource>();
        foreach (var r in resourceRepository.GetAll())
        {
            string?[] fields = [r.Name, r.Type, r.Description, r.Address, r.ZipCode, r.City];
            if (!fields.Any(f => f != null && f.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            var authorizationResult = await authorizationService.AuthorizeAsync(User, r, ResourceOperations.Read);
            if (authorizationResult.Succeeded)
            {
                matches.Add(r);
            }
        }

        var resources = matches
            .OrderByDescending(r => string.Equals(r.Name, q, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => r.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.Name)
            .Take(5)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                type = r.Type,
                address = string.Join(", ", new[] { r.Address, r.City }.Where(s => !string.IsNullOrWhiteSpace(s))),
                latitude = r.Latitude,
                longitude = r.Longitude
            })
            .ToList();

        var places = await kartverket.SearchAsync(q, lat, lng);
        return Json(new { resources, places });
    }
}