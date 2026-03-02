using System.Security.Claims;

namespace ShoppingWebApi.Common
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            // We set both "userId" and sub during token creation.
            var val = user.FindFirstValue("userId") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        public static string? GetRole(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Role);
    }
}