using Heimevernet_Prosjekt.Models.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Heimevernet_Prosjekt.Services;

public static class IdentitySeeder
{
    public const string DevelopmentAdminEmail = "admin@Test.Local";
    public const string DevelopmentUserEmail = "user@Test.local";
    // Test password for development only - never use in production!
    public const string DevelopmentPassword = "test123";

    // CHANGED: creates all roles, not just Admin (help with AI)
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = [RoleNames.Admin, RoleNames.User, RoleNames.Partner];

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the {roleName} role: {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    public static async Task SeedDevelopmentAdminAsync(UserManager<IdentityUser> userManager)
    {
        var user = await FindOrCreateDevelopmentUserAsync(userManager, DevelopmentAdminEmail);

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await EnsureSucceededAsync(
                userManager.AddToRoleAsync(user, RoleNames.Admin),
                $"Could not add the development administrator to the {RoleNames.Admin} role.");
        }
    }

    // CHANGED: the test user also gets the User role (help with AI)
    public static async Task<IdentityUser> SeedDevelopmentUserAsync(UserManager<IdentityUser> userManager)
    {
        var user = await FindOrCreateDevelopmentUserAsync(userManager, DevelopmentUserEmail);

        if (!await userManager.IsInRoleAsync(user, RoleNames.User))
        {
            await EnsureSucceededAsync(
                userManager.AddToRoleAsync(user, RoleNames.User),
                $"Could not add the development user to the {RoleNames.User} role.");
        }

        return user;
    }

    private static async Task<IdentityUser> FindOrCreateDevelopmentUserAsync(
        UserManager<IdentityUser> userManager,
        string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            await EnsureSucceededAsync(
                userManager.CreateAsync(user, DevelopmentPassword),
                "Could not create the development user.");
        }

        return user;
    }

    private static async Task EnsureSucceededAsync(Task<IdentityResult> operation, string failureMessage)
    {
        var result = await operation;
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{failureMessage} {string.Join(", ", result.Errors.Select(error => error.Description))}");
        }
    }
}