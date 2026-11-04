using System.ComponentModel.DataAnnotations;

namespace Heimevernet_Prosjekt.Models.Entities;

// A message from an operator to admin about a resource (e.g. "wrong address, please edit") - Docs: Creating and configuring a model (EF Core)
// https://learn.microsoft.com/ef/core/modeling/
public class ResourceReport
{
    public int Id { get; set; }

    public int ResourceId { get; set; }

    // Who sent the message (AspNetUsers.Id) and their email, so admin can see it even if the account is deleted later
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set when admin marks the message as solved; null = still open
    public DateTime? ResolvedAt { get; set; }
}