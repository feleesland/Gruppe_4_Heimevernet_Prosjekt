using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

public sealed class HomeControllerTests
{
    [Fact]
    public void Index_ReturnsDefaultView()
    {
        var controller = new HomeController();

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public void MapGet_ReturnsDefaultView()
    {
        var controller = new HomeController();

        var result = controller.Map();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
    }

    [Fact]
    public void MapPost_WhenModelIsValid_ReturnsKartViewWithSubmittedPosition()
    {
        var controller = new HomeController();
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
        var controller = new HomeController();
        controller.ModelState.AddModelError(nameof(PositionModel.Latitude), "Latitude is required.");

        var result = controller.Map(new PositionModel());

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Null(view.Model);
    }

    [Fact]
    public void Error_UsesTraceIdentifierWhenThereIsNoActivity()
    {
        var controller = new HomeController
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
