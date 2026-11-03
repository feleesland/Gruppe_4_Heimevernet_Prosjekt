using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Heimevernet_Prosjekt.Controllers
{
    // Users must log in before accessing these pages.
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;

        public ResourceController(IResourceRepository resourceRepository)
        {
            // Keep the repository so we can find resources.
            _resourceRepository = resourceRepository;
        }

        [HttpGet]
        public IActionResult Index(int? id)
        {
            // A resource ID is needed to open this page.
            if (!id.HasValue)
            {
                return NotFound();
            }

            var resource = _resourceRepository.GetById(id.Value);

            // Return 404 if the resource does not exist.
            if (resource == null)
            {
                return NotFound();
            }

            // Only the owner or an administrator can view this resource.
            if (!CanAccess(resource.OwnerId))
            {
                return Forbid();
            }

            // Copy the information needed by the page.
            var viewModel = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult List()
        {
            var resources = _resourceRepository.GetAll();

            // Administrators see all resources; other users see their own.
            if (!User.IsInRole(RoleNames.Admin))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                resources = resources.Where(r => r.OwnerId == userId).ToList();
            }

            var model = resources.Select(r => new ResourceViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Type = r.Type
            }).ToList();

            return View(model);
        }

        // Allow access for administrators and the resource owner.
        private bool CanAccess(string ownerId)
        {
            return User.IsInRole(RoleNames.Admin)
                || ownerId == User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}