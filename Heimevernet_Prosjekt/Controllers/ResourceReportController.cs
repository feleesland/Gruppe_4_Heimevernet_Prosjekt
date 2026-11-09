using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Heimevernet_Prosjekt.Controllers;

// Messages from operators to admin about a resource ("Meld til admin") - Docs: Role-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/roles
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Operator)]
public class ResourceReportController(IResourceRepository resourceRepository, IResourceReportRepository reportRepository,
    IAuthorizationService authorizationService) : Controller
{
    // Sends a message to admin; the user must be allowed to see the resource
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int resourceId, string? message)
    {
        var resource = resourceRepository.GetById(resourceId);
        if (resource == null)
        {
            return NotFound();
        }

        var authorizationResult = await authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Read);
        if (!authorizationResult.Succeeded)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 1000)
        {
            TempData["Message"] = "Skriv en melding til admin (maks 1000 tegn).";
            return RedirectToAction("Index", "Resource", new { id = resourceId });
        }

        // UserName is the email, so User.Identity.Name is the reporter's email
        reportRepository.Create(
            resourceId,
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            message);

        TempData["Message"] = "Meldingen er sendt til admin.";
        return RedirectToAction("Index", "Resource", new { id = resourceId });
    }

    // Admin marks a message as solved; it disappears from the page, the list marker and the bell
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Admin)]
    public IActionResult Resolve(int id, int resourceId)
    {
        reportRepository.Resolve(id);
        TempData["Message"] = "Meldingen er markert som løst.";
        return RedirectToAction("Index", "Resource", new { id = resourceId });
    }
}