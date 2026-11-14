using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Heimevernet_Prosjekt.Security.Authorization;
using Heimevernet_Prosjekt.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers
{
    // Needs are the operators' work: only admin and operator - Docs: Role-based authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/roles
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.Operator)]
    public class ResourceNeedController : Controller
    {
        private readonly IResourceNeedRepository _resourceNeedRepository;
        private readonly IResourceRequestRepository _requestRepository;
        private readonly IResourceRepository _resourceRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorizationService;

        public ResourceNeedController(
            IResourceNeedRepository resourceNeedRepository,
            IResourceRequestRepository requestRepository,
            IResourceRepository resourceRepository,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorizationService)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _requestRepository = requestRepository;
            _resourceRepository = resourceRepository;
            _userManager = userManager;
            _authorizationService = authorizationService;
        }

        // "Oversikt": the operator's main page. Built from needs and their requests (help with AI)
        [HttpGet]
        public IActionResult Overview()
        {
            var now = DateTime.UtcNow;
            var model = new OverviewViewModel();
            var oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
            string Time(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, oslo).ToString("dd.MM HH:mm");
            string ResourceName(int id) => _resourceRepository.GetById(id)?.Name ?? $"Ressurs #{id}";
            // One row for the "Ute nå" list: resource, owner and need
            OverviewLoanRow Loan(int needId, string needName, bool needClosed, ResourceRequest r)
            {
                var resource = _resourceRepository.GetById(r.ResourceId);
                var owner = string.IsNullOrEmpty(resource?.OwnerId) ? null : _userManager.Users.FirstOrDefault(u => u.Id == resource.OwnerId);
                var ownerName = owner == null ? "Ukjent eier" : $"{owner.FirstName} {owner.LastName}".Trim();
                return new OverviewLoanRow
                {
                    NeedId = needId,
                    NeedName = needName,
                    NeedClosed = needClosed,
                    ResourceId = r.ResourceId,
                    ResourceName = resource?.Name ?? $"Ressurs #{r.ResourceId}",
                    OwnerName = ownerName.Length > 0 ? ownerName : owner?.Email ?? "Ukjent eier",
                    OwnerPhone = owner?.PhoneNumber,
                    DeliveredAt = r.DeliveredAt,
                    ReturnedAt = r.ReturnedAt
                };
            }

            foreach (var need in _resourceNeedRepository.GetAll())
            {
                var requests = _requestRepository.GetForNeed(need.Id);
                // Everything that has been delivered: still out, or returned
                foreach (var r in requests.Where(r => r.Status == RequestStatus.Godtatt && r.DeliveredAt != null))
                {
                    var row = Loan(need.Id, need.Name, need.ClosedAt != null, r);
                    if (r.ReturnedAt == null) model.OutLoans.Add(row);
                    else model.ReturnedLoans.Add(row);
                }

                // Resources that are out right now - also on closed needs, so nothing is forgotten
                foreach (var r in requests.Where(r => r.Status == RequestStatus.Godtatt && r.DeliveredAt != null && r.ReturnedAt == null))
                {
                    model.OutNow++;
                    if (need.ClosedAt != null)
                    {
                        model.Attention.Add(new AttentionItem(need.Id, "📦",
                            $"«{ResourceName(r.ResourceId)}» er fortsatt ute. «{need.Name}» ble avsluttet {Time(need.ClosedAt.Value)}.", need.ClosedAt));
                    }
                }

                if (need.ClosedAt != null)
                {
                    continue;
                }

                var waiting = requests.Count(r => r.Status == RequestStatus.Sendt && !r.IsUnanswered(now));
                var overdue = requests.Where(r => r.IsUnanswered(now)).ToList();

                foreach (var r in requests.Where(r => r.Status == RequestStatus.Sendt && !r.IsUnanswered(now)))
                {
                    model.WaitingList.Add(new AttentionItem(need.Id, "⏳",
                        $"«{ResourceName(r.ResourceId)}» · «{need.Name}» – svar innen {Time(r.AnswerBy)}", r.AnswerBy));
                }
                foreach (var r in overdue)
                {
                    model.OverdueList.Add(new AttentionItem(need.Id, "⏱",
                        $"«{ResourceName(r.ResourceId)}» · «{need.Name}» – fristen gikk {Time(r.AnswerBy)}", r.AnswerBy));
                }

                // Covered per line, never more than the line asks for
                var covered = need.Items.Sum(item => Math.Min(item.Quantity,
                    requests.Count(r => r.ResourceNeedItemId == item.Id && r.Status == RequestStatus.Godtatt)));
                var quantity = need.Items.Sum(item => item.Quantity);

                model.ActiveNeeds++;
                if (need.Urgency == NeedUrgency.Akutt) model.ActiveAkutt++;
                model.Waiting += waiting;
                model.Overdue += overdue.Count;

                foreach (var r in overdue)
                {
                    model.Attention.Add(new AttentionItem(need.Id, "⏱",
                        $"«{ResourceName(r.ResourceId)}» har ikke svart på «{need.Name}». Fristen gikk {Time(r.AnswerBy)}.", r.AnswerBy));
                }

                // Not covered, and nobody is being asked: the operator must send more requests
                if (covered < quantity && waiting == 0)
                {
                    model.Attention.Add(new AttentionItem(need.Id, need.Urgency == NeedUrgency.Akutt ? "⏰" : "📅",
                        $"«{need.Name}»: {covered} av {quantity} dekket, ingen venter på svar.", need.CreatedAt));
                }

                model.Needs.Add(new OverviewNeedRow
                {
                    Id = need.Id,
                    Name = need.Name,
                    Urgency = need.Urgency,
                    NeededFrom = need.NeededFrom,
                    Covered = covered,
                    Quantity = quantity,
                    Waiting = waiting
                });
            }

            // Akutt first, then the oldest
            model.Needs = model.Needs.OrderBy(n => n.Urgency).ThenBy(n => n.Id).ToList();
            model.Attention = model.Attention.OrderBy(a => a.When).ToList();
            model.OutLoans = model.OutLoans.OrderBy(l => l.DeliveredAt).ToList();
            model.ReturnedLoans = model.ReturnedLoans.OrderByDescending(l => l.ReturnedAt).Take(20).ToList();
            model.WaitingList = model.WaitingList.OrderBy(a => a.When).ToList();
            return View(model);
        }

        // The need's page, with its lines, requests and suggestions
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
                Status = NeedStatusRules.For(need, _requestRepository.GetForNeed(need.Id)),
                Items = need.Items.OrderBy(item => item.CreatedAt).ToList()
            };
            ViewData["Log"] = _resourceNeedRepository.GetLog(id.Value);
            ViewData["Updates"] = _resourceNeedRepository.GetUpdates(id.Value); // "Info til innbyggere"
            viewModel.Lines = await BuildLinesAsync(need, viewModel.Items);
            return View(viewModel);
        }

        // Each line with its requests and suggestions: free resources of the same type, nearest first (help with AI)
        private async Task<List<NeedLineViewModel>> BuildLinesAsync(ResourceNeed need, List<ResourceNeedItem> items)
        {
            var now = DateTime.UtcNow;
            var requests = _requestRepository.GetForNeed(need.Id);
            var inUse = _requestRepository.GetResourceIdsInUse();
            var resources = _resourceRepository.GetAll();
            var resourcesById = resources.ToDictionary(resource => resource.Id);

            // Owners are looked up once each - Docs: Introduction to Identity on ASP.NET Core
            var owners = new Dictionary<string, ApplicationUser?>();
            async Task<ApplicationUser?> OwnerAsync(string? ownerId)
            {
                if (string.IsNullOrEmpty(ownerId)) return null;
                if (!owners.TryGetValue(ownerId, out var owner))
                {
                    owner = await _userManager.FindByIdAsync(ownerId);
                    owners[ownerId] = owner;
                }
                return owner;
            }
            static string OwnerName(ApplicationUser? owner) =>
                owner == null ? "Ukjent eier"
                : $"{owner.FirstName} {owner.LastName}".Trim() is { Length: > 0 } name ? name : owner.Email ?? "Ukjent eier";

            var lines = new List<NeedLineViewModel>();
            foreach (var item in items)
            {
                var itemRequests = requests.Where(request => request.ResourceNeedItemId == item.Id).ToList();
                var line = new NeedLineViewModel
                {
                    ItemId = item.Id,
                    Type = item.Type,
                    Quantity = item.Quantity,
                    Accepted = itemRequests.Count(request => request.Status == RequestStatus.Godtatt)
                };

                foreach (var request in itemRequests)
                {
                    resourcesById.TryGetValue(request.ResourceId, out var resource);
                    var owner = await OwnerAsync(resource?.OwnerId);
                    line.Requests.Add(new RequestRowViewModel
                    {
                        Id = request.Id,
                        ResourceName = resource?.Name ?? "Slettet ressurs",
                        OwnerName = OwnerName(owner),
                        OwnerPhone = owner?.PhoneNumber ?? resource?.OwnerTelephoneNumber,
                        Status = request.Status,
                        Unanswered = request.IsUnanswered(now),
                        Delivery = request.Delivery,
                        SentAt = request.SentAt,
                        AnswerBy = request.AnswerBy,
                        ConfirmedByPhone = request.ConfirmedByPhone,
                        Comment = request.Comment,
                        DeliveredAt = request.DeliveredAt,
                        ReturnedAt = request.ReturnedAt
                    });
                }

                if (line.IsCovered || need.ClosedAt != null)
                {
                    lines.Add(line);
                    continue;
                }

                // Already asked in this need (on any line): waiting, not answered or accepted.
                // Not answered: give more time or withdraw it first, instead of asking again
                var asked = requests
                    .Where(request => request.Status == RequestStatus.Godtatt || request.Status == RequestStatus.Sendt)
                    .Select(request => request.ResourceId)
                    .ToHashSet();

                var candidates = resources
                    .Where(resource => resource.Type == item.Type
                                       && (string.IsNullOrEmpty(resource.Status) || resource.Status == "Ledig")
                                       && !inUse.Contains(resource.Id)
                                       && !asked.Contains(resource.Id))
                    .Select(resource => new
                    {
                        Resource = resource,
                        Distance = need.Latitude != null && need.Longitude != null && resource.Latitude != null && resource.Longitude != null
                            ? GeoDistance.Km(need.Latitude.Value, need.Longitude.Value, resource.Latitude.Value, resource.Longitude.Value)
                            : (double?)null
                    })
                    .OrderBy(x => x.Distance ?? double.MaxValue)
                    .Take(10);

                foreach (var candidate in candidates)
                {
                    var owner = await OwnerAsync(candidate.Resource.OwnerId);
                    line.Suggestions.Add(new SuggestionViewModel
                    {
                        ResourceId = candidate.Resource.Id,
                        Name = candidate.Resource.Name,
                        OwnerName = OwnerName(owner),
                        OwnerPhone = owner?.PhoneNumber ?? candidate.Resource.OwnerTelephoneNumber,
                        DistanceKm = candidate.Distance,
                        InsideCircle = candidate.Distance != null && candidate.Distance <= need.RadiusKm,
                         Latitude = candidate.Resource.Latitude,
                        Longitude = candidate.Resource.Longitude
                    });
                }

                lines.Add(line);
            }

            return lines;
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
                        Status = NeedStatusRules.For(r, _requestRepository.GetForNeed(r.Id)),
                        CreatedAt = r.CreatedAt,
                        City = r.City,
                        Municipality = r.Municipality,
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