using System.Security.Claims;
using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.DataAccess;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Heimevernet_Prosjekt.Models.ViewModels.Resource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Heimevernet_Prosjekt.Security.Authorization;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

/// <summary>
///this is several unit tests for the resource controller
/// Provides unit tests for resource controller actions.
/// <remarks>Verifies resource lookup, authorization, result types, and view model mapping.</remarks>'
///  <summary>
public sealed class ResourceControllerTests
{
    [Fact]
    public async Task Index_WhenResourceIdIsMissing_ReturnsNotFound()
    {
        var repository = new Mock<IResourceRepository>();
        var authorization = new Mock<IAuthorizationService>();
        var controller = CreateController(repository, authorization);

        var result = await controller.Index(null);

        // A missing resource ID should return HTTP 404.
        Assert.IsType<NotFoundResult>(result);

        // No lookup or authorization check is needed without an ID.
        repository.Verify(
            repository => repository.GetById(It.IsAny<int>()),
            Times.Never);
        authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Index_WhenResourceIsAuthorized_ReturnsMappedResourceViewModel()
    {
        var resource = CreateResource();
        var repository = new Mock<IResourceRepository>();
        repository.Setup(repository => repository.GetById(resource.Id)).Returns(resource);
        var authorization = new Mock<IAuthorizationService>();
        // Default answer "not allowed" for everything else: Index also checks Update/Delete for the Rediger/Slett buttons (help with AI)
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
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
        // Default answer "not allowed" for everything else: List also checks Update/Delete for the Rediger/Slett buttons (help with AI)
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
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
            //.ReturnsAsync(AuthorizationResult.Failed());
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
    [Fact]
    public async Task Index_WhenResourceDoesNotExist_ReturnsNotFound()
    {
        var repository = new Mock<IResourceRepository>();
        repository
            .Setup(repo => repo.GetById(999))
            .Returns((Resource?)null);

        var authorization = new Mock<IAuthorizationService>();
        var controller = CreateController(repository, authorization);

        var result = await controller.Index(999);

        Assert.IsType<NotFoundResult>(result);

        repository.Verify(repo => repo.GetById(999), Times.Once);
        authorization.VerifyNoOtherCalls();
    }

    private static ResourceController CreateController(
    Mock<IResourceRepository> repository,
    Mock<IAuthorizationService> authorization)
    {
        // ResourceController now needs UserManager (owner info) - a mock with an empty user store (help with AI)
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var requests = new Mock<IResourceRequestRepository>();
        requests.Setup(r => r.GetResourceIdsInUse()).Returns(new HashSet<int>()); // no resources are lent out in these tests
        // No open "feil info" messages in the tests (the ? filter on the map) (help with AI)
        var reports = new Mock<IResourceReportRepository>();
        reports.Setup(r => r.CountOpenByResource()).Returns(new Dictionary<int, int>());
        // No types or categories in the tests, so List can look up icons without crashing (help with AI)
        var types = new Mock<IResourceTypeRepository>();
        types.Setup(t => t.GetCategories()).Returns([]);

        return new ResourceController(repository.Object, authorization.Object, userManager.Object, types.Object, requests.Object, reports.Object)
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
