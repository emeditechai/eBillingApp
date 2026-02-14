using System;
using System.Security.Claims;

namespace RestaurantManagementSystem.Utilities
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            if (user == null)
            {
                return null;
            }

            var identifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(identifier, out var userId))
            {
                return userId;
            }

            return null;
        }

        public static int? GetActiveRoleId(this ClaimsPrincipal user)
        {
            var value = user?.FindFirst("ActiveRoleId")?.Value;
            return int.TryParse(value, out var roleId) ? roleId : null;
        }

        public static string? GetActiveRoleName(this ClaimsPrincipal user)
            => user?.FindFirst("ActiveRoleName")?.Value;

        public static bool IsSuperAdminUser(this ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var username = user.Identity?.Name;
            return !string.IsNullOrWhiteSpace(username)
                   && username.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasConfirmedRoleSelection(this ClaimsPrincipal user)
        {
            var claimValue = user?.FindFirst("RoleSelectionConfirmed")?.Value;
            return claimValue != null && claimValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
