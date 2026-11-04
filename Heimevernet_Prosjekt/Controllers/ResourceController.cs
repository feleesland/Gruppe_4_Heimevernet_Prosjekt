using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Identity;


namespace Heimevernet_Prosjekt.Controllers
{
    // Must be logged in; who can see what is checked by ResourceAuthorizationHandler - Docs: Introduction to authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/introduction
    [Authorize]
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourceController(IResourceRepository resourceRepository, IAuthorizationService authorizationService, UserManager<ApplicationUser> userManager)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
            _userManager = userManager;
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
                Notes = resource.Notes,
                HasImage = resource.ImageData != null,

                // Address, position and date
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

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Read);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            return File(resource.ImageData, resource.ImageContentType);
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
                    // Same checks as in ResourceHandlerController, so the buttons only show when they work
                    var canEdit = await _authorizationService.AuthorizeAsync(User, r, ResourceOperations.Update);
                    var canDelete = await _authorizationService.AuthorizeAsync(User, r, ResourceOperations.Delete);

                    model.Add(new ResourceViewModel
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        Type = r.Type,
                        CanEdit = canEdit.Succeeded,
                        CanDelete = canDelete.Succeeded
                    });
                }
            }

            return View(model);
        }

        [HttpGet]   //Kart relatert metode for at kartet skal kunne hente ressurser fra databasen og vise de som markører på kartet.
        public async Task<IActionResult> MapResources()
        {
            var resources = new List<object>();

            foreach (var r in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, r, ResourceOperations.Read);

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