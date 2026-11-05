using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers
{
    // Needs are the operators' work: only admin and operator - Docs: Role-based authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/roles
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Operator)]
    public class ResourceNeedController : Controller
    {
        private readonly IResourceNeedRepository _resourceNeedRepository;
        private readonly IAuthorizationService _authorizationService;

        public ResourceNeedController(IResourceNeedRepository resourceNeedRepository, IAuthorizationService authorizationService)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _authorizationService = authorizationService;
        }

        // The need's page, with its lines
        public async Task<IActionResult> Index(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction(nameof(List));
            }

            var need = _resourceNeedRepository.GetWithItems(id.Value);
            if (need == null)
            {
                return NotFound();
            }

            // Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, need, ResourceNeedOperations.Read);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            var viewModel = new ResourceNeedViewModel
            {
                Id = need.Id,
                Name = need.Name,
                Description = need.Description,
                Type = need.Type,

                // Address, position and date
                Address = need.Address,
                ZipCode = need.ZipCode,
                City = need.City,
                Latitude = need.Latitude,
                Longitude = need.Longitude,
                CreatedAt = need.CreatedAt,
                OwnerRemoved = need.OwnerRemoved,

                // Akutt/Planlagt, time, radius and the lines
                Urgency = need.Urgency,
                RadiusKm = need.RadiusKm,
                NeededFrom = need.NeededFrom,
                NeededTo = need.NeededTo,
                ClosedAt = need.ClosedAt,
                Items = need.Items.OrderBy(item => item.CreatedAt).ToList()
            };
            return View(viewModel);
        }

        // List of needs: only those the user is allowed to see - Docs: Resource-based authorization
        public async Task<IActionResult> List()
        {
            var model = new List<ResourceNeedViewModel>();

            // Active first, newest first
            foreach (var r in _resourceNeedRepository.GetAll()
                         .OrderBy(r => r.ClosedAt != null)
                         .ThenByDescending(r => r.CreatedAt))
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
                        Urgency = r.Urgency,
                        NeededFrom = r.NeededFrom,
                        ClosedAt = r.ClosedAt,
                        Items = r.Items.ToList()
                    });
                }
            }

            return View(model);
        }

        // The main map: active needs as circles with how many things are needed - Docs: Return JSON from a controller action
        // https://learn.microsoft.com/aspnet/core/web-api/action-return-types
        [HttpGet]
        public async Task<IActionResult> MapResourceNeeds()
        {
            var needs = new List<object>();

            foreach (var r in _resourceNeedRepository.GetAll().Where(r => r.ClosedAt == null))
            {
                var authorizationResult = await _authorizationService
                    .AuthorizeAsync(User, r, ResourceNeedOperations.Read);

                if (authorizationResult.Succeeded && r.Latitude != null && r.Longitude != null)
                {
                    needs.Add(new
                    {
                        id = r.Id,
                        name = r.Name,
                        urgency = r.Urgency.ToString(),
                        radiusKm = r.RadiusKm,
                        neededFrom = r.NeededFrom,
                        latitude = r.Latitude,
                        longitude = r.Longitude,
                        count = r.Items.Sum(item => item.Quantity),
                        items = r.Items.Select(item => new { type = item.Type, quantity = item.Quantity })
                    });
                }
            }

            return Json(needs);
        }
    }
}