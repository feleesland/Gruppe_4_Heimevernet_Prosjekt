using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Account;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-postadresse er påkrevd.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}