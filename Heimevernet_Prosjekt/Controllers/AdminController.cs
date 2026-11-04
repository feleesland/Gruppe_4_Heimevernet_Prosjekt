using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Admin;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Heimevernet_Prosjekt.Models.Entities;

namespace Heimevernet_Prosjekt.Controllers
{
    // Only admin can see users and change roles - Docs: Role-based authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/roles
    [Authorize(Roles = RoleNames.Admin)]
    public class AdminController(UserManager<ApplicationUser> userManager, IResourceRepository resourceRepository) : Controller
    {
        // The roles admin can choose between (Partner comes later)
        public static readonly string[] Roles = [RoleNames.Admin, RoleNames.Operator, RoleNames.User];

        // List of users with name, email and role, with search on email - Docs: Introduction to Identity on ASP.NET Core
        // https://learn.microsoft.com/aspnet/core/security/authentication/identity
        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var users = userManager.Users;
            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u => u.Email != null && u.Email.Contains(search));
            }

            var model = new List<UserRoleViewModel>();
            foreach (var user in users.OrderBy(u => u.Email).ToList())
            {
                var roles = await userManager.GetRolesAsync(user);
                model.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? string.Empty,
                    Name = $"{user.FirstName} {user.LastName}".Trim(),
                    // Users created before roles were added are shown as User
                    Role = roles.FirstOrDefault() ?? RoleNames.User
                });
            }

            ViewData["Search"] = search;
            return View(model);
        }

        // Gives the user one role (replaces the old one) (help with AI)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(string userId, string role)
        {
            if (!Roles.Contains(role))
            {
                return BadRequest();
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Admin can't change their own role, so there is always at least one admin
            if (user.Id == userManager.GetUserId(User))
            {
                TempData["Message"] = "Du kan ikke endre din egen rolle.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, role);

            TempData["Message"] = $"{user.Email} har nå rollen {RoleNames.DisplayName(role)}.";
            return RedirectToAction(nameof(Index));
        }

        // All resources one user has registered (admin only, see [Authorize] above)
        [HttpGet]
        public async Task<IActionResult> UserResources(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var model = resourceRepository.GetAll()
                .Where(r => r.OwnerId == user.Id)
                .Select(r => new ResourceViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    Type = r.Type,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt
                })
                .ToList();

            ViewData["UserId"] = user.Id;
            ViewData["Email"] = user.Email;
            return View(model);
        }

        // Deletes a user; resources are kept (owner removed) or soft deleted (help with AI)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId, bool deleteResources)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Admin can't delete themselves, so there is always at least one admin
            if (user.Id == userManager.GetUserId(User))
            {
                TempData["Message"] = "Du kan ikke slette deg selv.";
                return RedirectToAction(nameof(Index));
            }

            if (deleteResources)
            {
                resourceRepository.DeleteAllByOwner(user.Id);
            }
            resourceRepository.RemoveOwner(user.Id);

            await userManager.DeleteAsync(user);

            TempData["Message"] = deleteResources
                ? $"{user.Email} og ressursene deres er slettet."
                : $"{user.Email} er slettet. Ressursene er beholdt.";
            return RedirectToAction(nameof(Index));
        }
    }
}