using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        private readonly IResourceTypeRepository _typeRepository;

        public ResourceController(
            IResourceRepository resourceRepository,
            IAuthorizationService authorizationService,
            UserManager<ApplicationUser> userManager,
            IResourceTypeRepository typeRepository)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
            _userManager = userManager;
            _typeRepository = typeRepository;
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

            // Same checks as in ResourceHandlerController, so Rediger/Slett only show on the page when they work
            var canEdit = await _authorizationService
                .AuthorizeAsync(User, resource, ResourceOperations.Update);
            var canDelete = await _authorizationService
                .AuthorizeAsync(User, resource, ResourceOperations.Delete);

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
                OwnerRemoved = resource.OwnerRemoved,
                CanEdit = canEdit.Succeeded,
                CanDelete = canDelete.Succeeded
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
        // search/type/lat/lng/radius come from the search and filter in the menu, mine from "Alle | Mine" (all optional)
        public async Task<IActionResult> List(string? search = null, string? type = null, double? lat = null, double? lng = null, double? radius = null, bool mine = false)
        {
            var model = new List<ResourceViewModel>();
            var ownerId = mine ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            // Include only resources the user is allowed to view.
            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                if (authorizationResult.Succeeded && PassesFilter(resource, search, type, lat, lng, radius, ownerId))
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
                        Status = resource.Status,
                        CanEdit = canEdit.Succeeded,
                        CanDelete = canDelete.Succeeded
                    });
                }
            }

            return View(model);
        }

        [HttpGet]
        // Same search and filter as the list, so the map and the list show the same resources
        public async Task<IActionResult> MapResources(string? search = null, string? type = null, double? lat = null, double? lng = null, double? radius = null, bool mine = false)
        {
            var resources = new List<object>();
            var ownerId = mine ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                // Map markers need permission and both coordinates.
                if (authorizationResult.Succeeded &&
                    resource.Latitude != null &&
                    resource.Longitude != null &&
                    PassesFilter(resource, search, type, lat, lng, radius, ownerId))
                {
                    resources.Add(new
                    {
                        id = resource.Id,
                        name = resource.Name,
                        description = resource.Description,
                        type = resource.Type,
                        status = resource.Status,
                        latitude = resource.Latitude,
                        longitude = resource.Longitude
                    });
                }
            }

            return Json(resources);
        }

        // The types for the Type dropdown in the filter: only the types in the list (ResourceTypes),
        // so old or deleted types never show up. Same order as in Opprett/Rediger ("Annet" last)
        [HttpGet]
        public IActionResult Types()
        {
            return Json(_typeRepository.GetAll().Select(t => t.Name));
        }

        // Search, type, radius and "only mine" shared by the list and the map (help with AI)
        private static bool PassesFilter(Resource r, string? search, string? type, double? lat, double? lng, double? radius, string? ownerId)
        {
            if (ownerId != null && r.OwnerId != ownerId)
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(type) &&
                !string.Equals(r.Type?.Trim(), type.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string?[] fields = [r.Name, r.Type, r.Description, r.Address, r.ZipCode, r.City];
                if (!fields.Any(f => f != null && f.Contains(search, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
            if (lat != null && lng != null && radius != null)
            {
                if (r.Latitude == null || r.Longitude == null)
                {
                    return false;
                }
                if (DistanceKm(lat.Value, lng.Value, r.Latitude.Value, r.Longitude.Value) > radius.Value)
                {
                    return false;
                }
            }
            return true;
        }

        // Distance in km between two points on the earth (Haversine formula) (help with AI)
        private static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double EarthRadiusKm = 6371;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}