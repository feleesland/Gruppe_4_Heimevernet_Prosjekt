using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.ResourceNeed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Security.Claims;




namespace Heimevernet_Prosjekt.Controllers
{
    // Must be logged in; who can edit and delete is checked by ResourceAuthorizationHandler - Docs: Introduction to authorization
    // https://learn.microsoft.com/aspnet/core/security/authorization/introduction
    // [ValidateAntiForgeryToken] on all POST methods - Docs: Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks
    // https://learn.microsoft.com/aspnet/core/security/anti-request-forgery
    //[Authorize(Roles = RoleNames.Admin)]
    [Authorize]
    public class ResourceNeedHandlerController : Controller
    {
        private readonly IResourceNeedRepository _resourceNeedRepository;
        private readonly IAuthorizationService _authorizationService;

        public ResourceNeedHandlerController(IResourceNeedRepository resourceNeedRepository, IAuthorizationService authorizationService)
        {
            _resourceNeedRepository = resourceNeedRepository;
            _authorizationService = authorizationService;
        }


        // The list is on the Resources page, which only shows what the user is allowed to see
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("List", "ResourceNeed");
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ResourceNeedViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // The logged-in user owns the new resource - Docs: Claims-based authorization
            model.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var resource = _resourceNeedRepository.Create(model);

            return RedirectToAction("List", "ResourceNeed", new { id = resource.Id });
        }

        [HttpGet]
        public async Task<ActionResult> Edit(int id)
        {
            var resource = _resourceNeedRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            // Only the owner or admin can edit (not operator) - Docs: Resource-based authorization
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
            // Only the owner or admin can save changes - Docs: Resource-based authorization
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
            // Only the owner or admin can delete - Docs: Resource-based authorization
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

            return RedirectToAction("List", "Resource");
        }
    }
}