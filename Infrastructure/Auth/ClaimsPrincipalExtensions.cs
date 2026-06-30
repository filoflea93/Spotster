using System.Security.Claims;

namespace Spotster.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? TryGetUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        return principal.TryGetUserId()
            ?? throw new UnauthorizedAccessException("Error_UserNotAuthenticated");
    }
}
