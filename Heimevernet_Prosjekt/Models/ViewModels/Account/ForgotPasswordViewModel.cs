using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Account;

public sealed class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}