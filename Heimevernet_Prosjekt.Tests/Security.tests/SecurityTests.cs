using System.Reflection;
using Heimevernet_Prosjekt.Controllers;
using Heimevernet_Prosjekt.Models.Authorization;
using Heimevernet_Prosjekt.Models.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Heimevernet_Prosjekt.Tests.Controller.tests;

public sealed class SecurityTests
{
    // ---------------------------------------------------------------
    // PART 1: Reflection tests. They check that the protections are
    // DECLARED. They don't call any controller code, so they are fast.
    // ---------------------------------------------------------------

    [Fact]
    public void AdminController_RequiresAdminRole()
    {
        // GetCustomAttribute reads the [Authorize(...)] sitting on the class
        var attr = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(RoleNames.Admin, attr!.Roles);
    }

    [Fact]
    public void AdminController_PostActions_HaveAntiForgeryToken()
    {
        AssertAllPostActionsHaveAntiForgery(typeof(AdminController));
    }

    [Fact]
    public void AccountController_PostActions_HaveAntiForgeryToken()
    {
        AssertAllPostActionsHaveAntiForgery(typeof(AccountController));
    }

    [Fact]
    public void AccountController_Logout_RequiresLogin()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Logout))!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
    }

    // Helper: finds every method marked [HttpPost] and checks it also has
    // [ValidateAntiForgeryToken]. If someone adds a new POST action and
    // forgets the token, this test fails.
    private static void AssertAllPostActionsHaveAntiForgery(Type controllerType)
    {
        var postMethods = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
            .ToList();

        Assert.NotEmpty(postMethods); // sanity check: we actually found some

        foreach (var method in postMethods)
        {
            Assert.True(
                method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null,
                $"{controllerType.Name}.{method.Name} is a POST action without [ValidateAntiForgeryToken]");
        }
    }

    // ---------------------------------------------------------------
    // PART 2: Behaviour tests on AccountController.Login (POST).
    // ---------------------------------------------------------------

    // Open redirect: after a successful login, an attacker-controlled
    // returnUrl must NOT send the user to another website.
    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    public async Task Login_Success_WithExternalReturnUrl_RedirectsToHomeInstead(string badUrl)
    {
        var (controller, userManager, signInManager) = CreateController();
        var user = new ApplicationUser { Email = "a@test.no", UserName = "a@test.no" };

        userManager.Setup(m => m.FindByEmailAsync("a@test.no")).ReturnsAsync(user);
        signInManager
            .Setup(m => m.PasswordSignInAsync(user, "Password1!", false, true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var model = new LoginViewModel { Email = "a@test.no", Password = "Password1!", ReturnUrl = badUrl };

        var result = await controller.Login(model);

        // It must be a redirect to the Home controller, not to badUrl
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    // The opposite case: a normal local URL should still work,
    // otherwise the fix would break real logins.
    [Fact]
    public async Task Login_Success_WithLocalReturnUrl_RedirectsToThatUrl()
    {
        var (controller, userManager, signInManager) = CreateController();
        var user = new ApplicationUser { Email = "a@test.no", UserName = "a@test.no" };

        userManager.Setup(m => m.FindByEmailAsync("a@test.no")).ReturnsAsync(user);
        signInManager
            .Setup(m => m.PasswordSignInAsync(user, "Password1!", false, true))
            .ReturnsAsync(IdentitySignInResult.Success);

        var model = new LoginViewModel { Email = "a@test.no", Password = "Password1!", ReturnUrl = "/Resource/List" };

        var result = await controller.Login(model);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Resource/List", redirect.Url);
    }

    // User enumeration: "no such user" and "wrong password" must look identical,
    // otherwise an attacker can find out which emails are registered.
    [Fact]
    public async Task Login_UnknownUser_And_WrongPassword_GiveSameErrorMessage()
    {
        // Case 1: user does not exist
        var (controller1, userManager1, _) = CreateController();
        userManager1.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        await controller1.Login(new LoginViewModel { Email = "ghost@test.no", Password = "x" });
        var messageUnknownUser = controller1.ModelState[string.Empty]!.Errors.Single().ErrorMessage;

        // Case 2: user exists, password is wrong
        var (controller2, userManager2, signInManager2) = CreateController();
        var user = new ApplicationUser { Email = "a@test.no", UserName = "a@test.no" };
        userManager2.Setup(m => m.FindByEmailAsync("a@test.no")).ReturnsAsync(user);
        signInManager2
            .Setup(m => m.PasswordSignInAsync(user, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Failed);

        await controller2.Login(new LoginViewModel { Email = "a@test.no", Password = "wrong" });
        var messageWrongPassword = controller2.ModelState[string.Empty]!.Errors.Single().ErrorMessage;

        Assert.Equal(messageUnknownUser, messageWrongPassword);
    }

    // Lockout: a locked-out account must not reveal that it is locked.
    // (Otherwise attackers learn the account exists.)
    [Fact]
    public async Task Login_LockedOutUser_GetsSameGenericError()
    {
        var (controller, userManager, signInManager) = CreateController();
        var user = new ApplicationUser { Email = "a@test.no", UserName = "a@test.no" };
        userManager.Setup(m => m.FindByEmailAsync("a@test.no")).ReturnsAsync(user);
        signInManager
            .Setup(m => m.PasswordSignInAsync(user, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.LockedOut);

        var result = await controller.Login(new LoginViewModel { Email = "a@test.no", Password = "x" });

        Assert.IsType<ViewResult>(result);
        Assert.Equal("Invalid email or password.", controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
    }

    // Brute force protection: the controller must ask Identity to count failed
    // attempts (lockoutOnFailure: true). Verify() checks how the mock was called.
    [Fact]
    public async Task Login_EnablesLockoutOnFailure()
    {
        var (controller, userManager, signInManager) = CreateController();
        var user = new ApplicationUser { Email = "a@test.no", UserName = "a@test.no" };
        userManager.Setup(m => m.FindByEmailAsync("a@test.no")).ReturnsAsync(user);
        signInManager
            .Setup(m => m.PasswordSignInAsync(user, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Failed);

        await controller.Login(new LoginViewModel { Email = "a@test.no", Password = "wrong" });

        signInManager.Verify(
            m => m.PasswordSignInAsync(user, "wrong", It.IsAny<bool>(), true),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Setup helper. If your existing test file already has a
    // CreateController(), you can reuse that instead.
    // ---------------------------------------------------------------
    // IdentityUser changed to ApplicationUser: the app uses ApplicationUser (FirstName/LastName) since the merge
    private static (AccountController controller,
                    Mock<UserManager<ApplicationUser>> userManager,
                    Mock<SignInManager<ApplicationUser>> signInManager) CreateController()
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

        var controller = new AccountController(userManager.Object, signInManager.Object, Mock.Of<IEmailSender>());

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // A REAL UrlHelper, so Url.IsLocalUrl uses ASP.NET's actual logic
        // instead of something we made up in a mock.
        controller.Url = new UrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));

        return (controller, userManager, signInManager);
    }
}