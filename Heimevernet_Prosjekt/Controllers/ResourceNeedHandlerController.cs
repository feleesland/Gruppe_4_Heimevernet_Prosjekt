using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Heimevernet_Prosjekt.Controllers
{
    // Needs are the operators' work: only admin and operator - Docs: Role-based authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/roles
    // [ValidateAntiForgeryToken] on all POST methods - Docs: Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks
    // https://learn.microsoft.com/aspnet/core/security/anti-request-forgery
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Operator)]
    public class ResourceNeedHandlerController : Controller
    {
        private readonly IResourceNeedRepository _resourceNeedRepository;
        private readonly IResourceTypeRepository _typeRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IResourceRequestRepository _requestRepository;

        // Times in the form are Norwegian time; they are saved as UTC like CreatedAt - Docs: Converting times between time zones
        // https://learn.microsoft.com/dotnet/standard/datetime/converting-between-time-zones
        private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

        public ResourceNeedHandlerController(
            IResourceNeedRepository resourceNeedRepository,
            IResourceTypeRepository typeRepository,
            IAuthorizationService authorizationService,
            IResourceRequestRepository requestRepository)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _typeRepository = typeRepository;
            _authorizationService = authorizationService;
            _requestRepository = requestRepository;
        }

        // The list is on the Behov page
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("List", "ResourceNeed");
        }

        // "Meld behov". From the map, place and radius come in the address (?lat=..&lng=..&radius=..)
        [HttpGet]
        public IActionResult Create(double? lat, double? lng, double? radius)
        {
            LoadTypes();
            var model = new ReportNeedViewModel
            {
                Latitude = lat,
                Longitude = lng,
                RadiusKm = radius is > 0 ? radius.Value : 2
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReportNeedViewModel model)
        {
            ValidateNeed(model);

            // "Legg til i ...": the need must exist, be active and the user must be allowed to change it
            ResourceNeed? existing = null;
            if (model.ExistingNeedId != null)
            {
                existing = _resourceNeedRepository.GetById(model.ExistingNeedId.Value);
                if (existing == null || existing.ClosedAt != null)
                {
                    ModelState.AddModelError(string.Empty, "Behovet finnes ikke lenger eller er avsluttet. Velg «Nytt behov».");
                }
                else
                {
                    var canUpdate = await _authorizationService.AuthorizeAsync(User, existing, ResourceNeedOperations.Update);
                    if (!canUpdate.Succeeded)
                    {
                        return Forbid();
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                LoadTypes();
                return View(model);
            }

            if (existing != null)
            {
                _resourceNeedRepository.AddItems(existing.Id, model.Lines);
                TempData["Message"] = $"{CountText(model.Lines)} lagt til i «{existing.Name}»: {Summary(model.Lines)}.";
                return RedirectToAction("Index", "ResourceNeed", new { id = existing.Id });
            }

            // New need: times to UTC, Akutt has no "from" (it is now)
            if (model.Urgency == NeedUrgency.Akutt)
            {
                model.NeededFrom = null;
            }
            model.NeededFrom = ToUtc(model.NeededFrom);
            model.NeededTo = ToUtc(model.NeededTo);

            var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var need = _resourceNeedRepository.CreateWithItems(model, ownerId);

            TempData["Message"] = $"«{need.Name}» er meldt med {CountText(model.Lines)}: {Summary(model.Lines)}.";
            return RedirectToAction("Index", "ResourceNeed", new { id = need.Id });
        }

        // Asked by the form when a place is chosen: active needs whose circle contains the point (for the orange box)
        // Docs: Return JSON from a controller action - https://learn.microsoft.com/aspnet/core/web-api/action-return-types
        [HttpGet]
        public IActionResult ActiveAt(double lat, double lng)
        {
            var needs = _resourceNeedRepository.GetActiveAt(lat, lng)
                .Select(need => new
                {
                    id = need.Id,
                    name = need.Name,
                    urgency = need.Urgency.ToString(),
                    distanceKm = Math.Round(GeoDistance.Km(lat, lng, need.Latitude!.Value, need.Longitude!.Value), 1),
                    items = need.Items.Select(item => new { type = item.Type, quantity = item.Quantity })
                });
            return Json(needs);
        }

        // The deadlines the form can choose (minutes): 30 min, 1 h, 3 h, 1 day, 2 days
        private static readonly int[] AnswerMinutes = { 30, 60, 180, 1440, 2880 };

        // "Send forespørsler" on the need page: one request per chosen resource for one line
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequests(int itemId, List<int> resourceIds, int answerMinutes)
        {
            var item = _requestRepository.GetItem(itemId);
            if (item == null)
            {
                return NotFound();
            }

            // Docs: Resource-based authorization
            var canUpdate = await _authorizationService.AuthorizeAsync(User, item.ResourceNeed, ResourceNeedOperations.Update);
            if (!canUpdate.Succeeded)
            {
                return Forbid();
            }

            if (item.ResourceNeed.ClosedAt != null)
            {
                TempData["Error"] = "Behovet er avsluttet.";
            }
            else if (resourceIds.Count == 0)
            {
                TempData["Error"] = "Velg minst én ressurs.";
            }
            else
            {
                // A changed form can't choose its own deadline
                if (!AnswerMinutes.Contains(answerMinutes))
                {
                    answerMinutes = item.ResourceNeed.Urgency == NeedUrgency.Akutt ? 30 : 1440;
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var sent = _requestRepository.Create(item.Id, resourceIds, userId, DateTime.UtcNow.AddMinutes(answerMinutes));

                TempData["Message"] = sent == 1
                    ? $"1 forespørsel sendt for {item.Quantity} × {item.Type}."
                    : $"{sent} forespørsler sendt for {item.Quantity} × {item.Type}.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = item.ResourceNeedId });
        }

        [HttpGet]
        public async Task<ActionResult> Edit(int id)
        {
            var resource = _resourceNeedRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            // Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceNeedOperations.Update);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var model = new ResourceNeedViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type,

                // Address and position on the map
                Address = resource.Address,
                City = resource.City,
                ZipCode = resource.ZipCode,
                Latitude = resource.Latitude,
                Longitude = resource.Longitude,
                CreatedAt = resource.CreatedAt
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ResourceNeedViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (model.Id == null)
            {
                return BadRequest();
            }
            var existing = _resourceNeedRepository.GetById(model.Id.Value);
            if (existing == null)
            {
                return NotFound();
            }
            // Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, existing, ResourceNeedOperations.Update);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var success = _resourceNeedRepository.Update(model.Id.Value, model);

            if (!success)
            {
                return NotFound();
            }
            return RedirectToAction("List", "ResourceNeed");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            var resource = _resourceNeedRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            // Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceNeedOperations.Delete);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var success = _resourceNeedRepository.Delete(id);
            if (!success)
            {
                return NotFound();
            }

            // Back to the Behov list (it went to the resource list before)
            return RedirectToAction("List", "ResourceNeed");
        }

        // Rules that depend on each other, so they can't be attributes on the ViewModel - Docs: Model validation
        // https://learn.microsoft.com/aspnet/core/mvc/models/validation
        private void ValidateNeed(ReportNeedViewModel model)
        {
            if (model.Lines.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Legg til minst én ting som trengs.");
            }

            for (var i = 0; i < model.Lines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(model.Lines[i].Type) && !_typeRepository.Exists(model.Lines[i].Type))
                {
                    ModelState.AddModelError($"Lines[{i}].Type", "Velg en type fra listen.");
                }
            }

            // Adding to an existing need: name, place and time come from that need
            if (model.ExistingNeedId != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "Skriv inn et navn.");
            }
            if (model.Latitude == null || model.Longitude == null)
            {
                ModelState.AddModelError(string.Empty, "Velg et sted: klikk i kartet, søk en adresse eller bruk «Bruk min posisjon».");
            }
            if (model.Urgency == NeedUrgency.Planlagt && model.NeededFrom == null)
            {
                ModelState.AddModelError(nameof(model.NeededFrom), "Velg når det trengs fra.");
            }
            if (model.NeededFrom != null && model.NeededTo != null && model.NeededTo < model.NeededFrom)
            {
                ModelState.AddModelError(nameof(model.NeededTo), "«Til» må være etter «Fra».");
            }
        }

        // The types for the dropdowns and chips, same list as resources ("Annet" last)
        private void LoadTypes()
        {
            ViewData["ResourceTypes"] = _typeRepository.GetAll();
        }

        private static DateTime? ToUtc(DateTime? norwegianTime)
        {
            return norwegianTime == null
                ? null
                : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(norwegianTime.Value, DateTimeKind.Unspecified), Oslo);
        }

        // "1 behov" / "3 behov" (same word in singular and plural)
        private static string CountText(List<NeedLineInput> lines)
        {
            return $"{lines.Count} behov";
        }

        // "1 × Husly og bygg, 2 × Motoriserte kjøretøy" for the green message
        private static string Summary(List<NeedLineInput> lines)
        {
            return string.Join(", ", lines.Select(line => $"{line.Quantity} × {line.Type}"));
        }
    }
}