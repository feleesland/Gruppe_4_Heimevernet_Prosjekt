using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;

namespace Heimevernet_Prosjekt.Security.Authorization
{
    // Admin: everything, owner: everything, operator: read only - Docs: Resource-based authorization (Operational requirements)
    // https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
    public class ResourceAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Resource>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            Resource resource)
        {
            // Admin can do everything
            if (context.User.IsInRole(RoleNames.Admin))
            {
                context.Succeed(requirement);
            }
            // The owner can do everything with their own resources
            else if (resource.OwnerId == context.User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                context.Succeed(requirement);
            }
            // Operator can only read all resources
            else if (context.User.IsInRole(RoleNames.Operator)
                && requirement.Name == ResourceOperations.Read.Name)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}