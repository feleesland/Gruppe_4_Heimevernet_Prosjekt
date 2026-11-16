using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Account;

// Registration form - Teacher: RegisterViewModel.cs. FirstName, LastName and PhoneNumber added - Docs: Model validation in ASP.NET Core MVC
// https://learn.microsoft.com/aspnet/core/mvc/models/validation
public sealed class RegisterViewModel
{
    [Required]
    [StringLength(50)]
    [Display(Name = "Fornavn")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "Etternavn")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Telefonnummer")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Passord")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passordene matcher ikke.")]
    [Display(Name = "Gjenta passord")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Demo only (Auth:AllowRoleChoice): the role the visitor wants to try. The server only accepts User or Operator,
    // and only when the switch is on - otherwise every new account is User (help with AI)
    // Docs: https://learn.microsoft.com/aspnet/core/security/authorization/roles
    public string? DemoRole { get; set; }
}