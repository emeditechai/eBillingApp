using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RestaurantManagementSystem.Models.Authorization;
using RestaurantManagementSystem.Services;
using RestaurantManagementSystem.Utilities;

namespace RestaurantManagementSystem.Filters
{
    /// <summary>
    /// Global authorization filter that prevents direct URL access to menu-backed pages
    /// unless the current user has CanView permission for the matching NavigationMenu.
    /// 
    /// Rules:
    /// - Username "Admin" bypasses all menu restrictions.
    /// - Only enforces when the current request maps to a NavigationMenu entry.
    /// - Only enforces for GET/HEAD (direct page navigation). Mutating requests should
    ///   be protected via explicit action-level permissions.
    /// </summary>
    public sealed class EnforceMenuPermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly RolePermissionService _permissionService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EnforceMenuPermissionFilter> _logger;

        public EnforceMenuPermissionFilter(
            RolePermissionService permissionService,
            IMemoryCache cache,
            ILogger<EnforceMenuPermissionFilter> logger)
        {
            _permissionService = permissionService;
            _cache = cache;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context?.HttpContext;
            var user = http?.User;

            if (http is null)
            {
                return;
            }

            var endpoint = http.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
            {
                return;
            }

            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new ChallengeResult();
                return;
            }

            if (user.IsSuperAdminUser())
            {
                return;
            }

            var method = http.Request?.Method;
            if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
            {
                return;
            }

            var menuCode = await TryResolveMenuCodeAsync(http, context);
            if (string.IsNullOrWhiteSpace(menuCode))
            {
                return;
            }

            var allowed = await _permissionService.HasPermissionAsync(user, menuCode, PermissionAction.View);
            if (!allowed)
            {
                context.Result = new ForbidResult();
            }
        }

        private async Task<string?> TryResolveMenuCodeAsync(HttpContext http, AuthorizationFilterContext context)
        {
            try
            {
                var menus = await GetMenusCachedAsync();
                if (menus.Count == 0)
                {
                    return null;
                }

                var path = NormalizePath(http.Request?.Path.Value);

                // Route pieces (MVC)
                var controller = context.RouteData.Values["controller"]?.ToString();
                var action = context.RouteData.Values["action"]?.ToString();
                var area = context.RouteData.Values["area"]?.ToString();

                // 1) Exact CustomUrl match (most explicit)
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var byCustomUrl = menus.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.CustomUrl)
                        && NormalizePath(m.CustomUrl) == path);
                    if (byCustomUrl != null)
                    {
                        return byCustomUrl.Code;
                    }
                }

                // 2) Exact route match (Area + Controller + Action)
                if (!string.IsNullOrWhiteSpace(controller))
                {
                    var normalizedAction = string.IsNullOrWhiteSpace(action) ? "Index" : action;

                    var byExactRoute = menus.FirstOrDefault(m =>
                        MatchesArea(m.Area, area)
                        && StringEquals(m.ControllerName, controller)
                        && ActionMatches(m.ActionName, normalizedAction));

                    if (byExactRoute != null)
                    {
                        return byExactRoute.Code;
                    }

                    // 3) Controller-level match for other actions (Details/Edit etc.)
                    // If a menu is defined for a controller (commonly Index), protect all GET actions under it.
                    var byController = menus.FirstOrDefault(m =>
                        MatchesArea(m.Area, area)
                        && StringEquals(m.ControllerName, controller)
                        && (string.IsNullOrWhiteSpace(m.ActionName) || m.ActionName.Equals("Index", StringComparison.OrdinalIgnoreCase)));

                    if (byController != null)
                    {
                        return byController.Code;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to resolve menu code for request {Path}", http.Request?.Path.Value);
                return null;
            }
        }

        private async Task<IReadOnlyList<NavigationMenu>> GetMenusCachedAsync()
        {
            const string cacheKey = "NavigationMenus.Active.All";

            if (_cache.TryGetValue(cacheKey, out IReadOnlyList<NavigationMenu> cached) && cached != null)
            {
                return cached;
            }

            var all = await _permissionService.GetAllMenusAsync();
            var active = all.Where(m => m.IsActive).ToList();

            var options = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };

            _cache.Set(cacheKey, active, options);
            return active;
        }

        private static bool MatchesArea(string? menuArea, string? requestArea)
        {
            // Many menus have null/empty area; treat it as "no area".
            if (string.IsNullOrWhiteSpace(menuArea))
            {
                return string.IsNullOrWhiteSpace(requestArea);
            }
            return StringEquals(menuArea, requestArea);
        }

        private static bool ActionMatches(string? menuAction, string requestAction)
        {
            if (string.IsNullOrWhiteSpace(menuAction))
            {
                // When ActionName is empty in DB, treat it as Index.
                return requestAction.Equals("Index", StringComparison.OrdinalIgnoreCase);
            }

            return menuAction.Equals(requestAction, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StringEquals(string? left, string? right)
            => !string.IsNullOrWhiteSpace(left)
               && !string.IsNullOrWhiteSpace(right)
               && left.Equals(right, StringComparison.OrdinalIgnoreCase);

        private static string NormalizePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var path = value.Trim();

            // If stored as absolute URL, only compare the path portion.
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            if (path.Length > 1)
            {
                path = path.TrimEnd('/');
            }

            return path;
        }
    }
}
