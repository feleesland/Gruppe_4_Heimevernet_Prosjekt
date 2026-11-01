using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Heimevernet_Prosjekt.Models.Authorization
{
    // What the user wants to do with a resource - Docs: Resource-based authorization (Operational requirements)
    // https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
    public static class ResourceOperations
    {
        public static OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };
        public static OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
        public static OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
    }
}