using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Heimevernet_Prosjekt.Security.Authorization;
using Heimevernet_Prosjekt.Services;
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
        private readonly IResourceRequestRepository _requestRepository;
        private readonly IResourceReportRepository _reportRepository;

        public ResourceController(
            IResourceRepository resourceRepository,
            IAuthorizationService authorizationService,
            UserManager<ApplicationUser> userManager,
            IResourceTypeRepository typeRepository,
            IResourceRequestRepository requestRepository,
            IResourceReportRepository reportRepository)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
            _userManager = userManager;
            _typeRepository = typeRepository;
            _requestRepository = requestRepository;
            _reportRepository = reportRepository;
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
                CanDelete = canDelete.Succeeded,
                Status = resource.Status,
                UnavailableReason = resource.UnavailableReason,
                UnavailableUntil = resource.UnavailableUntil,
                // "Når er den tilgjengelig?", and "Ledig fra 16:00" when a free resource is outside its times now
                AvailabilityKind = resource.AvailabilityKind,
                AvailableFromTime = resource.AvailableFromTime,
                AvailableToTime = resource.AvailableToTime,
                AvailableDayBits = Enumerable.Range(0, 7).Select(i => 1 << i).Where(bit => (resource.AvailableDays & bit) != 0).ToList(),
                AvailableFromDate = resource.AvailableFromDate,
                AvailableToDate = resource.AvailableToDate,
                FreeFrom = ShownStatus(resource, _requestRepository.GetResourceIdsInUse()).FreeFrom,
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
            var inUse = _requestRepository.GetResourceIdsInUse();
            // For the table (help with AI): the type's icon and category, and (admin/operator) the owner's name and phone for print/export
            var typeInfo = _typeRepository.GetCategories()
                .SelectMany(category => category.Types.Select(t => (t.Name, Icon: t.Icon ?? category.Icon, Category: category.Name)))
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, g => g.First());
            var seeOwners = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Operator);
            var owners = seeOwners
                ? _userManager.Users.ToDictionary(u => u.Id, u => (Name: $"{u.FirstName} {u.LastName}".Trim(), Phone: u.PhoneNumber))
                : new Dictionary<string, (string Name, string? Phone)>();

            // Include only resources the user is allowed to view.
            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                // Distance is filtered in resource-filter.js, so the filter can count hits for each distance (help with AI)
                if (authorizationResult.Succeeded && PassesFilter(resource, search, type, null, null, null, ownerId))
                {
                    // Same checks as in ResourceHandlerController, so Rediger/Slett only show when they work
                    var canEdit = await _authorizationService
                        .AuthorizeAsync(User, resource, ResourceOperations.Update);
                    var canDelete = await _authorizationService
                        .AuthorizeAsync(User, resource, ResourceOperations.Delete);

                    var shown = ShownStatus(resource, inUse);
                    typeInfo.TryGetValue(resource.Type ?? "", out var info);
                    owners.TryGetValue(resource.OwnerId ?? "", out var owner);

                    model.Add(new ResourceViewModel
                    {
                        Id = resource.Id,
                        Name = resource.Name,
                        Description = resource.Description,
                        Type = resource.Type ?? string.Empty,
                        Status = shown.Status, // "I bruk" / "Lever tilbake" (see ShownStatus)
                        FreeFrom = shown.FreeFrom,
                        UsedByNeedName = shown.NeedName,
                        Icon = info.Icon,
                        Category = info.Category,
                        OwnerName = owner.Name,
                        OwnerPhone = owner.Phone,
                        Municipality = resource.Municipality,
                        Latitude = resource.Latitude,
                        Longitude = resource.Longitude,
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
            // The type's icon on the map (the same as in "Ny ressurs"), or the category's icon if the type has none
            var icons = _typeRepository.GetCategories()
                .SelectMany(category => category.Types.Select(t => (t.Name, Icon: t.Icon ?? category.Icon)))
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, g => g.First().Icon);
            var inUse = _requestRepository.GetResourceIdsInUse();
            // Admin and operator: open "feil info" messages per resource, for the ⚑ filter on the map (others get 0) (help with AI)
            var openReports = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Operator)
                ? _reportRepository.CountOpenByResource()
                : new Dictionary<int, int>();

            foreach (var resource in _resourceRepository.GetAll())
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, resource, ResourceOperations.Read);

                // Map markers need permission and both coordinates.
                if (authorizationResult.Succeeded &&
                    resource.Latitude != null &&
                    resource.Longitude != null &&
                    // Distance is filtered in resource-filter.js, so the filter can count hits for each distance (help with AI)
                    PassesFilter(resource, search, type, null, null, null, ownerId))
                {
                    var shown = ShownStatus(resource, inUse);
                    resources.Add(new
                    {
                        id = resource.Id,
                        name = resource.Name,
                        description = resource.Description,
                        type = resource.Type,
                        status = shown.Status, // "I bruk" / "Lever tilbake" (see ShownStatus)
                        usedByNeedId = shown.NeedId,
                        usedByNeedName = shown.NeedName,
                        freeFrom = shown.FreeFrom,
                        icon = icons.GetValueOrDefault(resource.Type ?? ""),
                        latitude = resource.Latitude,
                        longitude = resource.Longitude,
                        municipality = resource.Municipality,
                        reports = openReports.GetValueOrDefault(resource.Id)
                    });
                }
            }

            return Json(resources);
        }

        // The types for "Type ressurs" in the filter, grouped by category in the same order as in "Ny ressurs".
        // Only types with a category, so old or removed types (e.g. Lokaler) never show up
        [HttpGet]
        public IActionResult Types()
        {
            return Json(_typeRepository.GetCategories()
                .Where(category => category.Types.Count > 0)
                .Select(category => new
                {
                    name = category.Name,
                    icon = category.Icon,
                    types = category.Types.Select(type => type.Name)
                }));
        }

        // The status people see on the map and in the list (help with AI): lent out to a need and not returned = "I bruk",
        // or "Lever tilbake" if that need is closed. "Opptatt" is shown as "I bruk". Outside its fixed times or period a free
        // resource is "Ledig senere", with FreeFrom = "Ledig fra 16:00". The database still stores "Opptatt" and "Ledig"
        private (string Status, int? NeedId, string? NeedName, string? FreeFrom) ShownStatus(Resource resource, IReadOnlySet<int> inUse)
        {
            if (inUse.Contains(resource.Id))
            {
                var loan = _requestRepository.GetLoansForResource(resource.Id).FirstOrDefault(l => l.ReturnedAt == null);
                if (loan != null)
                {
                    return (loan.NeedClosed ? "Lever tilbake" : "I bruk", loan.NeedId, loan.NeedName, null);
                }
            }
            var status = string.IsNullOrEmpty(resource.Status) ? "Ledig" : resource.Status;
            if (status == "Ledig" && !ResourceAvailabilityService.IsAvailableAt(resource, DateTime.UtcNow))
            {
                var next = ResourceAvailabilityService.NextAvailableText(resource, DateTime.UtcNow);
                return ("Ledig senere", null, null, next == null ? "Ikke ledig nå" : "Ledig fra " + next);
            }
            return (status == "Opptatt" ? "I bruk" : status, null, null, null);
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