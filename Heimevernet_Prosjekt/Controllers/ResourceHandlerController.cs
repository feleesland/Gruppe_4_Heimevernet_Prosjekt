using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;




namespace Heimevernet_Prosjekt.Controllers
{
    [Authorize(Roles = RoleNames.Admin)]
    public class ResourceHandlerController : Controller
    {
        private readonly IResourceRepository _resourceRepository;

        public ResourceHandlerController(IResourceRepository resourceRepository)
        {
            _resourceRepository = resourceRepository;
        }


        [HttpGet]
        public IActionResult index()
        {
            var all = _resourceRepository.GetAll();
            //map the resources to view models
            var model = all.Select(r => new ResourceViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Type = r.Type
            }).ToList();

            return View(model);

        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Create(ResourceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var resource = _resourceRepository.Create(model);

            return RedirectToAction("index", "Resource", new { id = resource.Id });
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var resource = _resourceRepository.GetById(id);
            if (resource == null)
            {
                return NotFound();
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
            var success = _resourceRepository.Update(model.Id.Value, model);

            if(!success)
            {
                return NotFound();
            }
            return RedirectToAction("Index");
        }
        [HttpGet]
        public ActionResult Delete(int id)
        {
            var success = _resourceRepository.Delete(id);
            if (!success)
                    {
                return NotFound();
            }

            return RedirectToAction("Index");
        }
    }
}
