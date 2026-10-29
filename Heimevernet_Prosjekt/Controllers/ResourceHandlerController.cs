using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Security.Claims;




namespace Heimevernet_Prosjekt.Controllers
{
    //[Authorize(Roles = RoleNames.Admin)]
    [Authorize]
    public class ResourceHandlerController : Controller
    {
        private readonly IResourceRepository _resourceRepository;

        public ResourceHandlerController(IResourceRepository resourceRepository)
        {
            _resourceRepository = resourceRepository;
        }


        // The list is on the Resources page, which is filtered by owner (help with AI)
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
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // The logged-in user owns the new resource (help with AI)
            model.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var resource = _resourceRepository.Create(model);

            return RedirectToAction("List", "Resource", new { id = resource.Id });
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            if (!CanAccess(resource.OwnerId))
            {
                return Forbid();
            }
            var model = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type
            };
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ResourceViewModel model)
        {
            if(!ModelState.IsValid)
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
            if (!CanAccess(existing.OwnerId))
            {
                return Forbid();
            }
            var success = _resourceRepository.Update(model.Id.Value, model);

            if(!success)
            {
                return NotFound();
            }
            return RedirectToAction("List","Resource");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
            }
            if (!CanAccess(resource.OwnerId))
            {
                return Forbid();
            }
            var success = _resourceRepository.Delete(id);
            if (!success)
                    {
                return NotFound();
            }

            return RedirectToAction("List","Resource");
        }

        // Admin can access everything, others only their own (help with AI)
        private bool CanAccess(string ownerId)
        {
            return User.IsInRole(RoleNames.Admin)
                || ownerId == User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
