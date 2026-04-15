using System.Security.Claims;
using ColdWarHistory.BuildingBlocks.Application;
using Microsoft.AspNetCore.Http;

namespace ColdWarHistory.BuildingBlocks.Api;

public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public CurrentUser GetCurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new CurrentUser(null, null, Array.Empty<string>());
        }

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = principal.FindFirstValue(ClaimTypes.Name);
        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        Guid? userId = Guid.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;

        return new CurrentUser(userId, userName, roles);
    }
}
