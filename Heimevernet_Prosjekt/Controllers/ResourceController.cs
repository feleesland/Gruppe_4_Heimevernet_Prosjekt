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
            _resourceRepository = resourceRepository;
        }
        [Authorize]
        public IActionResult Index(int? id)
        {
            var resource = id.HasValue ? _resourceRepository.GetById(id.Value) : null;
            var viewModel = new ResourceViewModel();
            if (resource == null)
            {
                return View(viewModel);
            }
            viewModel = new ResourceViewModel
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Type = resource.Type
            };
            return View(viewModel);
        }
    }
}
