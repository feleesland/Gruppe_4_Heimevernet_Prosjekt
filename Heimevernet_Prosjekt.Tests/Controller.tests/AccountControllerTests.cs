using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.Models.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        var user = new IdentityUser { Email = "user@example.com" };
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
        var user = new IdentityUser { Email = "user@example.com" };
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
        Mock<UserManager<IdentityUser>>? userManager = null,
        Mock<SignInManager<IdentityUser>>? signInManager = null)
    {
        userManager ??= CreateUserManager();
        signInManager ??= CreateSignInManager(userManager);
        return new AccountController(userManager.Object, signInManager.Object, Mock.Of<IEmailSender>());
    }

    private static Mock<UserManager<IdentityUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<IdentityUser>>(),
            Array.Empty<IUserValidator<IdentityUser>>(),
            Array.Empty<IPasswordValidator<IdentityUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<IdentityUser>>>());
    }

    private static Mock<SignInManager<IdentityUser>> CreateSignInManager(
        Mock<UserManager<IdentityUser>> userManager)
    {
        return new Mock<SignInManager<IdentityUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<ILogger<SignInManager<IdentityUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<IdentityUser>>());
    }
}
