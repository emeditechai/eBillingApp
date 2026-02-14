using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestaurantManagementSystem.Models.Authorization;
using RestaurantManagementSystem.Utilities;
using RestaurantManagementSystem.ViewModels.Authorization;

namespace RestaurantManagementSystem.Services
{
    public class RolePermissionService
    {
        private readonly string _connectionString;
        private readonly ILogger<RolePermissionService> _logger;

        public RolePermissionService(IConfiguration configuration, ILogger<RolePermissionService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration), "Missing connection string 'DefaultConnection'.");
            _logger = logger;
        }

        private SqlConnection CreateConnection() => new(_connectionString);

        #region Navigation queries
        public async Task<List<NavigationMenu>> GetAllMenusAsync()
        {
            var menus = new List<NavigationMenu>();
            const string sql = @"SELECT Id, Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
                                           RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
                                           ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
                                    FROM dbo.NavigationMenus
                                    ORDER BY DisplayOrder, DisplayName";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                menus.Add(MapMenu(reader));
            }

            return menus;
        }

        private static NavigationMenu MapMenu(SqlDataReader reader) => new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Code = reader.GetString(reader.GetOrdinal("Code")),
            ParentCode = reader["ParentCode"] as string,
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            Description = reader["Description"] as string,
            Area = reader["Area"] as string,
            ControllerName = reader["ControllerName"] as string,
            ActionName = reader["ActionName"] as string,
            RouteValues = reader["RouteValues"] as string,
            CustomUrl = reader["CustomUrl"] as string,
            IconCss = reader["IconCss"] as string,
            DisplayOrder = reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            IsVisible = reader.GetBoolean(reader.GetOrdinal("IsVisible")),
            ThemeColor = reader["ThemeColor"] as string,
            ShortcutHint = reader["ShortcutHint"] as string,
            OpenInNewTab = reader.GetBoolean(reader.GetOrdinal("OpenInNewTab")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };

        public async Task<List<NavigationMenuNode>> GetNavigationTreeForUserAsync(ClaimsPrincipal user)
        {
            var userId = user?.GetUserId();
            var activeRoleId = user?.GetActiveRoleId();
            var isSuperAdmin = IsSuperAdminUser(user);

            var allMenus = await GetAllMenusAsync();
            var visibleMenus = allMenus.Where(m => m.IsActive && m.IsVisible).ToList();
            HashSet<string> allowedCodes;

            if (isSuperAdmin || userId is null)
            {
                allowedCodes = visibleMenus.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            else if (activeRoleId.HasValue)
            {
                allowedCodes = await GetMenuCodesForRoleAsync(activeRoleId.Value);
                EnsureParentsIncluded(allMenus, allowedCodes);
            }
            else
            {
                allowedCodes = await GetMenuCodesForUserAsync(userId.Value);
                EnsureParentsIncluded(allMenus, allowedCodes);
            }

            return BuildMenuTree(visibleMenus, allowedCodes, isSuperAdmin);
        }

        private async Task<HashSet<string>> GetMenuCodesForRoleAsync(int roleId)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"SELECT DISTINCT nm.Code
                                   FROM dbo.NavigationMenus nm
                                   INNER JOIN dbo.RoleMenuPermissions rmp ON rmp.MenuId = nm.Id AND rmp.CanView = 1
                                   WHERE rmp.RoleId = @RoleId AND nm.IsActive = 1 AND nm.IsVisible = 1";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RoleId", roleId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                codes.Add(reader.GetString(0));
            }

            return codes;
        }

        private async Task<HashSet<string>> GetMenuCodesForUserAsync(int userId)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"SELECT DISTINCT nm.Code
                                   FROM dbo.NavigationMenus nm
                                   INNER JOIN dbo.RoleMenuPermissions rmp ON rmp.MenuId = nm.Id AND rmp.CanView = 1
                                   INNER JOIN dbo.UserRoles ur ON ur.RoleId = rmp.RoleId
                                   WHERE ur.UserId = @UserId AND nm.IsActive = 1 AND nm.IsVisible = 1";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                codes.Add(reader.GetString(0));
            }
            return codes;
        }

        private static void EnsureParentsIncluded(IEnumerable<NavigationMenu> menus, HashSet<string> allowedCodes)
        {
            var lookup = menus.ToDictionary(m => m.Code, m => m, StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(allowedCodes);

            while (queue.Count > 0)
            {
                var code = queue.Dequeue();
                if (!lookup.TryGetValue(code, out var menu) || string.IsNullOrWhiteSpace(menu.ParentCode))
                {
                    continue;
                }

                if (allowedCodes.Add(menu.ParentCode))
                {
                    queue.Enqueue(menu.ParentCode);
                }
            }
        }

        private static List<NavigationMenuNode> BuildMenuTree(List<NavigationMenu> menus, HashSet<string> allowedCodes, bool includeEverything)
        {
            var grouped = menus.GroupBy(m => m.ParentCode ?? string.Empty)
                                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.DisplayOrder).ToList(), StringComparer.OrdinalIgnoreCase);

            List<NavigationMenuNode> BuildBranch(string parentCode)
            {
                var key = parentCode ?? string.Empty;
                if (!grouped.TryGetValue(key, out var children))
                {
                    return new List<NavigationMenuNode>();
                }

                var nodes = new List<NavigationMenuNode>();
                foreach (var child in children)
                {
                    var childNodes = BuildBranch(child.Code);
                    var canView = includeEverything || allowedCodes.Contains(child.Code);
                    var include = canView || childNodes.Count > 0;
                    if (!include)
                    {
                        continue;
                    }

                    nodes.Add(new NavigationMenuNode
                    {
                        Id = child.Id,
                        Code = child.Code,
                        ParentCode = child.ParentCode,
                        DisplayName = child.DisplayName,
                        Description = child.Description,
                        Area = child.Area,
                        ControllerName = child.ControllerName,
                        ActionName = child.ActionName,
                        RouteValues = child.RouteValues,
                        CustomUrl = child.CustomUrl,
                        IconCss = child.IconCss,
                        DisplayOrder = child.DisplayOrder,
                        ThemeColor = child.ThemeColor,
                        ShortcutHint = child.ShortcutHint,
                        OpenInNewTab = child.OpenInNewTab,
                        CanView = canView,
                        Children = childNodes
                    });
                }
                return nodes;
            }

            return BuildBranch(null);
        }
        #endregion

        #region Menu management
        public async Task<int> CreateMenuAsync(MenuEditViewModel model, int? userId)
        {
            if (model is null) throw new ArgumentNullException(nameof(model));
            model.Code = model.Code?.Trim().ToUpperInvariant();

            const string sql = @"INSERT INTO dbo.NavigationMenus
                                  (Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName, RouteValues, CustomUrl,
                                   IconCss, DisplayOrder, IsActive, IsVisible, ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt)
                                  VALUES
                                  (@Code, @ParentCode, @DisplayName, @Description, @Area, @ControllerName, @ActionName, @RouteValues, @CustomUrl,
                                   @IconCss, @DisplayOrder, @IsActive, @IsVisible, @ThemeColor, @ShortcutHint, @OpenInNewTab, SYSUTCDATETIME(), SYSUTCDATETIME());
                                  SELECT SCOPE_IDENTITY();";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            AddMenuParameters(command, model);
            var result = await command.ExecuteScalarAsync();
            var newId = Convert.ToInt32(result);

            if (await IsAdministratorRoleAvailableAsync())
            {
                await GrantFullPermissionsToRoleAsync(null, new[] { newId });
            }

            _logger?.LogInformation("Navigation menu {Code} created by user {User}", model.Code, userId);
            return newId;
        }

        public async Task UpdateMenuAsync(MenuEditViewModel model, int? userId)
        {
            if (model?.Id is null) throw new ArgumentNullException(nameof(model.Id));
            model.Code = model.Code?.Trim().ToUpperInvariant();

            const string sql = @"UPDATE dbo.NavigationMenus
                                  SET ParentCode = @ParentCode,
                                      DisplayName = @DisplayName,
                                      Description = @Description,
                                      Area = @Area,
                                      ControllerName = @ControllerName,
                                      ActionName = @ActionName,
                                      RouteValues = @RouteValues,
                                      CustomUrl = @CustomUrl,
                                      IconCss = @IconCss,
                                      DisplayOrder = @DisplayOrder,
                                      IsActive = @IsActive,
                                      IsVisible = @IsVisible,
                                      ThemeColor = @ThemeColor,
                                      ShortcutHint = @ShortcutHint,
                                      OpenInNewTab = @OpenInNewTab,
                                      UpdatedAt = SYSUTCDATETIME()
                                  WHERE Id = @Id";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            AddMenuParameters(command, model);
            command.Parameters.AddWithValue("@Id", model.Id.Value);
            await command.ExecuteNonQueryAsync();

            _logger?.LogInformation("Navigation menu {Id} updated by user {User}", model.Id, userId);
        }

        private static void AddMenuParameters(SqlCommand command, MenuEditViewModel model)
        {
            command.Parameters.AddWithValue("@Code", model.Code?.Trim());
            command.Parameters.AddWithValue("@ParentCode", (object?)model.ParentCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@DisplayName", model.DisplayName?.Trim());
            command.Parameters.AddWithValue("@Description", (object?)model.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@Area", (object?)model.Area ?? DBNull.Value);
            command.Parameters.AddWithValue("@ControllerName", (object?)model.ControllerName ?? DBNull.Value);
            command.Parameters.AddWithValue("@ActionName", (object?)model.ActionName ?? DBNull.Value);
            command.Parameters.AddWithValue("@RouteValues", (object?)model.RouteValues ?? DBNull.Value);
            command.Parameters.AddWithValue("@CustomUrl", (object?)model.CustomUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@IconCss", (object?)model.IconCss ?? DBNull.Value);
            command.Parameters.AddWithValue("@DisplayOrder", model.DisplayOrder);
            command.Parameters.AddWithValue("@IsActive", model.IsActive);
            command.Parameters.AddWithValue("@IsVisible", model.IsVisible);
            command.Parameters.AddWithValue("@ThemeColor", (object?)model.ThemeColor ?? DBNull.Value);
            command.Parameters.AddWithValue("@ShortcutHint", (object?)model.ShortcutHint ?? DBNull.Value);
            command.Parameters.AddWithValue("@OpenInNewTab", model.OpenInNewTab);
        }
        #endregion

        #region Role menu mapping
        public async Task<List<RoleMenuTreeNode>> GetRoleMenuTreeAsync(int roleId)
        {
            var menus = await GetAllMenusAsync();
            const string sql = @"SELECT MenuId FROM dbo.RoleMenuPermissions WHERE RoleId = @RoleId AND CanView = 1";
            var assigned = new HashSet<int>();

            await using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@RoleId", roleId);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    assigned.Add(reader.GetInt32(0));
                }
            }

            var nodes = menus.Where(m => m.IsActive && m.IsVisible)
                             .Select(m => new RoleMenuTreeNode
                             {
                                 MenuId = m.Id,
                                 Code = m.Code,
                                 DisplayName = m.DisplayName,
                                 ParentCode = m.ParentCode,
                                 IsAssigned = assigned.Contains(m.Id)
                             })
                             .ToList();

            return BuildRoleTree(nodes);
        }

        private static List<RoleMenuTreeNode> BuildRoleTree(List<RoleMenuTreeNode> nodes)
        {
            var lookup = nodes.GroupBy(n => n.ParentCode ?? string.Empty)
                              .ToDictionary(g => g.Key, g => g.OrderBy(n => n.DisplayName).ToList(), StringComparer.OrdinalIgnoreCase);

            List<RoleMenuTreeNode> Build(string parentCode)
            {
                var key = parentCode ?? string.Empty;
                if (!lookup.TryGetValue(key, out var children))
                {
                    return new List<RoleMenuTreeNode>();
                }

                foreach (var child in children)
                {
                    child.Children = Build(child.Code);
                }
                return children;
            }

            return Build(null);
        }

        public async Task SaveRoleMenuAssignmentsAsync(int roleId, IEnumerable<int> menuIds, int? updatedBy)
        {
            menuIds ??= Array.Empty<int>();
            var menuSet = new HashSet<int>(menuIds);

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var existing = new HashSet<int>();
                await using (var selectCmd = new SqlCommand("SELECT MenuId FROM dbo.RoleMenuPermissions WHERE RoleId = @RoleId", connection, (SqlTransaction)transaction))
                {
                    selectCmd.Parameters.AddWithValue("@RoleId", roleId);
                    await using var reader = await selectCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        existing.Add(reader.GetInt32(0));
                    }
                }

                var toRemove = existing.Except(menuSet).ToList();
                var toAdd = menuSet.Except(existing).ToList();
                var toKeep = existing.Intersect(menuSet).ToList();

                if (toRemove.Count > 0)
                {
                    foreach (var chunk in Chunk(toRemove, 50))
                    {
                        var ids = string.Join(",", chunk);
                        var deleteSql = $"DELETE FROM dbo.RoleMenuPermissions WHERE RoleId = @RoleId AND MenuId IN ({ids})";
                        await using var deleteCmd = new SqlCommand(deleteSql, connection, (SqlTransaction)transaction);
                        deleteCmd.Parameters.AddWithValue("@RoleId", roleId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }

                if (toKeep.Count > 0)
                {
                    foreach (var chunk in Chunk(toKeep, 50))
                    {
                        var ids = string.Join(",", chunk);
                        var updateSql = $"UPDATE dbo.RoleMenuPermissions SET CanView = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy WHERE RoleId = @RoleId AND MenuId IN ({ids})";
                        await using var updateCmd = new SqlCommand(updateSql, connection, (SqlTransaction)transaction);
                        updateCmd.Parameters.AddWithValue("@RoleId", roleId);
                        updateCmd.Parameters.AddWithValue("@UpdatedBy", (object?)updatedBy ?? DBNull.Value);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }

                if (toAdd.Count > 0)
                {
                    foreach (var menuId in toAdd)
                    {
                        const string insertSql = @"INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                                                   VALUES (@RoleId, @MenuId, 1, 0, 0, 0, 0, 0, 0, SYSUTCDATETIME(), @UpdatedBy, SYSUTCDATETIME(), @UpdatedBy)";
                        await using var insertCmd = new SqlCommand(insertSql, connection, (SqlTransaction)transaction);
                        insertCmd.Parameters.AddWithValue("@RoleId", roleId);
                        insertCmd.Parameters.AddWithValue("@MenuId", menuId);
                        insertCmd.Parameters.AddWithValue("@UpdatedBy", (object?)updatedBy ?? DBNull.Value);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError(ex, "Error saving role-menu assignments for role {RoleId}", roleId);
                throw;
            }
        }
        #endregion

        #region Permission matrix
        public async Task<List<RolePermissionMatrixRow>> GetPermissionMatrixAsync(int roleId, bool assignedMenusOnly = false)
        {
            var results = new List<RolePermissionMatrixRow>();
            const string sql = @"WITH AssignedBase AS (
                                        SELECT DISTINCT nm.Id, nm.Code, nm.ParentCode
                                        FROM dbo.NavigationMenus nm
                                        INNER JOIN dbo.RoleMenuPermissions rmp ON rmp.MenuId = nm.Id AND rmp.RoleId = @RoleId AND rmp.CanView = 1
                                    ),
                                    RecursiveMenus AS (
                                        SELECT Id, Code, ParentCode FROM AssignedBase
                                        UNION ALL
                                        SELECT parent.Id, parent.Code, parent.ParentCode
                                        FROM dbo.NavigationMenus parent
                                        INNER JOIN RecursiveMenus child ON child.ParentCode = parent.Code
                                    )
                                    SELECT nm.Id, nm.Code, nm.DisplayName, nm.ParentCode,
                                           ISNULL(rmp.CanView, 0) AS CanView,
                                           ISNULL(rmp.CanAdd, 0) AS CanAdd,
                                           ISNULL(rmp.CanEdit, 0) AS CanEdit,
                                           ISNULL(rmp.CanDelete, 0) AS CanDelete,
                                           ISNULL(rmp.CanApprove, 0) AS CanApprove,
                                           ISNULL(rmp.CanPrint, 0) AS CanPrint,
                                           ISNULL(rmp.CanExport, 0) AS CanExport
                                    FROM dbo.NavigationMenus nm
                                    LEFT JOIN dbo.RoleMenuPermissions rmp ON rmp.MenuId = nm.Id AND rmp.RoleId = @RoleId
                                    WHERE nm.IsActive = 1
                                      AND (@AssignedOnly = 0 OR nm.Id IN (SELECT Id FROM RecursiveMenus))
                                    ORDER BY COALESCE(nm.ParentCode, nm.Code), nm.DisplayOrder";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RoleId", roleId);
            command.Parameters.AddWithValue("@AssignedOnly", assignedMenusOnly ? 1 : 0);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new RolePermissionMatrixRow
                {
                    MenuId = reader.GetInt32(0),
                    MenuCode = reader.GetString(1),
                    MenuName = reader.GetString(2),
                    ParentCode = reader[3] as string,
                    CanView = reader.GetBoolean(4),
                    CanAdd = reader.GetBoolean(5),
                    CanEdit = reader.GetBoolean(6),
                    CanDelete = reader.GetBoolean(7),
                    CanApprove = reader.GetBoolean(8),
                    CanPrint = reader.GetBoolean(9),
                    CanExport = reader.GetBoolean(10)
                });
            }

            return results;
        }

        public async Task SavePermissionMatrixAsync(int roleId, IEnumerable<RolePermissionMatrixRow> rows, int? updatedBy)
        {
            rows ??= Array.Empty<RolePermissionMatrixRow>();

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var row in rows)
                {
                    var enforceView = row.CanView || row.CanAdd || row.CanEdit || row.CanDelete || row.CanApprove || row.CanPrint || row.CanExport;
                    var canView = row.CanView || enforceView;

                    const string upsert = @"IF EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions WHERE RoleId = @RoleId AND MenuId = @MenuId)
                                            BEGIN
                                                UPDATE dbo.RoleMenuPermissions
                                                SET CanView = @CanView,
                                                    CanAdd = @CanAdd,
                                                    CanEdit = @CanEdit,
                                                    CanDelete = @CanDelete,
                                                    CanApprove = @CanApprove,
                                                    CanPrint = @CanPrint,
                                                    CanExport = @CanExport,
                                                    UpdatedAt = SYSUTCDATETIME(),
                                                    UpdatedBy = @UpdatedBy
                                                WHERE RoleId = @RoleId AND MenuId = @MenuId;
                                            END
                                            ELSE IF @CanView = 1
                                            BEGIN
                                                INSERT INTO dbo.RoleMenuPermissions
                                                    (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                                                VALUES (@RoleId, @MenuId, @CanView, @CanAdd, @CanEdit, @CanDelete, @CanApprove, @CanPrint, @CanExport, SYSUTCDATETIME(), @UpdatedBy, SYSUTCDATETIME(), @UpdatedBy);
                                            END";

                    await using var cmd = new SqlCommand(upsert, connection, (SqlTransaction)transaction);
                    cmd.Parameters.AddWithValue("@RoleId", roleId);
                    cmd.Parameters.AddWithValue("@MenuId", row.MenuId);
                    cmd.Parameters.AddWithValue("@CanView", canView ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanAdd", row.CanAdd ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanEdit", row.CanEdit ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanDelete", row.CanDelete ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanApprove", row.CanApprove ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanPrint", row.CanPrint ? 1 : 0);
                    cmd.Parameters.AddWithValue("@CanExport", row.CanExport ? 1 : 0);
                    cmd.Parameters.AddWithValue("@UpdatedBy", (object?)updatedBy ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();

                    if (!canView)
                    {
                        const string cleanup = "DELETE FROM dbo.RoleMenuPermissions WHERE RoleId = @RoleId AND MenuId = @MenuId";
                        await using var cleanupCmd = new SqlCommand(cleanup, connection, (SqlTransaction)transaction);
                        cleanupCmd.Parameters.AddWithValue("@RoleId", roleId);
                        cleanupCmd.Parameters.AddWithValue("@MenuId", row.MenuId);
                        await cleanupCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger?.LogError(ex, "Error saving permission matrix for role {RoleId}", roleId);
                throw;
            }
        }
        #endregion

        #region Permission evaluation
        public async Task<PermissionSet> GetPermissionsForUserAsync(ClaimsPrincipal user, string menuCode)
        {
            var permission = new PermissionSet();
            if (user == null || string.IsNullOrWhiteSpace(menuCode))
            {
                return permission;
            }

            if (IsSuperAdminUser(user))
            {
                return PermissionSet.FullAccess;
            }

            var activeRoleId = user.GetActiveRoleId();
            if (activeRoleId.HasValue)
            {
                const string sqlRole = @"SELECT TOP 1 rmp.CanView, rmp.CanAdd, rmp.CanEdit, rmp.CanDelete, rmp.CanApprove, rmp.CanPrint, rmp.CanExport
                                           FROM dbo.RoleMenuPermissions rmp
                                           INNER JOIN dbo.NavigationMenus nm ON nm.Id = rmp.MenuId
                                           WHERE rmp.RoleId = @RoleId AND nm.Code = @Code";

                await using var connection = CreateConnection();
                await connection.OpenAsync();
                await using var command = new SqlCommand(sqlRole, connection);
                command.Parameters.AddWithValue("@RoleId", activeRoleId.Value);
                command.Parameters.AddWithValue("@Code", menuCode);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    permission.CanView = reader.GetBoolean(0);
                    permission.CanAdd = reader.GetBoolean(1);
                    permission.CanEdit = reader.GetBoolean(2);
                    permission.CanDelete = reader.GetBoolean(3);
                    permission.CanApprove = reader.GetBoolean(4);
                    permission.CanPrint = reader.GetBoolean(5);
                    permission.CanExport = reader.GetBoolean(6);
                }

                return permission;
            }

            var userId = user.GetUserId();
            if (userId is null)
            {
                return permission;
            }

            const string sql = @"SELECT TOP 1 rmp.CanView, rmp.CanAdd, rmp.CanEdit, rmp.CanDelete, rmp.CanApprove, rmp.CanPrint, rmp.CanExport
                                   FROM dbo.RoleMenuPermissions rmp
                                   INNER JOIN dbo.UserRoles ur ON ur.RoleId = rmp.RoleId
                                   INNER JOIN dbo.NavigationMenus nm ON nm.Id = rmp.MenuId
                                   WHERE ur.UserId = @UserId AND nm.Code = @Code";

            await using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", userId.Value);
                command.Parameters.AddWithValue("@Code", menuCode);
                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    permission.CanView = reader.GetBoolean(0);
                    permission.CanAdd = reader.GetBoolean(1);
                    permission.CanEdit = reader.GetBoolean(2);
                    permission.CanDelete = reader.GetBoolean(3);
                    permission.CanApprove = reader.GetBoolean(4);
                    permission.CanPrint = reader.GetBoolean(5);
                    permission.CanExport = reader.GetBoolean(6);
                }
            }

            return permission;
        }

        public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuCode, PermissionAction action)
        {
            if (user == null)
            {
                return false;
            }

            if (IsSuperAdminUser(user))
            {
                return true;
            }

            var permissions = await GetPermissionsForUserAsync(user, menuCode);
            return action switch
            {
                PermissionAction.View => permissions.CanView,
                PermissionAction.Add => permissions.CanAdd,
                PermissionAction.Edit => permissions.CanEdit,
                PermissionAction.Delete => permissions.CanDelete,
                PermissionAction.Approve => permissions.CanApprove,
                PermissionAction.Print => permissions.CanPrint,
                PermissionAction.Export => permissions.CanExport,
                _ => false
            };
        }
        #endregion

        #region Helpers
        private static IEnumerable<List<int>> Chunk(IEnumerable<int> source, int size)
        {
            var chunk = new List<int>(size);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk;
                    chunk = new List<int>(size);
                }
            }
            if (chunk.Count > 0)
            {
                yield return chunk;
            }
        }

        private async Task<bool> IsAdministratorRoleAsync(int roleId)
        {
            const string sql = "SELECT CASE WHEN Name = 'Administrator' THEN 1 ELSE 0 END FROM dbo.Roles WHERE Id = @RoleId";
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@RoleId", roleId);
            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) == 1;
        }

        private async Task<bool> IsAdministratorRoleAvailableAsync()
        {
            const string sql = "SELECT COUNT(1) FROM dbo.Roles WHERE Name = 'Administrator'";
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        private static bool IsSuperAdminUser(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var username = user.Identity?.Name;
            return !string.IsNullOrWhiteSpace(username)
                   && username.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> IsAdministratorUserAsync(int userId)
        {
            const string sql = @"SELECT TOP 1 1
                                   FROM dbo.UserRoles ur
                                   INNER JOIN dbo.Roles r ON r.Id = ur.RoleId
                                   WHERE ur.UserId = @UserId AND r.Name = 'Administrator'";

            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            var result = await command.ExecuteScalarAsync();
            return result != null;
        }

        private async Task GrantFullPermissionsToRoleAsync(int? roleId, IEnumerable<int> specificMenuIds = null)
        {
            const string roleSql = "SELECT Id FROM dbo.Roles WHERE Name = 'Administrator'";
            var targetRoleId = roleId;

            await using var connection = CreateConnection();
            await connection.OpenAsync();

            if (targetRoleId is null)
            {
                await using var roleCmd = new SqlCommand(roleSql, connection);
                var result = await roleCmd.ExecuteScalarAsync();
                if (result == null)
                {
                    return;
                }
                targetRoleId = Convert.ToInt32(result);
            }

            var menuFilter = specificMenuIds?.ToList();
            string insertSql = @"INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                                 SELECT @RoleId, nm.Id, 1,1,1,1,1,1,1, SYSUTCDATETIME(), 0, SYSUTCDATETIME(), 0
                                 FROM dbo.NavigationMenus nm
                                 WHERE NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions rmp WHERE rmp.RoleId = @RoleId AND rmp.MenuId = nm.Id)";
            if (menuFilter is { Count: > 0 })
            {
                var ids = string.Join(",", menuFilter);
                insertSql += $" AND nm.Id IN ({ids})";
            }

            await using (var insertCmd = new SqlCommand(insertSql, connection))
            {
                insertCmd.Parameters.AddWithValue("@RoleId", targetRoleId.Value);
                await insertCmd.ExecuteNonQueryAsync();
            }

            if (menuFilter is null)
            {
                const string updateSql = @"UPDATE dbo.RoleMenuPermissions
                                             SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1,
                                                 CanApprove = 1, CanPrint = 1, CanExport = 1,
                                                 UpdatedAt = SYSUTCDATETIME(), UpdatedBy = 0
                                             WHERE RoleId = @RoleId";
                await using var updateCmd = new SqlCommand(updateSql, connection);
                updateCmd.Parameters.AddWithValue("@RoleId", targetRoleId.Value);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }
        #endregion
    }
}
