using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers
{
    // Users must log in before accessing these pages.
    // Who can see what is checked by ResourceAuthorizationHandler - Docs: Introduction to authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/introduction
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourceController(
            IResourceRepository resourceRepository,
            IAuthorizationService authorizationService,
            UserManager<ApplicationUser> userManager)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
            _userManager = userManager;
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
            // Docs: Resource-based authorization - https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
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
                // Notes and picture from the owner
                Notes = resource.Notes,
                HasImage = resource.ImageData != null,
                Address = resource.Address,
                ZipCode = resource.ZipCode,
                City = resource.City,
                Latitude = resource.Latitude,
                Longitude = resource.Longitude,
                CreatedAt = resource.CreatedAt,
                OwnerRemoved = resource.OwnerRemoved
            };

            // Owner's name, email and phone come from the account, so they are always up to date - Docs: Introduction to Identity on ASP.NET Core
            // https://learn.microsoft.com/aspnet/core/security/authentication/identity
            if (!string.IsNullOrEmpty(resource.OwnerId))
            {
                var owner = await _userManager.FindByIdAsync(resource.OwnerId);
                if (owner != null)
                {
                    viewModel.OwnerName = $"{owner.FirstName} {owner.LastName}".Trim();
                    viewModel.OwnerEmail = owner.Email;
                    viewModel.OwnerPhone = owner.PhoneNumber;
                }
            }

            return View(viewModel);
        }

        // Returns the resource's picture from the database, same access as the detail page - Docs: Upload files in ASP.NET Core
        // https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads
        [HttpGet]
        public async Task<IActionResult> Image(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null || resource.ImageData == null || resource.ImageContentType == null)
            {
                return NotFound();
            }

            var authorizationResult = await _authorizationService
                .AuthorizeAsync(User, resource, ResourceOperations.Read);

            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            return File(resource.ImageData, resource.ImageContentType);
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
                    // Same checks as in ResourceHandlerController, so Rediger/Slett only show when they work
                    var canEdit = await _authorizationService
                        .AuthorizeAsync(User, resource, ResourceOperations.Update);
                    var canDelete = await _authorizationService
                        .AuthorizeAsync(User, resource, ResourceOperations.Delete);

                    model.Add(new ResourceViewModel
                    {
                        Id = resource.Id,
                        Name = resource.Name,
                        Description = resource.Description,
                        Type = resource.Type,
                        CanEdit = canEdit.Succeeded,
                        CanDelete = canDelete.Succeeded
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