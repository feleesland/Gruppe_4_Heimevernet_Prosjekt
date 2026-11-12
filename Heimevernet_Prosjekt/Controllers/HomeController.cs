using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Heimevernet_Prosjekt.Controllers;

public class HomeController : Controller
{
    private readonly HeimevernetDbContext _context;

    public HomeController(HeimevernetDbContext context)
    {
        _context = context;
    }

   
    //definerer en liste som en in-memory lagring
    private static List<PositionModel> positions = new List<PositionModel>();
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Map()
    {
        return View();
    }
    [HttpPost]
    public IActionResult Map(PositionModel model)
    {
        if (ModelState.IsValid)
        {
            //Legger ny posisjon til "positions" listen
            positions.Add(model);

            return View("Kart", positions);
        }
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PermanentMapMarkers()
    {
        return Ok(
            await _context.PermanentMapMarkers.ToListAsync()
        );
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
