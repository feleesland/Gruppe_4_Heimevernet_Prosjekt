using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Controllers;

// The status buttons on the resource page. Only those who may edit the resource (owner, admin) - Docs: Resource-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
[Authorize]
public class ResourceStatusController : Controller
{
    // Gyldig (= "Ledig") or Ugyldig. "I bruk" and "Lever tilbake" come from the needs, "Ikke tilgjengelig" from SetUnavailable
    private static readonly string[] Allowed = { "Ledig", "Ugyldig" };

    private readonly IResourceRepository _resourceRepository;
    private readonly IResourceRequestRepository _requestRepository;
    private readonly IAuthorizationService _authorizationService;

    public ResourceStatusController(IResourceRepository resourceRepository,
        IResourceRequestRepository requestRepository, IAuthorizationService authorizationService)
    {
        _resourceRepository = resourceRepository;
        _requestRepository = requestRepository;
        _authorizationService = authorizationService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id, string status)
    {
        if (!Allowed.Contains(status))
        {
            return BadRequest();
        }

        var resource = _resourceRepository.GetById(id);
        if (resource == null)
        {
            return NotFound();
        }

        var canEdit = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Update);
        if (!canEdit.Succeeded)
        {
            return Forbid();
        }

        // Lent out to a need: it becomes free when it is marked as returned, not by hand
        if (_requestRepository.GetResourceIdsInUse().Contains(id))
        {
            TempData["Message"] = "Ressursen er utlånt til et behov. Den blir ledig når den er markert som levert tilbake.";
            return RedirectToAction("Index", "Resource", new { id });
        }

        _resourceRepository.SetStatus(id, status);
        TempData["Message"] = $"Status er endret til «{status}».";
        return RedirectToAction("Index", "Resource", new { id });
    }
    // "⏸ Ikke tilgjengelig": the time in the form is Norwegian time, saved as UTC
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUnavailable(int id, string? reason, DateTime? until, bool unknown)
    {
        var resource = _resourceRepository.GetById(id);
        if (resource == null)
        {
            return NotFound();
        }

        var canEdit = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Update);
        if (!canEdit.Succeeded)
        {
            return Forbid();
        }

        if (_requestRepository.GetResourceIdsInUse().Contains(id))
        {
            TempData["Message"] = "Ressursen er utlånt til et behov. Den blir ledig når den er markert som levert tilbake.";
            return RedirectToAction("Index", "Resource", new { id });
        }

        DateTime? untilUtc = null;
        if (!unknown)
        {
            if (until == null)
            {
                TempData["Message"] = "Velg når ressursen er ledig igjen, eller kryss av for «Vet ikke når».";
                return RedirectToAction("Index", "Resource", new { id });
            }
            var oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
            untilUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(until.Value, DateTimeKind.Unspecified), oslo);
            if (untilUtc <= DateTime.UtcNow)
            {
                TempData["Message"] = "Tidspunktet må være fram i tid.";
                return RedirectToAction("Index", "Resource", new { id });
            }
        }

        if (reason?.Length > 200)
        {
            reason = reason[..200];
        }

        _resourceRepository.SetUnavailable(id, reason, untilUtc);
        TempData["Message"] = "Ressursen er satt til «Ikke tilgjengelig».";
        return RedirectToAction("Index", "Resource", new { id });
    }
    // "Endre" on Tilgjengelig (help with AI): Hele tiden, faste tider (also over midnight, e.g. 16:00-08:00) or a period.
    // Only the owner: the times belong to them. Admin can set "Ikke tilgjengelig" (SetUnavailable) or "Ugyldig" (Set)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAvailability(int id, AvailabilityKind kind, TimeSpan? fromTime, TimeSpan? toTime,
        List<int>? days, DateTime? fromDate, DateTime? toDate)
    {
        var resource = _resourceRepository.GetById(id);
        if (resource == null)
        {
            return NotFound();
        }

        if (resource.OwnerId != User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            return Forbid();
        }

        if (_requestRepository.GetResourceIdsInUse().Contains(id))
        {
            TempData["Message"] = "Ressursen er utlånt til et behov. Tidene kan endres når den er levert tilbake.";
            return RedirectToAction("Index", "Resource", new { id });
        }

        // Only real weekdays (1, 2, 4 ... 64), added together to one number
        var dayBits = (days ?? new List<int>()).Where(d => d is >= 1 and <= 64 && (d & (d - 1)) == 0).Distinct().Sum();

        if (kind == AvailabilityKind.Weekly && (fromTime == null || toTime == null || dayBits == 0))
        {
            TempData["Message"] = "Velg fra, til og minst én dag.";
            return RedirectToAction("Index", "Resource", new { id });
        }
        if (kind == AvailabilityKind.Period && (fromDate == null || toDate == null || toDate < fromDate))
        {
            TempData["Message"] = "Velg en periode der «til» er etter «fra».";
            return RedirectToAction("Index", "Resource", new { id });
        }

        _resourceRepository.SetAvailability(id, kind,
            kind == AvailabilityKind.Weekly ? fromTime : null,
            kind == AvailabilityKind.Weekly ? toTime : null,
            kind == AvailabilityKind.Weekly ? dayBits : 127,
            kind == AvailabilityKind.Period ? fromDate : null,
            kind == AvailabilityKind.Period ? toDate : null);
        TempData["Message"] = "Tilgjengeligheten er lagret.";
        return RedirectToAction("Index", "Resource", new { id });
    }
}