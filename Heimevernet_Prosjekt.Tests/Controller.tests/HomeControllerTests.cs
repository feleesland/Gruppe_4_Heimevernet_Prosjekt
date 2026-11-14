using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

/// <summary>
/// Contains tests for the home controller actions.
/// </summary>
/// <remarks>Verifies default views, map submission handling, model validation, and request identifier
/// preservation.</remarks>
public sealed class HomeControllerTests
{
    private static HeimevernetDbContext CreateContext()
    {
        // Lager en ny, isolert in-memory database for hver test

        var options = new DbContextOptionsBuilder<HeimevernetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HeimevernetDbContext(options);
    }


    [Fact]
    public void Index_ReturnsDefaultView()
    {
        using var context = CreateContext();        //trenger denn linjen for å opprette en instans av HomeController som krever en HeimevernetDbContext i konstruktøren
        var controller = new HomeController(context);

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public void MapGet_ReturnsDefaultView()
    {
        using var context = CreateContext();
        var controller = new HomeController(context);

        var result = controller.Map();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public void MapPost_WhenModelIsValid_ReturnsKartViewWithSubmittedPosition()
    {
        using var context = CreateContext();
        var controller = new HomeController(context);
        var model = new PositionModel
        {
            Latitude = "59.9139",
            Longitude = "10.7522"
        };

        var result = controller.Map(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Kart", view.ViewName);
        var positions = Assert.IsAssignableFrom<IEnumerable<PositionModel>>(view.Model);
        Assert.Contains(model, positions);
    }

    [Fact]
    public void MapPost_WhenModelStateIsInvalid_ReturnsDefaultView()
    {
        using var context = CreateContext();
        var controller = new HomeController(context);
        controller.ModelState.AddModelError(nameof(PositionModel.Latitude), "Latitude is required.");

        var result = controller.Map(new PositionModel());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Null(view.Model);
    }

    [Fact]
    public void Error_UsesTraceIdentifierWhenThereIsNoActivity()
    {
        using var context = CreateContext();
        var controller = new HomeController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "request-123"
                }
            }
        };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("request-123", model.RequestId);
    }
}
