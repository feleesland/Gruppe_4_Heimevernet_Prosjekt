using System.Text;
using System.Text.Encodings.Web;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Heimevernet_Prosjekt.Controllers;

[Authorize]
public class MinSideController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IResourceRepository _resourceRepository;
    private readonly IEmailSender _emailSender;

    public MinSideController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IResourceRepository resourceRepository,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _resourceRepository = resourceRepository;
        _emailSender = emailSender;
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
            Phone = user.PhoneNumber,
            ActorKind = user.ActorKind,
            SelectedSkills = ActorTypes.Split(user.ActorSkills).ToList()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(Models.ViewModels.Account.EditProfileViewModel model)
    {
        var isOperator = User.IsInRole(RoleNames.Operator);

        var actorError = ActorTypes.Validate(model.ActorKind, model.SelectedSkills, isOperator);
        if (actorError != null) ModelState.AddModelError(string.Empty, actorError);

        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        user.PhoneNumber = model.Phone;
        user.ActorKind = string.IsNullOrEmpty(model.ActorKind) ? null : model.ActorKind;
        user.ActorSkills = isOperator || model.SelectedSkills.Count == 0
            ? null
            : string.Join(";", model.SelectedSkills.Distinct());

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["Message"] = "Profilen din har blitt oppdatert.";
        return RedirectToAction("Index");
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPasswordReset()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.Email == null) return Challenge();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Action("ResetPassword", "Account",
            new { code, email = user.Email }, Request.Scheme);

        await _emailSender.SendEmailAsync(user.Email, "Tilbakestill passord",
            $"Tilbakestill passordet ditt ved å <a href='{HtmlEncoder.Default.Encode(link!)}'>klikke her</a>.");

        TempData["Message"] = $"Vi har sendt en lenke til {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DeleteProfile() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProfile(string? password)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        
        if (await _userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            ModelState.AddModelError(string.Empty,
                "Administratorer kan ikke slette seg selv. Be en annen administrator om å gjøre det.");
            return View();
        }

        if (string.IsNullOrEmpty(password) ||
            !await _userManager.CheckPasswordAsync(user, password))
        {
            ModelState.AddModelError(string.Empty, "Feil passord.");
            return View();
        }

        
        _resourceRepository.RemoveOwner(user.Id);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

}