using Heimevernet_Prosjekt.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Heimevernet_Prosjekt.Models.Authorization
{
    // Needs are the operators' work: admin and operator can do everything, other users nothing
    // Operators work together, so one operator can add lines to a need another operator reported (help with AI)
    // Docs: Resource-based authorization (Operational requirements)
    // https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased

    public class ResourceNeedAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ResourceNeed>
    {
        protected override Task HandleRequirementAsync
            (
                 AuthorizationHandlerContext context,
                 OperationAuthorizationRequirement requirement,
                 ResourceNeed resourceNeed)
        {
            if (context.User.IsInRole(RoleNames.Admin) || context.User.IsInRole(RoleNames.Operator))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

    }
}
