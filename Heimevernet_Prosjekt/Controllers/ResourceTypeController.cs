using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers;

// Admin page for resource types: add, rename and delete (help with AI)
// Only admin can use this page - Docs: Role-based authorization
// https://learn.microsoft.com/aspnet/core/security/authorization/roles
[Authorize(Roles = RoleNames.Admin)]
public class ResourceTypeController : Controller
{
    // "Annet" is the fallback type for everything that doesn't fit, so it can't be renamed or deleted
    private const string OtherType = "Annet";

    private readonly IResourceTypeRepository typeRepository;

    public ResourceTypeController(IResourceTypeRepository typeRepository)
    {
        this.typeRepository = typeRepository;
    }

    // List of all types, with how many resources use each one
    [HttpGet]
    public IActionResult Index()
    {
        var types = typeRepository.GetAll();
        ViewData["Counts"] = types.ToDictionary(t => t.Id, t => typeRepository.CountResourcesUsing(t.Name));
        return View(types);
    }

    // New type from the form at the top of the list
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(string? name, string? examples)
    {
        name = name?.Trim();
        examples = string.IsNullOrWhiteSpace(examples) ? null : examples.Trim();

        var error = Validate(name, examples, currentName: null);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        typeRepository.Create(name!, examples);
        TempData["Message"] = $"«{name}» er lagt til.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var type = typeRepository.GetById(id);
        if (type == null)
        {
            return NotFound();
        }
        return View(type);
    }

    // Renaming also updates the resources that use the old name (done in EfResourceTypeRepository.Update)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, string? name, string? examples)
    {
        var type = typeRepository.GetById(id);
        if (type == null)
        {
            return NotFound();
        }

        name = name?.Trim();
        examples = string.IsNullOrWhiteSpace(examples) ? null : examples.Trim();

        string? error = null;
        if (type.Name == OtherType && name != OtherType)
        {
            error = $"«{OtherType}» kan ikke få nytt navn, men du kan endre eksemplene.";
        }
        error ??= Validate(name, examples, currentName: type.Name);

        if (error != null)
        {
            // Show the form again with what the user typed - Docs: Model validation
            // https://learn.microsoft.com/aspnet/core/mvc/models/validation
            ModelState.AddModelError(string.Empty, error);
            return View(new ResourceType { Id = id, Name = name ?? string.Empty, Examples = examples });
        }

        var oldName = type.Name;
        typeRepository.Update(id, name!, examples);
        TempData["Message"] = oldName == name
            ? $"«{name}» er oppdatert."
            : $"«{oldName}» har fått nytt navn: «{name}».";
        return RedirectToAction(nameof(Index));
    }

    // Deleting is allowed even if resources use the type: they keep the old name and are marked "Gammel type"
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var type = typeRepository.GetById(id);
        if (type == null)
        {
            return NotFound();
        }

        if (type.Name == OtherType)
        {
            TempData["Error"] = $"«{OtherType}» kan ikke slettes.";
            return RedirectToAction(nameof(Index));
        }

        var count = typeRepository.CountResourcesUsing(type.Name);
        typeRepository.Delete(id);
        TempData["Message"] = count == 0
            ? $"«{type.Name}» er slettet."
            : $"«{type.Name}» er slettet. {count} {(count == 1 ? "ressurs må" : "ressurser må")} få ny type.";
        return RedirectToAction(nameof(Index));
    }

    // Same rules as on ResourceType (Required, MaxLength 100 / 300). currentName lets a type keep its own name when editing
    private string? Validate(string? name, string? examples, string? currentName)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Skriv inn et navn.";
        }
        if (name.Length > 100)
        {
            return "Navnet kan ikke være lengre enn 100 tegn.";
        }
        if (examples != null && examples.Length > 300)
        {
            return "Eksemplene kan ikke være lengre enn 300 tegn.";
        }
        if (!string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase) && typeRepository.Exists(name))
        {
            return $"Typen «{name}» finnes allerede.";
        }
        return null;
    }
}