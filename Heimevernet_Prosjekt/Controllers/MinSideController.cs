using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers;

[Authorize]
public class MinSideController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public MinSideController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewData["Roles"] = string.Join(", ", await _userManager.GetRolesAsync(user));
        return View(user);
    }

    public async Task<IActionResult> EditProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var model = new Models.ViewModels.Account.EditProfileViewModel
        {
            Phone = user.PhoneNumber
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(Models.ViewModels.Account.EditProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        user.PhoneNumber = model.Phone;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        { foreach (var error in result.Errors)
            { 
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
         }

        TempData["Message"] = "Profilen din har blitt oppdatert.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteProfile(string? password)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    if (string.IsNullOrEmpty(password) ||
        !await _userManager.CheckPasswordAsync(user, password))
    {
        ModelState.AddModelError(string.Empty, "Feil passord.");
        return RedirectToAction("Index");
    }

    var result = await _userManager.DeleteAsync(user);
    if (!result.Succeeded)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View();
    }

    return RedirectToAction("Index");
}
    [HttpGet]
    public async Task<IActionResult> DeleteProfile()
    {
        return View();
    }

}
