using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Heimevernet_Prosjekt.Models.Authorization;


namespace Heimevernet_Prosjekt.Controllers
{
    public class ResourceNeedController : Controller
    {
        private readonly IResourceNeedRepository _resourceNeedRepository;
        private readonly IAuthorizationService _authorizationService;
            
        public ResourceNeedController(IResourceNeedRepository resourceNeedRepository, IAuthorizationService authorizationService)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _authorizationService = authorizationService;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var resource = id.HasValue ? _resourceNeedRepository.GetById(id.Value) : null;
            var viewModel = new ResourceNeedViewModel();
            if (resource == null)
            {
                return View(viewModel);
            }

            // Owner, admin and operator can see the resource - Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceNeedOperations.Read);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            viewModel = new ResourceNeedViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type,

                // Address, position and date
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

        // List of resources: only those the user is allowed to see - Docs: Resource-based authorization
        public async Task<IActionResult> List()
        {
            var model = new List<ResourceNeedViewModel>();

            foreach (var r in _resourceNeedRepository.GetAll())
            {
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, r, ResourceNeedOperations.Read);
                if (authorizationResult.Succeeded)
                {
                    model.Add(new ResourceNeedViewModel
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        Type = r.Type,
                    });
                }
            }

            return View(model);
        }

        [HttpGet]   //Kart relatert metode for at kartet skal kunne hente behov fra databasen og vise de som markører på kartet.
        public async Task<IActionResult> MapResourceNeeds()
        {
            var resources = new List<object>();

            foreach (var r in _resourceNeedRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, r, ResourceNeedOperations.Read);

                if (authorizationResult.Succeeded &&
                    r.Latitude != null &&
                    r.Longitude != null)
                {
                    resources.Add(new
                    {
                        id = r.Id,
                        name = r.Name,
                        description = r.Description,
                        type = r.Type,
                        latitude = r.Latitude,
                        longitude = r.Longitude
                    });
                }
            }

            return Json(resources);
        }
    }
}
