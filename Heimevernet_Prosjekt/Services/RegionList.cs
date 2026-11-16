using System.Globalization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Microsoft.AspNetCore.Http;

namespace Heimevernet_Prosjekt.Services;

// The area (kommune) shared by Oversikt, Ressurser and Behov (help with AI): the kommune of a need or a resource,
// whether it is inside the chosen area, and the list of kommuner for the area picker (Views/Shared/_RegionPicker.cshtml)
public static class RegionList
{
    // "Hele Norge" in the address (?region=alle)
    public const string All = OverviewViewModel.RegionAll;

    private static readonly TextInfo Norwegian = CultureInfo.GetCultureInfo("nb-NO").TextInfo;

    // The kommune as "Kristiansand" (Kartverket writes it in capitals); the post town on old ones without a kommune
    public static string PlaceOf(string? municipality, string? city)
    {
        var place = string.IsNullOrWhiteSpace(municipality) ? city : municipality;
        return string.IsNullOrWhiteSpace(place) ? "" : Norwegian.ToTitleCase(place.Trim().ToLower());
    }

    // The chosen area: the cookie the browser keeps (region-picker.js writes it when an area is chosen) wins over ?region=,
    // so the page comes right the first time, without jumping to another address (no flicker). No cookie: the address
    // Docs: Cookies in ASP.NET Core (HttpRequest.Cookies) - https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httprequest.cookies
    public const string Cookie = "hvRegion";

    public static string? Chosen(HttpRequest request, string? fromAddress)
    {
        var fromCookie = request.Cookies[Cookie];
        var region = string.IsNullOrWhiteSpace(fromCookie) ? fromAddress : Uri.UnescapeDataString(fromCookie);
        return string.IsNullOrWhiteSpace(region) ? null : region.Trim();
    }

    // No area chosen and "Hele Norge" show everything
    public static bool InRegion(string place, string? region) =>
        string.IsNullOrWhiteSpace(region)
        || region == All
        || string.Equals(place, region.Trim(), StringComparison.OrdinalIgnoreCase);

    // The kommuner with active needs or resources, the most active first
    public static List<OverviewRegion> Build(IEnumerable<ResourceNeed> needs, IEnumerable<Resource> resources)
    {
        var activePlaces = needs.Where(n => n.ClosedAt == null).Select(n => PlaceOf(n.Municipality, n.City)).ToList();
        var resourcePlaces = resources.Select(r => PlaceOf(r.Municipality, r.City)).ToList();
        return activePlaces.Concat(resourcePlaces)
            .Where(p => p != "")
            .Distinct()
            .Select(p => new OverviewRegion(p, activePlaces.Count(x => x == p), resourcePlaces.Count(x => x == p)))
            .OrderByDescending(r => r.Needs).ThenByDescending(r => r.Resources).ThenBy(r => r.Name)
            .ToList();
    }
}
