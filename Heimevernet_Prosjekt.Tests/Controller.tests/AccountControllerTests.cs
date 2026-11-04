using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.Models.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

public sealed class AccountControllerTests
{
    [Fact]
    public void LoginGet_ReturnsLoginViewModelWithReturnUrl()
    {
        var controller = CreateController();

        var result = controller.Login("/Resource/List");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("/Resource/List", model.ReturnUrl);
    }

    [Fact]
    public async Task LoginPost_WhenModelStateIsInvalid_ReturnsTheSameViewModel()
    {
        var controller = CreateController();
        var model = new LoginViewModel { Email = "invalid", Password = string.Empty };
        controller.ModelState.AddModelError(nameof(LoginViewModel.Email), "Invalid email.");

        var result = await controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
    }

    [Fact]
    public async Task LoginPost_WhenCredentialsAreValid_RedirectsToLocalReturnUrl()
    {
        var user = new ApplicationUser { Email = "user@example.com" };
        var userManager = CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        signInManager
            .Setup(manager => manager.PasswordSignInAsync(user, "Password1!", true, true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var controller = CreateController(userManager, signInManager);
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(url => url.IsLocalUrl("/Resource/List")).Returns(true);
        controller.Url = urlHelper.Object;

        var result = await controller.Login(new LoginViewModel
        {
            Email = user.Email!,
            Password = "Password1!",
            RememberMe = true,
            ReturnUrl = "/Resource/List"
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Resource/List", redirect.Url);
        signInManager.Verify(manager => manager.PasswordSignInAsync(user, "Password1!", true, true), Times.Once);
    }

    [Fact]
    public async Task LoginPost_WhenCredentialsAreInvalid_ReturnsViewWithModelError()
    {
        var user = new ApplicationUser { Email = "user@example.com" };
        var userManager = CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        userManager.Setup(manager => manager.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        signInManager
            .Setup(manager => manager.PasswordSignInAsync(user, "WrongPassword1!", false, true))
            .ReturnsAsync(IdentitySignInResult.Failed);

        var controller = CreateController(userManager, signInManager);

        var result = await controller.Login(new LoginViewModel
        {
            Email = user.Email!,
            Password = "WrongPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(view.Model, view.ViewData.Model);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Invalid email or password.");
    }

    [Fact]
    public void ResetPasswordGet_WhenCodeOrEmailIsMissing_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.ResetPassword();

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("A password reset code and email address are required.", badRequest.Value);
    }

    private static AccountController CreateController(
        Mock<UserManager<ApplicationUser>>? userManager = null,
        Mock<SignInManager<ApplicationUser>>? signInManager = null)
    {
        userManager ??= CreateUserManager();
        signInManager ??= CreateSignInManager(userManager);
        return new AccountController(userManager.Object, signInManager.Object, Mock.Of<IEmailSender>());
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(
        Mock<UserManager<ApplicationUser>> userManager)
    {
        return new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }
}