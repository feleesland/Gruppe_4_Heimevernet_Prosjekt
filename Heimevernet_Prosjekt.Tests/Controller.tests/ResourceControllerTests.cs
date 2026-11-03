using System.Security.Claims;
using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

public sealed class ResourceControllerTests
{
    [Fact]
    public async Task Index_WhenResourceIdIsMissing_ReturnsEmptyViewModel()
    {
        var repository = new Mock<IResourceRepository>();
        var authorization = new Mock<IAuthorizationService>();
        var controller = CreateController(repository, authorization);

        var result = await controller.Index(null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResourceViewModel>(view.Model);
        Assert.Null(model.Id);
        repository.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Index_WhenResourceIsAuthorized_ReturnsMappedResourceViewModel()
    {
        var resource = CreateResource();
        var repository = new Mock<IResourceRepository>();
        repository.Setup(repository => repository.GetById(resource.Id)).Returns(resource);
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), resource,
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Contains(ResourceOperations.Read))))
            .ReturnsAsync(AuthorizationResult.Success());
        var controller = CreateController(repository, authorization);

        var result = await controller.Index(resource.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ResourceViewModel>(view.Model);
        Assert.Equal(resource.Id, model.Id);
        Assert.Equal(resource.Name, model.Name);
        Assert.Equal(resource.Description, model.Description);
        Assert.Equal(resource.Type, model.Type);
        Assert.Equal(resource.Address, model.Address);
        Assert.Equal(resource.ZipCode, model.ZipCode);
        Assert.Equal(resource.City, model.City);
        Assert.Equal(resource.Latitude, model.Latitude);
        Assert.Equal(resource.Longitude, model.Longitude);
        Assert.Equal(resource.CreatedAt, model.CreatedAt);
        Assert.Equal(resource.OwnerRemoved, model.OwnerRemoved);
    }

    [Fact]
    public async Task Index_WhenResourceIsNotAuthorized_ReturnsForbidResult()
    {
        var resource = CreateResource();
        var repository = new Mock<IResourceRepository>();
        repository.Setup(repository => repository.GetById(resource.Id)).Returns(resource);
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), resource,
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Contains(ResourceOperations.Read))))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = CreateController(repository, authorization);

        var result = await controller.Index(resource.Id);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task List_ReturnsOnlyResourcesTheUserCanRead()
    {
        var allowedResource = CreateResource(1, "Allowed resource");
        var deniedResource = CreateResource(2, "Denied resource");
        var repository = new Mock<IResourceRepository>();
        repository
            .Setup(repository => repository.GetAll())
            .Returns(new[] { allowedResource, deniedResource });
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), allowedResource,
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Contains(ResourceOperations.Read))))
            .ReturnsAsync(AuthorizationResult.Success());
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), deniedResource,
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Contains(ResourceOperations.Read))))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = CreateController(repository, authorization);

        var result = await controller.List();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<ResourceViewModel>>(view.Model);
        var resources = model.ToList();
        var returnedResource = Assert.Single(resources);
        Assert.Equal(allowedResource.Id, returnedResource.Id);
        Assert.Equal(allowedResource.Name, returnedResource.Name);
        Assert.Equal(allowedResource.Description, returnedResource.Description);
        Assert.Equal(allowedResource.Type, returnedResource.Type);
    }

    private static ResourceController CreateController(
        Mock<IResourceRepository> repository,
        Mock<IAuthorizationService> authorization)
    {
        return new ResourceController(repository.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    private static Resource CreateResource(int id = 42, string name = "Test resource")
    {
        return new Resource
        {
            Id = id,
            Name = name,
            Description = "A resource used in a unit test.",
            Type = "Equipment",
            Address = "Test street 1",
            ZipCode = "0001",
            City = "Oslo",
            Latitude = 59.9139,
            Longitude = 10.7522,
            CreatedAt = new DateTime(2026, 1, 1),
            OwnerRemoved = true
        };
    }
}
