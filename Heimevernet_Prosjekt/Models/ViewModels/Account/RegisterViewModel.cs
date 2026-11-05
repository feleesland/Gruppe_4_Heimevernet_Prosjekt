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
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Telefonnummer")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passordene matcher ikke.")]
    [Display(Name = "Gjenta passord")]
    public string ConfirmPassword { get; set; } = string.Empty;
}