using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
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
    public class ResourceHandlerController : Controller
    {
        private readonly IResourceRepository _resourceRepository;
        private readonly IAuthorizationService _authorizationService;

        // Allowed picture types and max size (5 MB)
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
        private const long MaxImageSize = 5 * 1024 * 1024;

        public ResourceHandlerController(IResourceRepository resourceRepository, IAuthorizationService authorizationService)
        {
            _resourceRepository = resourceRepository;
            _authorizationService = authorizationService;
        }


        // The list is on the Resources page, which only shows what the user is allowed to see
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("List", "Resource");
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ResourceViewModel model)
        {
            ValidateImage(model.Image);
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // The logged-in user owns the new resource - Docs: Claims-based authorization
            model.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var resource = _resourceRepository.Create(model);

            return RedirectToAction("List", "Resource", new { id = resource.Id });
        }

        [HttpGet]
        public async Task<ActionResult> Edit(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            // Only the owner or admin can edit (not operator) - Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Update);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var model = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type,
                Notes = resource.Notes,
                HasImage = resource.ImageData != null,

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
        public async Task<ActionResult> Edit(ResourceViewModel model)
        {
            ValidateImage(model.Image);
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (model.Id == null)
            {
                return BadRequest();
            }
            var existing = _resourceRepository.GetById(model.Id.Value);
            if (existing == null)
            {
                return NotFound();
            }
            // Only the owner or admin can save changes - Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, existing, ResourceOperations.Update);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var success = _resourceRepository.Update(model.Id.Value, model);

            if (!success)
            {
                return NotFound();
            }
            return RedirectToAction("List", "Resource");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            // Only the owner or admin can delete (soft delete, the resource is kept) - Docs: Resource-based authorization
            var authorizationResult = await _authorizationService.AuthorizeAsync(User, resource, ResourceOperations.Delete);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }
            var success = _resourceRepository.Delete(id);
            if (!success)
            {
                return NotFound();
            }

            return RedirectToAction("List", "Resource");
        }

        // Checks the uploaded picture before it is saved: only images, max 5 MB - Docs: Upload files in ASP.NET Core (security considerations)
        // https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads
        private void ValidateImage(IFormFile? image)
        {
            if (image == null)
            {
                return;
            }
            if (!AllowedImageTypes.Contains(image.ContentType))
            {
                ModelState.AddModelError(nameof(ResourceViewModel.Image), "Bildet må være JPG, PNG eller WebP.");
            }
            if (image.Length > MaxImageSize)
            {
                ModelState.AddModelError(nameof(ResourceViewModel.Image), "Bildet kan ikke være større enn 5 MB.");
            }
        }
    }
}