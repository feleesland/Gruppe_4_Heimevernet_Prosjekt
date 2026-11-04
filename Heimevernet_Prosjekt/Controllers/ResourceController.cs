using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers
{
    // Users must log in before accessing these pages.
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;
        private readonly IAuthorizationService _authorizationService;

        public ResourceController(
            IResourceRepository resourceRepository,
            IAuthorizationService authorizationService)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? id)
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

            // Use the shared authorization rules to check access.
            var authorizationResult = await _authorizationService
                .AuthorizeAsync(User, resource, ResourceOperations.Read);

            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            // Include the resource details and map position.
            var viewModel = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type,
                Address = resource.Address,
                ZipCode = resource.ZipCode,
                City = resource.City,
                Latitude = resource.Latitude,
                Longitude = resource.Longitude,
                CreatedAt = resource.CreatedAt,
                OwnerRemoved = resource.OwnerRemoved
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var model = new List<ResourceViewModel>();

            // Include only resources the user is allowed to view.
            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                if (authorizationResult.Succeeded)
                {
                    model.Add(new ResourceViewModel
                    {
                        Id = resource.Id,
                        Name = resource.Name,
                        Description = resource.Description,
                        Type = resource.Type
                    });
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MapResources()
        {
            var resources = new List<object>();

            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                // Map markers need permission and both coordinates.
                if (authorizationResult.Succeeded &&
                    resource.Latitude != null &&
                    resource.Longitude != null)
                {
                    resources.Add(new
                    {
                        id = resource.Id,
                        name = resource.Name,
                        description = resource.Description,
                        type = resource.Type,
                        latitude = resource.Latitude,
                        longitude = resource.Longitude
                    });
                }
            }

            return Json(resources);
        }
    }
}