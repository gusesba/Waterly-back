using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Water.Infrastructure.Identity;

namespace Water.Api.Features.Competition;

public sealed class ContestAdminRequirement : IAuthorizationRequirement;

public sealed class ContestAdminAuthorizationHandler(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : AuthorizationHandler<ContestAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ContestAdminRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;
        var user = await userManager.FindByIdAsync(userId);
        var configuredEmails = configuration.GetSection("Administration:ContestAdminEmails").Get<string[]>() ?? [];
        if (user?.Email is not null && configuredEmails.Contains(user.Email, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);
    }
}
