namespace Heimevernet_Prosjekt.Models.ViewModels.Admin
{
    // One row on the admin page: name, email and role
    public class UserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}