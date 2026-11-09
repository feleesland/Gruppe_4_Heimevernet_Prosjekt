using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Heimevernet_Prosjekt.Controllers
{
    // "Mine forespørsler": every logged-in user who owns resources can answer requests for them
    // [ValidateAntiForgeryToken] on POST - Docs: Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks
    // https://learn.microsoft.com/aspnet/core/security/anti-request-forgery
    [Authorize]
    public class ResourceRequestController : Controller
    {
        private readonly IResourceRequestRepository _requestRepository;

        public ResourceRequestController(IResourceRequestRepository requestRepository)
        {
            _requestRepository = requestRepository;
        }

        [HttpGet]
        public IActionResult Mine()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return View(_requestRepository.GetForOwner(userId));
        }

        // answer = "leverer", "hentes" or "nei"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Answer(int id, string answer, string? comment)
        {
            DeliveryMethod? delivery = answer switch
            {
                "leverer" => DeliveryMethod.Leverer,
                "hentes" => DeliveryMethod.Hentes,
                _ => null
            };
            if (delivery == null && answer != "nei")
            {
                return BadRequest();
            }
            if (comment?.Length > 500)
            {
                comment = comment[..500];
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = _requestRepository.Answer(id, userId, delivery != null, delivery, comment);

            switch (result)
            {
                case AnswerResult.NotFound:
                    return NotFound();
                case AnswerResult.Accepted:
                    TempData["Message"] = delivery == DeliveryMethod.Hentes
                        ? "Takk! Du har sagt ja, og ressursen blir hentet."
                        : "Takk! Du har sagt ja og leverer ressursen.";
                    break;
                case AnswerResult.Declined:
                    TempData["Message"] = "Takk for svaret.";
                    break;
                case AnswerResult.AlreadyCovered:
                    TempData["Error"] = "Behovet ble dekket av andre før du svarte. Takk likevel!";
                    break;
                default:
                    TempData["Error"] = "Forespørselen er ikke åpen lenger.";
                    break;
            }

            return RedirectToAction(nameof(Mine));
        }
    }
}