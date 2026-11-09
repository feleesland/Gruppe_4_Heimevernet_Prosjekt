using Heimevernet_Prosjekt.Models.Entities;
using Heimevernet_Prosjekt.Security.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Heimevernet_Prosjekt.Services;

// Creates roles and test users at startup - Teacher: IdentitySeeder.cs
public static class IdentitySeeder
{
    public const string DevelopmentAdminEmail = "admin@Test.Local";
    public const string DevelopmentOperatorEmail = "operator@Test.local";
    public const string DevelopmentUserEmail = "user@Test.local";
    // Test password for development only - never use in production!
    public const string DevelopmentPassword = "test123";

    // Creates all roles, not just Admin
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = [RoleNames.Admin, RoleNames.Operator, RoleNames.User, RoleNames.Partner];

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

    public static async Task SeedDevelopmentAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var user = await FindOrCreateDevelopmentUserAsync(userManager, DevelopmentAdminEmail, "Test", "Admin");

        if (!await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            await EnsureSucceededAsync(
                userManager.AddToRoleAsync(user, RoleNames.Admin),
                $"Could not add the development administrator to the {RoleNames.Admin} role.");
        }
    }

    // The test user also gets the User role
    public static async Task<ApplicationUser> SeedDevelopmentUserAsync(UserManager<ApplicationUser> userManager)
    {
        var user = await FindOrCreateDevelopmentUserAsync(userManager, DevelopmentUserEmail, "Test", "Bruker");

        if (!await userManager.IsInRoleAsync(user, RoleNames.User))
        {
            await EnsureSucceededAsync(
                userManager.AddToRoleAsync(user, RoleNames.User),
                $"Could not add the development user to the {RoleNames.User} role.");
        }

        return user;
    }

    private static async Task<ApplicationUser> FindOrCreateDevelopmentUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string firstName,
        string lastName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName
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