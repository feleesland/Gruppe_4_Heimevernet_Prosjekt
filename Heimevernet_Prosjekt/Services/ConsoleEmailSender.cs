using System.Net;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Heimevernet_Prosjekt.Services;

// Writes emails to the log instead of sending them (used in Development) - Teacher: ConsoleEmailSender.cs
public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        logger.LogInformation(
            "Development email to {Email}. Subject: {Subject}. Body: {Body}",
            email,
            subject,
            WebUtility.HtmlDecode(htmlMessage));
        return Task.CompletedTask;
    }
}