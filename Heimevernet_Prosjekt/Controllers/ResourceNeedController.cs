using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels;
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

        // "Oversikt": the operator's main page for one area (help with AI).
        // ?region=Kristiansand shows one kommune, ?region=alle all of Norway, no region = the page asks (overview.js
        // goes to the area saved in the browser first)
        [HttpGet]
        public async Task<IActionResult> Overview(string? region)
        {
            var now = DateTime.UtcNow;
            var model = new OverviewViewModel { Region = RegionList.Chosen(Request, region) }; // the saved area first
            var resources = _resourceRepository.GetAll();
            var resourcesById = resources.ToDictionary(resource => resource.Id);
            var inUse = _requestRepository.GetResourceIdsInUse();
            var allNeeds = _resourceNeedRepository.GetAll();

            // The kommune of a need or a resource, and the area picker: the same on Ressurser and Behov (Services/RegionList.cs)
            static string PlaceOf(string? municipality, string? city) => RegionList.PlaceOf(municipality, city);
            bool InRegion(string place) => RegionList.InRegion(place, model.Region);
            model.Regions = RegionList.Build(allNeeds, resources);
            model.RegionResources = resources.Count(r => InRegion(PlaceOf(r.Municipality, r.City)));

            // Owners are looked up once each - Docs: Introduction to Identity on ASP.NET Core
            // https://learn.microsoft.com/aspnet/core/security/authentication/identity
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
            static string NameOf(ApplicationUser? owner) =>
                owner == null ? "Ukjent eier"
                : $"{owner.FirstName} {owner.LastName}".Trim() is { Length: > 0 } name ? name : owner.Email ?? "Ukjent eier";

            // One row for "Utlånt nå" / "Må følges opp". A deleted resource is "Slettet ressurs" with no owner
            async Task<OverviewLoanRow> LoanAsync(ResourceNeed need, ResourceRequest r, DateTime? since)
            {
                resourcesById.TryGetValue(r.ResourceId, out var resource);
                var owner = await OwnerAsync(resource?.OwnerId);
                return new OverviewLoanRow
                {
                    RequestId = r.Id,
                    NeedId = need.Id,
                    NeedName = need.Name,
                    ResourceId = r.ResourceId,
                    ResourceDeleted = resource == null,
                    ResourceName = resource?.Name ?? "Slettet ressurs",
                    OwnerName = resource == null ? "" : NameOf(owner),
                    OwnerPhone = owner?.PhoneNumber ?? resource?.OwnerTelephoneNumber,
                    OwnerEmail = owner?.Email,
                    Since = since
                };
            }

            foreach (var need in allNeeds.Where(n => InRegion(PlaceOf(n.Municipality, n.City))))
            {
                var requests = _requestRepository.GetForNeed(need.Id);

                // Said yes and not back yet - also on closed needs, so nothing is forgotten
                foreach (var r in requests.Where(r => r.Status == RequestStatus.Godtatt && r.ReturnedAt == null))
                {
                    if (need.ClosedAt != null)
                    {
                        model.GiveBack.Add(await LoanAsync(need, r, need.ClosedAt));
                    }
                    else if (r.DeliveredAt == null)
                    {
                        // A planned need: count from when it starts, not from the yes
                        var since = need.Urgency == NeedUrgency.Planlagt && need.NeededFrom > r.AnsweredAt ? need.NeededFrom : r.AnsweredAt;
                        var row = await LoanAsync(need, r, since);
                        row.Late = since != null && since <= now.AddHours(-2);
                        model.NotDelivered.Add(row);
                    }
                    else
                    {
                        model.InUse.Add(await LoanAsync(need, r, r.DeliveredAt));
                    }
                }

                if (need.ClosedAt != null)
                {
                    continue;
                }

                var waiting = requests.Where(r => r.Status == RequestStatus.Sendt && !r.IsUnanswered(now)).ToList();
                var overdue = requests.Where(r => r.IsUnanswered(now)).ToList();

                // Per line: covered (never more than the line asks for), asked, and free resources of the type -
                // inside the need's circle and anywhere. No radius set: 2 km, like the map
                var radiusKm = need.RadiusKm > 0 ? (double)need.RadiusKm : 2;
                var hasPlace = need.Latitude != null && need.Longitude != null;
                var asked = requests
                    .Where(r => r.Status == RequestStatus.Godtatt || r.Status == RequestStatus.Sendt)
                    .Select(r => r.ResourceId)
                    .ToHashSet();
                int covered = 0, quantity = 0;
                var noneNearby = false;
                var checkedNearby = false;
                var nearbyIds = new HashSet<int>();
                var lines = new List<OverviewLineRow>();
                foreach (var item in need.Items)
                {
                    var itemRequests = requests.Where(r => r.ResourceNeedItemId == item.Id).ToList();
                    var accepted = itemRequests.Count(r => r.Status == RequestStatus.Godtatt);
                    covered += Math.Min(item.Quantity, accepted);
                    quantity += item.Quantity;
                    var line = new OverviewLineRow
                    {
                        Type = item.Type,
                        Quantity = item.Quantity,
                        Covered = Math.Min(item.Quantity, accepted),
                        Delivered = itemRequests.Count(r => r.Status == RequestStatus.Godtatt && r.DeliveredAt != null),
                        Waiting = itemRequests.Count(r => r.Status == RequestStatus.Sendt)
                    };
                    lines.Add(line);

                    // Enough said yes or are being asked, or the need has no place: nothing to look for
                    var open = itemRequests.Count(r => r.Status == RequestStatus.Godtatt || r.Status == RequestStatus.Sendt);
                    if (open >= item.Quantity || !hasPlace) continue;

                    checkedNearby = true;
                    var free = resources
                        .Where(res => res.Type == item.Type
                            && (string.IsNullOrEmpty(res.Status) || res.Status == "Ledig")
                            && !inUse.Contains(res.Id)
                            && !asked.Contains(res.Id)
                            && res.Latitude != null && res.Longitude != null)
                        .Select(res => (Id: res.Id, Km: GeoDistance.Km(need.Latitude!.Value, need.Longitude!.Value, res.Latitude!.Value, res.Longitude!.Value)))
                        .ToList();
                    var inside = free.Where(x => x.Km <= radiusKm).Select(x => x.Id).ToList();
                    line.FreeInside = inside.Count;
                    line.FreeAnywhere = free.Count;
                    if (inside.Count == 0) noneNearby = true;
                    nearbyIds.UnionWith(inside);
                }

                var missing = quantity - covered;
                var startsSoon = need.Urgency == NeedUrgency.Planlagt && need.NeededFrom != null
                                 && need.NeededFrom <= now.AddHours(24) && missing > 0;
                var urgent = need.Urgency == NeedUrgency.Akutt || startsSoon;
                var nobodyAsked = missing > 0 && waiting.Count == 0 && overdue.Count == 0;

                if (need.Urgency == NeedUrgency.Akutt) model.ActiveAkutt++;
                else model.ActivePlanned++;
                model.Waiting += waiting.Count;

                model.Needs.Add(new OverviewNeedRow
                {
                    Id = need.Id,
                    Name = need.Name,
                    Place = PlaceOf(need.Municipality, need.City),
                    Urgency = need.Urgency,
                    CreatedAt = need.CreatedAt,
                    NeededFrom = need.NeededFrom,
                    RadiusKm = radiusKm,
                    HasPlace = hasPlace,
                    Covered = covered,
                    Quantity = quantity,
                    Delivered = requests.Count(r => r.Status == RequestStatus.Godtatt && r.DeliveredAt != null),
                    Waiting = waiting.Count,
                    NextAnswerBy = waiting.Count > 0 ? waiting.Min(r => r.AnswerBy) : null,
                    Overdue = overdue.Count,
                    OverdueOwners = overdue.Select(r => r.ResourceId).Distinct().Count(),
                    OverdueSince = overdue.Count > 0 ? overdue.Min(r => r.AnswerBy) : null,
                    Lines = lines,
                    NobodyAsked = nobodyAsked,
                    NoneNearby = noneNearby,
                    FreeNearby = checkedNearby ? nearbyIds.Count : null,
                    StartsSoon = startsSoon,
                    Level = overdue.Count > 0 || (urgent && (nobodyAsked || noneNearby)) ? 2 : startsSoon ? 1 : 0
                });
            }

            // Red first, then orange, then Akutt before Planlagt, oldest first
            model.Needs = model.Needs
                .OrderByDescending(n => n.Level)
                .ThenBy(n => n.Urgency)
                .ThenBy(n => n.CreatedAt)
                .ToList();
            // Longest out first
            model.InUse = model.InUse.OrderBy(l => l.Since).ToList();
            model.NotDelivered = model.NotDelivered.OrderBy(l => l.Since).ToList();
            model.GiveBack = model.GiveBack.OrderBy(l => l.Since).ToList();
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
                MeetingPointName = need.MeetingPointName,
                MeetingLatitude = need.MeetingLatitude,
                MeetingLongitude = need.MeetingLongitude,
                ClosedAt = need.ClosedAt,
                Status = NeedStatusRules.For(need, _requestRepository.GetForNeed(need.Id)),
                Items = need.Items.OrderBy(item => item.CreatedAt).ToList()
            };
            // Who reported the need, so the operators can call them (help with AI). Name and phone from the account
            var reporter = string.IsNullOrEmpty(need.OwnerId) ? null : await _userManager.FindByIdAsync(need.OwnerId);
            if (reporter != null)
            {
                var reporterName = $"{reporter.FirstName} {reporter.LastName}".Trim();
                viewModel.ReportedByName = reporterName.Length > 0 ? reporterName : reporter.Email;
                viewModel.ReportedByPhone = reporter.PhoneNumber;
            }
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

        // List of needs: only those the user is allowed to see - Docs: Resource-based authorization.
        // ?region=Kristiansand: only that kommune, ?region=alle all of Norway (the area picker, the same as on Oversikt)
        public async Task<IActionResult> List(string? region = null)
        {
            var model = new List<ResourceNeedViewModel>();
            region = RegionList.Chosen(Request, region); // the saved area first, the same as on Oversikt
            var allNeeds = _resourceNeedRepository.GetAll();

            // Active first, newest first
            foreach (var r in allNeeds
                         .Where(r => RegionList.InRegion(RegionList.PlaceOf(r.Municipality, r.City), region))
                         .OrderBy(r => r.ClosedAt != null)
                         .ThenByDescending(r => r.CreatedAt))
            {
                var authorizationResult = await _authorizationService.AuthorizeAsync(User, r, ResourceNeedOperations.Read);
                if (authorizationResult.Succeeded)
                {
                    var requests = _requestRepository.GetForNeed(r.Id);
                    var (covered, quantity) = NeedStatusRules.Coverage(r, requests);
                    model.Add(new ResourceNeedViewModel
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Description = r.Description,
                        Type = r.Type,
                        Urgency = r.Urgency,
                        NeededFrom = r.NeededFrom,
                        ClosedAt = r.ClosedAt,
                        Status = NeedStatusRules.For(r, requests),
                        Covered = covered,
                        Quantity = quantity,
                        OutCount = requests.Count(x => x.Status == RequestStatus.Godtatt && x.ReturnedAt == null),
                        CreatedAt = r.CreatedAt,
                        City = r.City,
                        Municipality = r.Municipality,
                        Items = r.Items.ToList()
                    });
                }
            }

            // The area line at the top (Views/Shared/_RegionPicker.cshtml)
            ViewData["RegionPicker"] = new RegionPickerViewModel
            {
                Region = region,
                Regions = RegionList.Build(allNeeds, _resourceRepository.GetAll()),
                Url = Url.Action(nameof(List))!,
                Compact = true
            };
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
