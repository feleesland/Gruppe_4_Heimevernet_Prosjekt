using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Encodings.Web;

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
        private readonly IResourceRepository _resourceRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        // Times in the form are Norwegian time; they are saved as UTC like CreatedAt - Docs: Converting times between time zones
        // https://learn.microsoft.com/dotnet/standard/datetime/converting-between-time-zones
        private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

        public ResourceNeedHandlerController(
            IResourceNeedRepository resourceNeedRepository,
            IResourceTypeRepository typeRepository,
            IAuthorizationService authorizationService,
            IResourceRequestRepository requestRepository,
            IResourceRepository resourceRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _typeRepository = typeRepository;
            _authorizationService = authorizationService;
            _requestRepository = requestRepository;
            _resourceRepository = resourceRepository;
            _userManager = userManager;
            _emailSender = emailSender;
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

        // "Send forespørsler" on the need page: all ticked resources on all lines, sent in one go (help with AI)
        // Each tick is "lineId:resourceId", so one list can hold choices for several lines
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequests(int needId, List<string> picks, int answerMinutes)
        {
            var need = _resourceNeedRepository.GetWithItems(needId);
            if (need == null)
            {
                return NotFound();
            }

            // Docs: Resource-based authorization
            var canUpdate = await _authorizationService.AuthorizeAsync(User, need, ResourceNeedOperations.Update);
            if (!canUpdate.Succeeded)
            {
                return Forbid();
            }

            // Only lines that belong to this need
            var lineIds = need.Items.Select(item => item.Id).ToHashSet();
            var byLine = picks
                .Select(pick => pick.Split(':'))
                .Where(parts => parts.Length == 2
                                && int.TryParse(parts[0], out var lineId) && lineIds.Contains(lineId)
                                && int.TryParse(parts[1], out _))
                .GroupBy(parts => int.Parse(parts[0]), parts => int.Parse(parts[1]))
                .ToList();

            if (need.ClosedAt != null)
            {
                TempData["Error"] = "Behovet er avsluttet.";
            }
            else if (byLine.Count == 0)
            {
                TempData["Error"] = "Velg minst én ressurs.";
            }
            else
            {
                // A changed form can't choose its own deadline
                if (!AnswerMinutes.Contains(answerMinutes))
                {
                    answerMinutes = need.Urgency == NeedUrgency.Akutt ? 30 : 1440;
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var answerBy = DateTime.UtcNow.AddMinutes(answerMinutes);
                var sentIds = byLine.SelectMany(line => _requestRepository.Create(line.Key, line, userId, answerBy)).ToList();
                await NotifyOwnersAsync(need, sentIds);

                TempData["Message"] = sentIds.Count == 1 ? "1 forespørsel sendt." : $"{sentIds.Count} forespørsler sendt.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // One e-mail per request, with a link to "Mine forespørsler" - Docs: Account confirmation and password recovery (IEmailSender)
        // https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm
        private async Task NotifyOwnersAsync(ResourceNeed need, IEnumerable<int> resourceIds)
        {
            var link = Url.Action("Mine", "ResourceRequest", null, Request.Scheme);
            var when = need.Urgency == NeedUrgency.Akutt ? "Det trengs nå." : "Det er et planlagt behov.";
            var html = HtmlEncoder.Default;

            foreach (var resourceId in resourceIds)
            {
                var resource = _resourceRepository.GetById(resourceId);
                if (resource == null || string.IsNullOrEmpty(resource.OwnerId)) continue;

                var owner = await _userManager.FindByIdAsync(resource.OwnerId);
                if (owner == null || string.IsNullOrEmpty(owner.Email)) continue;

                try
                {
                    await _emailSender.SendEmailAsync(
                        owner.Email,
                        $"Forespørsel: {resource.Name} trengs – {need.Name}",
                        $"<p>Hei!</p>" +
                        $"<p>Heimevernet spør om <strong>{html.Encode(resource.Name)}</strong> kan hjelpe til «{html.Encode(need.Name)}». {when}</p>" +
                        $"<p><a href=\"{link}\">Svar her</a>: ja, jeg leverer / ja, må hentes / nei.</p>");
                }
                catch
                {
                    // The request is saved anyway, and the owner also sees it under 🔔 in the app
                }
            }
        }

        // The buttons on each request sit in the big form on the need page and use formaction,
        // so their value is "requestId:choice" (help with AI) - Docs: formaction (MDN)
        // https://developer.mozilla.org/docs/Web/HTML/Element/button#formaction
        private static readonly int[] ExtendMinutes = { 30, 60 };

        // "Eieren svarte på telefon": ja (leverer / hentes) or nei
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmByPhone(int needId, string phoneAnswer)
        {
            if (!TryParsePair(phoneAnswer, out var requestId, out var choice)
                || choice is not ("leverer" or "hentes" or "nei"))
            {
                return BadRequest();
            }

            var denied = await CheckRequestAsync(needId, requestId);
            if (denied != null)
            {
                return denied;
            }

            var yes = choice != "nei";
            DeliveryMethod? delivery = choice == "hentes" ? DeliveryMethod.Hentes
                : choice == "leverer" ? DeliveryMethod.Leverer
                : null;

            var result = _requestRepository.ConfirmByPhone(requestId, yes, delivery);
            if (result == AnswerResult.Accepted)
            {
                TempData["Message"] = "Registrert: eieren sa ja på telefon.";
            }
            else if (result == AnswerResult.Declined)
            {
                TempData["Message"] = "Registrert: eieren sa nei på telefon.";
            }
            else if (result == AnswerResult.AlreadyCovered)
            {
                TempData["Error"] = "Behovet var allerede dekket. Forespørselen er satt til «Ikke lenger nødvendig».";
            }
            else
            {
                TempData["Error"] = "Forespørselen er ikke åpen lenger.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // "↩ Angre": undo an answer the operator registered by phone (back to "Venter")
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoAnswer(int needId, int undo)
        {
            var denied = await CheckRequestAsync(needId, undo);
            if (denied != null)
            {
                return denied;
            }

            if (_requestRepository.Undo(undo))
            {
                TempData["Message"] = "Angret. Forespørselen venter på svar igjen.";
            }
            else
            {
                TempData["Error"] = "Kan bare angre svar som ble registrert på telefon.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // "Trekk tilbake forespørselen" (waiting) / "Avbryt bekreftelsen" (accepted)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int needId, int cancel)
        {
            var denied = await CheckRequestAsync(needId, cancel);
            if (denied != null)
            {
                return denied;
            }

            if (_requestRepository.Cancel(cancel))
            {
                TempData["Message"] = "Forespørselen er avbrutt.";
            }
            else
            {
                TempData["Error"] = "Forespørselen kan ikke avbrytes lenger.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // "Marker som levert": the resource has arrived
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(int needId, int delivered)
        {
            var denied = await CheckRequestAsync(needId, delivered);
            if (denied != null)
            {
                return denied;
            }

            if (_requestRepository.MarkDelivered(delivered))
            {
                TempData["Message"] = "Markert som levert.";
            }
            else
            {
                TempData["Error"] = "Kan bare markere godtatte forespørsler som levert.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // "Marker som levert tilbake": the resource is back with the owner and becomes free again
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReturned(int needId, int returned)
        {
            var denied = await CheckRequestAsync(needId, returned);
            if (denied != null)
            {
                return denied;
            }

            if (_requestRepository.MarkReturned(returned))
            {
                TempData["Message"] = "Markert som levert tilbake. Ressursen er ledig igjen.";
            }
            else
            {
                TempData["Error"] = "Ressursen er allerede levert tilbake.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // "+30 min / +1 t": more time to answer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtendDeadline(int needId, string extend)
        {
            if (!TryParsePair(extend, out var requestId, out var choice)
                || !int.TryParse(choice, out var minutes) || !ExtendMinutes.Contains(minutes))
            {
                return BadRequest();
            }

            var denied = await CheckRequestAsync(needId, requestId);
            if (denied != null)
            {
                return denied;
            }

            if (_requestRepository.Extend(requestId, minutes))
            {
                TempData["Message"] = minutes == 60 ? "Fristen er forlenget med 1 time." : "Fristen er forlenget med 30 min.";
            }
            else
            {
                TempData["Error"] = "Forespørselen venter ikke på svar lenger.";
            }

            return RedirectToAction("Index", "ResourceNeed", new { id = needId });
        }

        // The request must belong to this need, and the user must be allowed to change the need
        private async Task<IActionResult?> CheckRequestAsync(int needId, int requestId)
        {
            if (_requestRepository.GetNeedId(requestId) != needId)
            {
                return NotFound();
            }
            var need = _resourceNeedRepository.GetById(needId);
            if (need == null)
            {
                return NotFound();
            }
            var canUpdate = await _authorizationService.AuthorizeAsync(User, need, ResourceNeedOperations.Update);
            return canUpdate.Succeeded ? null : Forbid();
        }

        // "12:leverer" → 12 and "leverer"
        private static bool TryParsePair(string? value, out int id, out string rest)
        {
            id = 0;
            rest = string.Empty;
            var parts = value?.Split(':');
            if (parts is not { Length: 2 } || !int.TryParse(parts[0], out id))
            {
                return false;
            }
            rest = parts[1];
            return true;
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