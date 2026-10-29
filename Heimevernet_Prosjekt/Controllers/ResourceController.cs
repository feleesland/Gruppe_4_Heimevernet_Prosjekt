using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Heimevernet_Prosjekt.Models.Authorization;


namespace Heimevernet_Prosjekt.Controllers
{
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;

        public ResourceController(IResourceRepository resourceRepository)
        {
            _resourceRepository = resourceRepository;
        }
        
        public IActionResult Index(int? id)
        {
            var resource = id.HasValue ? _resourceRepository.GetById(id.Value) : null;
            var viewModel = new ResourceViewModel();
            if (resource == null)
            {
                return View(viewModel);
            }
            if (!CanAccess(resource.OwnerId))
            {
                return Forbid();
            }
            viewModel = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type
            };
            return View(viewModel);
        }
        //help with AI
        public IActionResult List()
        {
            var resources = _resourceRepository.GetAll();

            // Admin sees all resources, everyone else only their own
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

        // Admin can access everything, others only their own (help with AI)
        private bool CanAccess(string ownerId)
        {
            return User.IsInRole(RoleNames.Admin)
                || ownerId == User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
