using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.ViewModels.Account
{
    public class EditProfileViewModel
    {
        [Phone(ErrorMessage = "Telefonnummeret er ikke gyldig.")]
        [StringLength(20, ErrorMessage = "Telefonnummeret er for langt.")]
        public string? Phone { get; set; }

        public string? ActorKind { get; set; }

        public List<string> SelectedSkills { get; set; } = new();
    }
}
