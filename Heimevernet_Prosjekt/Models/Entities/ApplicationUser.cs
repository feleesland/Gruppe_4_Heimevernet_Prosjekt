using Microsoft.AspNetCore.Identity;

namespace Heimevernet_Prosjekt.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // User with first and last name, shown on the website instead of the email - Docs: Add, download, and delete custom user data
        // https://learn.microsoft.com/aspnet/core/security/authentication/add-user-data
        [PersonalData]
        public string FirstName { get; set; } = string.Empty;

        [PersonalData]
        public string LastName { get; set; } = string.Empty;
    }
}
