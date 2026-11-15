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
        public async Task<IActionResult> Overview()
        {
            var now = DateTime.UtcNow;
            var model = new OverviewViewModel();
            var resources = _resourceRepository.GetAll();
            var resourcesById = resources.ToDictionary(resource => resource.Id);
            var inUse = _requestRepository.GetResourceIdsInUse();
            string ResourceName(int id) => resourcesById.TryGetValue(id, out var r) ? r.Name : $"Ressurs #{id}";

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

            // One row for the "Ute nå" box: resource, owner and need
            async Task<OverviewLoanRow> LoanAsync(ResourceNeed need, ResourceRequest r, DateTime? since)
            {
                resourcesById.TryGetValue(r.ResourceId, out var resource);
                var owner = await OwnerAsync(resource?.OwnerId);
                var ownerName = owner == null ? string.Empty : $"{owner.FirstName} {owner.LastName}".Trim();
                return new OverviewLoanRow
                {
                    RequestId = r.Id,
                    NeedId = need.Id,
                    NeedName = need.Name,
                    ResourceId = r.ResourceId,
                    ResourceName = resource?.Name ?? $"Ressurs #{r.ResourceId}",
                    OwnerName = ownerName.Length > 0 ? ownerName : owner?.Email ?? "Ukjent eier",
                    OwnerPhone = owner?.PhoneNumber ?? resource?.OwnerTelephoneNumber,
                    Since = since
                };
            }

            foreach (var need in _resourceNeedRepository.GetAll())
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
                var declined = requests.Where(r => r.Status == RequestStatus.Avslatt && r.AnsweredAt > now.AddHours(-24)).ToList();

                foreach (var r in waiting)
                {
                    model.WaitingList.Add(new OverviewRequestRow(r.Id, need.Id, need.Name, ResourceName(r.ResourceId), r.AnswerBy));
                }
                if (overdue.Count > 0)
                {
                    model.Overdue.Add(new OverviewRequestGroup(need.Id, need.Name,
                        overdue.Select(r => ResourceName(r.ResourceId)).ToList(), overdue.Min(r => r.AnswerBy)));
                }
                if (declined.Count > 0)
                {
                    model.Declined.Add(new OverviewRequestGroup(need.Id, need.Name,
                        declined.Select(r => ResourceName(r.ResourceId)).ToList(), declined.Max(r => r.AnsweredAt!.Value)));
                }

                // Covered per line (never more than the line asks for), and free resources inside the need's radius
                // for the lines that still miss something. No radius set: 2 km, like the map
                var radius = need.RadiusKm > 0 ? need.RadiusKm : 2;
                var asked = requests
                    .Where(r => r.Status == RequestStatus.Godtatt || r.Status == RequestStatus.Sendt)
                    .Select(r => r.ResourceId)
                    .ToHashSet();
                int covered = 0, quantity = 0;
                var noneNearby = false;
                var checkedNearby = false;
                var nearbyIds = new HashSet<int>();
                foreach (var item in need.Items)
                {
                    var itemRequests = requests.Where(r => r.ResourceNeedItemId == item.Id).ToList();
                    covered += Math.Min(item.Quantity, itemRequests.Count(r => r.Status == RequestStatus.Godtatt));
                    quantity += item.Quantity;

                    // Enough said yes or are being asked, or the need has no place: nothing to look for
                    var open = itemRequests.Count(r => r.Status == RequestStatus.Godtatt || r.Status == RequestStatus.Sendt);
                    if (open >= item.Quantity || need.Latitude == null || need.Longitude == null) continue;

                    checkedNearby = true;
                    var free = resources
                        .Where(res => res.Type == item.Type
                            && (string.IsNullOrEmpty(res.Status) || res.Status == "Ledig")
                            && !inUse.Contains(res.Id)
                            && !asked.Contains(res.Id)
                            && res.Latitude != null && res.Longitude != null
                            && GeoDistance.Km(need.Latitude.Value, need.Longitude.Value, res.Latitude.Value, res.Longitude.Value) <= radius)
                        .Select(res => res.Id)
                        .ToList();
                    if (free.Count == 0) noneNearby = true;
                    nearbyIds.UnionWith(free);
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
                    City = need.City,
                    Urgency = need.Urgency,
                    CreatedAt = need.CreatedAt,
                    NeededFrom = need.NeededFrom,
                    RadiusKm = (double)radius,
                    Covered = covered,
                    Quantity = quantity,
                    Delivered = requests.Count(r => r.Status == RequestStatus.Godtatt && r.DeliveredAt != null),
                    Waiting = waiting.Count,
                    NextAnswerBy = waiting.Count > 0 ? waiting.Min(r => r.AnswerBy) : null,
                    Overdue = overdue.Count,
                    NobodyAsked = nobodyAsked,
                    NoneNearby = noneNearby,
                    FreeNearby = checkedNearby ? nearbyIds.Count : null,
                    StartsSoon = startsSoon,
                    Level = overdue.Count > 0 || (urgent && (nobodyAsked || noneNearby)) ? 2 : startsSoon ? 1 : 0
                });
            }

            model.OutNow = model.NotDelivered.Count + model.GiveBack.Count + model.InUse.Count;

            // Red first, then orange, then Akutt before Planlagt, oldest first
            model.Needs = model.Needs
                .OrderByDescending(n => n.Level)
                .ThenBy(n => n.Urgency)
                .ThenBy(n => n.CreatedAt)
                .ToList();
            // Longest out first
            model.NotDelivered = model.NotDelivered.OrderBy(l => l.Since).ToList();
            model.GiveBack = model.GiveBack.OrderBy(l => l.Since).ToList();
            model.InUse = model.InUse.OrderBy(l => l.Since).ToList();
            model.Overdue = model.Overdue.OrderBy(g => g.When).ToList();
            model.WaitingList = model.WaitingList.OrderBy(r => r.AnswerBy).ToList();
            model.Declined = model.Declined.OrderByDescending(g => g.When).ToList();
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