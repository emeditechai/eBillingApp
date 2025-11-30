using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    public class MenuSyncController : Controller
    {
        private readonly string _sourceConnectionString;

        public MenuSyncController(IConfiguration configuration)
        {
            _sourceConnectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: MenuSync
        public async Task<IActionResult> Index()
        {
            // Load saved targets
            var targets = await GetSavedTargets();
            ViewBag.SavedTargets = targets;
            return View();
        }

        // GET: MenuSync/GetSavedTargets
        [HttpGet]
        public async Task<IActionResult> GetSavedTargets()
        {
            try
            {
                var targets = new List<MenuSyncTarget>();

                await using var connection = new SqlConnection(_sourceConnectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        Id, 
                        ServerName, 
                        ServerIP, 
                        DatabaseName, 
                        Username,
                        IsActive,
                        IsDefault,
                        Description,
                        LastSyncDate,
                        LastSyncStatus
                    FROM MenuSyncTargets 
                    WHERE IsActive = 1
                    ORDER BY IsDefault DESC, ServerName";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    targets.Add(new MenuSyncTarget
                    {
                        Id = reader.GetInt32(0),
                        ServerName = reader["ServerName"]?.ToString() ?? "",
                        ServerIP = reader["ServerIP"]?.ToString() ?? "",
                        DatabaseName = reader["DatabaseName"]?.ToString() ?? "",
                        Username = reader["Username"]?.ToString(),
                        IsActive = reader.GetBoolean(5),
                        IsDefault = reader.GetBoolean(6),
                        Description = reader["Description"]?.ToString(),
                        LastSyncDate = reader["LastSyncDate"] != DBNull.Value ? reader.GetDateTime(8) : null,
                        LastSyncStatus = reader["LastSyncStatus"]?.ToString()
                    });
                }

                return Json(new { success = true, data = targets });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to load saved targets: {ex.Message}" });
            }
        }

        // POST: MenuSync/SaveTarget
        [HttpPost]
        public async Task<IActionResult> SaveTarget([FromBody] SaveTargetRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ServerIP) || string.IsNullOrWhiteSpace(request.DatabaseName))
                {
                    return Json(new { success = false, message = "Server IP and Database Name are required." });
                }

                await using var connection = new SqlConnection(_sourceConnectionString);
                await connection.OpenAsync();

                // If setting as default, unset other defaults
                if (request.IsDefault)
                {
                    var unsetDefaultQuery = "UPDATE MenuSyncTargets SET IsDefault = 0";
                    await using var unsetCommand = new SqlCommand(unsetDefaultQuery, connection);
                    await unsetCommand.ExecuteNonQueryAsync();
                }

                string query;
                if (request.Id.HasValue && request.Id.Value > 0)
                {
                    // Update existing
                    query = @"
                        UPDATE MenuSyncTargets 
                        SET ServerName = @ServerName,
                            ServerIP = @ServerIP,
                            DatabaseName = @DatabaseName,
                            Username = @Username,
                            Password = @Password,
                            IsDefault = @IsDefault,
                            Description = @Description,
                            UpdatedAt = GETDATE()
                        WHERE Id = @Id";
                }
                else
                {
                    // Insert new
                    query = @"
                        INSERT INTO MenuSyncTargets (
                            ServerName, ServerIP, DatabaseName, Username, Password, 
                            IsActive, IsDefault, Description
                        ) 
                        VALUES (
                            @ServerName, @ServerIP, @DatabaseName, @Username, @Password,
                            1, @IsDefault, @Description
                        )";
                }

                await using var command = new SqlCommand(query, connection);
                if (request.Id.HasValue && request.Id.Value > 0)
                {
                    command.Parameters.AddWithValue("@Id", request.Id.Value);
                }
                command.Parameters.AddWithValue("@ServerName", request.ServerName ?? request.ServerIP);
                command.Parameters.AddWithValue("@ServerIP", request.ServerIP);
                command.Parameters.AddWithValue("@DatabaseName", request.DatabaseName);
                command.Parameters.AddWithValue("@Username", (object)request.Username ?? DBNull.Value);
                command.Parameters.AddWithValue("@Password", (object)request.Password ?? DBNull.Value);
                command.Parameters.AddWithValue("@IsDefault", request.IsDefault);
                command.Parameters.AddWithValue("@Description", (object)request.Description ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();

                return Json(new { success = true, message = "Target server saved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to save target: {ex.Message}" });
            }
        }

        // POST: MenuSync/DeleteTarget
        [HttpPost]
        public async Task<IActionResult> DeleteTarget([FromBody] DeleteTargetRequest request)
        {
            try
            {
                await using var connection = new SqlConnection(_sourceConnectionString);
                await connection.OpenAsync();

                var query = "UPDATE MenuSyncTargets SET IsActive = 0, UpdatedAt = GETDATE() WHERE Id = @Id";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", request.Id);

                await command.ExecuteNonQueryAsync();

                return Json(new { success = true, message = "Target server deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to delete target: {ex.Message}" });
            }
        }

        // POST: MenuSync/UpdateSyncStatus
        private async Task UpdateSyncStatus(int targetId, string status)
        {
            try
            {
                await using var connection = new SqlConnection(_sourceConnectionString);
                await connection.OpenAsync();

                var query = @"
                    UPDATE MenuSyncTargets 
                    SET LastSyncDate = GETDATE(), 
                        LastSyncStatus = @Status,
                        UpdatedAt = GETDATE()
                    WHERE Id = @Id";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", targetId);
                command.Parameters.AddWithValue("@Status", status);

                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Silently fail - this is not critical
            }
        }

        // POST: MenuSync/TestConnection
        [HttpPost]
        public async Task<IActionResult> TestConnection([FromBody] ConnectionTestRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ServerIp) || string.IsNullOrWhiteSpace(request.DatabaseName))
                {
                    return Json(new { success = false, message = "Server IP and Database Name are required." });
                }

                var connectionString = BuildConnectionString(request.ServerIp, request.DatabaseName, request.Username, request.Password);

                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check if MenuItems table exists
                var tableExistsQuery = @"
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = 'MenuItems'";

                await using var command = new SqlCommand(tableExistsQuery, connection);
                var tableExists = (int)await command.ExecuteScalarAsync() > 0;

                if (!tableExists)
                {
                    return Json(new { success = false, message = "MenuItems table not found in target database." });
                }

                return Json(new { success = true, message = "Connection successful! MenuItems table found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Connection failed: {ex.Message}" });
            }
        }

        // POST: MenuSync/GetPreviewData
        [HttpPost]
        public async Task<IActionResult> GetPreviewData()
        {
            try
            {
                var menuItems = new List<MenuSyncItem>();

                await using var connection = new SqlConnection(_sourceConnectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        Name,
                        Description,
                        CategoryName,
                        DineInPrice,
                        TakeAwayPrice,
                        DeliveryPrice,
                        ImagePath,
                        MenuItemGroup
                    FROM dev_Restaurant.dbo.vw_MenuItemSync
                    ORDER BY MenuItemGroup, CategoryName, SubcategoryName, Name";

                await using var command = new SqlCommand(query, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    menuItems.Add(new MenuSyncItem
                    {
                        Name = reader["Name"]?.ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        CategoryName = reader["CategoryName"]?.ToString() ?? "",
                        DineInPrice = reader["DineInPrice"] != DBNull.Value ? Convert.ToDecimal(reader["DineInPrice"]) : 0,
                        TakeAwayPrice = reader["TakeAwayPrice"] != DBNull.Value ? Convert.ToDecimal(reader["TakeAwayPrice"]) : 0,
                        DeliveryPrice = reader["DeliveryPrice"] != DBNull.Value ? Convert.ToDecimal(reader["DeliveryPrice"]) : 0,
                        ImagePath = reader["ImagePath"]?.ToString() ?? "",
                        MenuItemGroup = reader["MenuItemGroup"]?.ToString() ?? ""
                    });
                }

                return Json(new { success = true, data = menuItems, count = menuItems.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to retrieve preview data: {ex.Message}" });
            }
        }

        // POST: MenuSync/SyncMenuItems
        [HttpPost]
        public async Task<IActionResult> SyncMenuItems([FromBody] SyncRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ServerIp) || string.IsNullOrWhiteSpace(request.DatabaseName))
                {
                    return Json(new { success = false, message = "Server IP and Database Name are required." });
                }

                var targetConnectionString = BuildConnectionString(request.ServerIp, request.DatabaseName, request.Username, request.Password);

                // Execute sync operation on target database
                await using (var targetConnection = new SqlConnection(targetConnectionString))
                {
                    await targetConnection.OpenAsync();

                    await using var transaction = targetConnection.BeginTransaction();
                    try
                    {
                        // Delete existing records
                        var deleteCommand = new SqlCommand("DELETE FROM MenuItems", targetConnection, transaction);
                        var deletedCount = await deleteCommand.ExecuteNonQueryAsync();

                        // Insert new records using direct query from source view with ISNULL for ImagePath
                        var insertQuery = @"
                            INSERT INTO MenuItems (
                                Name,
                                Description,
                                Category,
                                DineInPrice,
                                TakeawayPrice,
                                DeliveryPrice,
                                ImageUrl,
                                IsActive,
                                CreatedAt,
                                UpdatedAt,
                                Itemgroup
                            )
                            SELECT 
                                name,
                                Description,
                                categoryName,
                                DineInPrice,
                                TakeAwayPrice,
                                DeliveryPrice,
                                ISNULL(ImagePath, '/images/menu/'),
                                1,
                                GETDATE(),
                                GETDATE(),
                                MenuItemgroup
                            FROM dev_restaurant.dbo.vw_MenuitemSync
                            ORDER BY MenuItemgroup, categoryName, SubcategoryName, Name";

                        await using var insertCommand = new SqlCommand(insertQuery, targetConnection, transaction);
                        var insertedCount = await insertCommand.ExecuteNonQueryAsync();

                        await transaction.CommitAsync();

                        return Json(new
                        {
                            success = true,
                            message = $"Sync completed successfully! Deleted: {deletedCount}, Inserted: {insertedCount}",
                            deleted = deletedCount,
                            inserted = insertedCount
                        });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception($"Transaction failed: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Sync failed: {ex.Message}" });
            }
        }

        private string BuildConnectionString(string serverIp, string databaseName, string? username = null, string? password = null)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = serverIp,
                InitialCatalog = databaseName,
                TrustServerCertificate = true,
                ConnectTimeout = 30,
                Encrypt = false // Disable encryption for older SQL Server versions
            };

            // If username is provided, use SQL Server authentication
            if (!string.IsNullOrWhiteSpace(username))
            {
                builder.UserID = username;
                builder.Password = password ?? ""; // Use empty string if password is null
                builder.IntegratedSecurity = false;
            }
            else
            {
                // Use Windows Authentication only if no username is provided
                builder.IntegratedSecurity = true;
            }

            return builder.ConnectionString;
        }
    }

    // Request Models
    public class ConnectionTestRequest
    {
        public string ServerIp { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class SyncRequest
    {
        public string ServerIp { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class MenuSyncItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal DineInPrice { get; set; }
        public decimal TakeAwayPrice { get; set; }
        public decimal DeliveryPrice { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string MenuItemGroup { get; set; } = string.Empty;
    }

    public class MenuSyncTarget
    {
        public int Id { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string ServerIP { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string? Username { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public string? Description { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public string? LastSyncStatus { get; set; }
    }

    public class SaveTargetRequest
    {
        public int? Id { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string ServerIP { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool IsDefault { get; set; }
        public string? Description { get; set; }
    }

    public class DeleteTargetRequest
    {
        public int Id { get; set; }
    }
}
