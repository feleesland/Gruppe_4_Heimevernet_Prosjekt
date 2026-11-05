using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities;

// One line in the need's log: who did what, and when.
// The name is saved as text, so the log still shows who it was if the user is deleted later.
public class ResourceNeedLog
{
    public int Id { get; set; }

    public int ResourceNeedId { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;
}