using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimevernet_Prosjekt.Controllers
{
    public class ResourceController : Controller
    {
        private readonly IResourceRepository _resourceRepository;

        public ResourceController(IResourceRepository resourceRepository)
        {
            // Keep the repository so we can find resources.
            _resourceRepository = resourceRepository;
        }

        [Authorize]
        [HttpGet]
        public IActionResult Index(int? id)
        {
            // A resource ID is needed to open this page.
            if (!id.HasValue)
            {
                return NotFound();
            }

            // Find the resource with this ID.
            var resource = _resourceRepository.GetById(id.Value);

            // Return 404 if the resource does not exist.
            if (resource == null)
            {
                return NotFound();
            }

            // Copy the information needed by the page.
            var viewModel = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type
            };

            // Show the resource page.
            return View(viewModel);
        }
    }
}