using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers;

// The status buttons on the resource page. Only those who may edit the resource (owner, admin) - Docs: Resource-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
[Authorize]
public class ResourceStatusController : Controller
{
    private static readonly string[] Allowed = { "Ledig", "Opptatt", "Ugyldig" };

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
}