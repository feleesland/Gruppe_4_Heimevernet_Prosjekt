
// Where the SendGrid API key is read from (user secrets, never in the code) - Docs: Account confirmation and password recovery
// https://learn.microsoft.com/aspnet/core/security/authentication/accconfirm

namespace Heimevernet_Prosjekt.Services
{
    public class AuthenticationMessageSenderOptions
    {
            public string? SendGridKey { get; set; }
    }
}
