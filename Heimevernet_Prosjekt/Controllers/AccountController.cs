using Heimevernet_Prosjekt.Models.ViewModels.Account;
using Heimevernet_Prosjekt.Models.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Encodings.Web;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Controllers;

// Login, register, logout and password reset - Teacher: AccountController.cs
// [ValidateAntiForgeryToken] on all POST methods - Docs: Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks
// https://learn.microsoft.com/aspnet/core/security/anti-request-forgery
public sealed class AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null)
        {
            var result = await signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return RedirectToLocalUrl(model.ReturnUrl);
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // New user with name and phone; FirstName/LastName are our own fields on ApplicationUser, PhoneNumber is built into IdentityUser - Docs: Add, download, and delete custom user data
        // https://learn.microsoft.com/aspnet/core/security/authentication/add-user-data
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            FirstName = model.FirstName,
            LastName = model.LastName
        };
        var result = await userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // New accounts always get the User role, never chosen by the user - Docs: Role-based authorization
            // https://learn.microsoft.com/aspnet/core/security/authorization/roles
            await userManager.AddToRoleAsync(user, RoleNames.User);

            // Send a link the user must click before they can log in - Docs: Account confirmation and password recovery
            // https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm
            var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Action(
                nameof(ConfirmEmail),
                "Account",
                new { userId = user.Id, code = encodedCode },
                Request.Scheme)
                ?? throw new InvalidOperationException("Could not generate the confirmation URL.");

            await emailSender.SendEmailAsync(
                model.Email,
                "Bekreft e-posten din",
                $"Bekreft kontoen din ved å <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>klikke her</a>.");

            return RedirectToAction(nameof(RegisterConfirmation));
        }

        // Same result whether or not the email exists, so nobody can see who has an account (help with AI)
        var duplicateCodes = new[] { "DuplicateUserName", "DuplicateEmail" };
        var otherErrors = result.Errors.Where(e => !duplicateCodes.Contains(e.Code)).ToList();
        if (otherErrors.Count == 0)
        {
            await emailSender.SendEmailAsync(
                model.Email,
                "Noen prøvde å registrere seg med e-posten din",
                "Det finnes allerede en konto med denne e-posten. Har du glemt passordet, kan du bruke «Glemt passord» på innloggingssiden.");
            return RedirectToAction(nameof(RegisterConfirmation));
        }

        // Other errors (e.g. password too short) don't reveal anything about other users
        foreach (var error in otherErrors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // "Check your email" page after registering - Docs: Account confirmation and password recovery
    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    // The link in the email ends up here and confirms the account - Docs: Account confirmation and password recovery
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction("Index", "Home");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        IdentityResult result;
        try
        {
            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            result = await userManager.ConfirmEmailAsync(user, decodedCode);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed();
        }

        ViewData["Succeeded"] = result.Succeeded;
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            var code = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { code = encodedCode, email = user.Email },
                Request.Scheme)
                ?? throw new InvalidOperationException("Could not generate the password reset URL.");

            await emailSender.SendEmailAsync(
                user.Email!,
                "Reset your password",
                $"Reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? code = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("A password reset code and email address are required.");
        }

        return View(new ResetPasswordViewModel { Code = code, Email = email });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        string code;
        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "The password reset code is invalid.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, code, model.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    private IActionResult RedirectToLocalUrl(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home")!;
    }
}