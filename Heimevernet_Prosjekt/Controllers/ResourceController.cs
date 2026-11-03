using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Heimevernet_Prosjekt.Models.Authorization;


namespace Heimevernet_Prosjekt.Controllers
{
    // Must be logged in; who can see what is checked by ResourceAuthorizationHandler - Docs: Introduction to authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/introduction
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;
        private readonly IAuthorizationService _authorizationService;

        public ResourceController(IResourceRepository resourceRepository, IAuthorizationService authorizationService)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var resource = id.HasValue ? _resourceRepository.GetById(id.Value) : null;
            var viewModel = new ResourceViewModel();
            if (resource == null)
            {
                return View(viewModel);
            }

            // Owner, admin and operator can see the resource - Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Read);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            viewModel = new ResourceViewModel
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
            var model = new List<ResourceViewModel>();

            foreach (var r in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, r, ResourceOperations.Read);
                if (authorizationResult.Succeeded)
                {
                    model.Add(new ResourceViewModel
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        Type = r.Type
                    });
                }
            }

            return View(model);
        }
    }
}