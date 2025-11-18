using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize]
    public class AuditTrailController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AuditTrailController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        // GET: AuditTrail
        public async Task<IActionResult> Index(int? orderId, string? orderNumber, DateTime? startDate, 
            DateTime? endDate, int? userId, string? entityType, string? searchTerm, int page = 1, int pageSize = 50)
        {
            // If dates are not provided, default to last 90 days
            if (!startDate.HasValue)
            {
                startDate = DateTime.Now.AddDays(-90);
            }
            if (!endDate.HasValue)
            {
                endDate = DateTime.Now.AddDays(1); // Include today's records
            }
            
            // Handle empty string for entityType (when "All Types" is selected)
            if (string.IsNullOrWhiteSpace(entityType))
            {
                entityType = null;
            }
            
            var model = new AuditTrailViewModel
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                StartDate = startDate,
                EndDate = endDate,
                UserId = userId,
                EntityType = entityType,
                SearchTerm = searchTerm,
                CurrentPage = page,
                PageSize = pageSize
            };

            try
            {
                await LoadAuditDataAsync(model);
                await LoadFilterOptionsAsync(model);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error loading audit trail: {ex.Message}";
            }

            return View(model);
        }

        // GET: Audit Trail for a specific order
        public async Task<IActionResult> OrderAudit(int id)
        {
            var model = new AuditTrailViewModel
            {
                OrderId = id,
                CurrentPage = 1,
                PageSize = 100,
                StartDate = DateTime.Now.AddYears(-1),
                EndDate = DateTime.Now
            };

            try
            {
                await LoadAuditDataAsync(model);
                await LoadOrderDetailsAsync(model, id);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error loading order audit trail: {ex.Message}";
            }

            return View(model);
        }

        // GET: Audit Trail Statistics
        public async Task<IActionResult> Statistics()
        {
            var stats = new AuditTrailStatistics();

            try
            {
                await LoadStatisticsAsync(stats);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error loading statistics: {ex.Message}";
            }

            return View(stats);
        }

        private async Task LoadAuditDataAsync(AuditTrailViewModel model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_GetOrderAuditTrail", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderId", model.OrderId.HasValue ? model.OrderId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@StartDate", model.StartDate.HasValue ? model.StartDate.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@EndDate", model.EndDate.HasValue ? model.EndDate.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@UserId", model.UserId.HasValue ? model.UserId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@EntityType", !string.IsNullOrEmpty(model.EntityType) ? model.EntityType : DBNull.Value);
                    command.Parameters.AddWithValue("@PageNumber", model.CurrentPage);
                    command.Parameters.AddWithValue("@PageSize", model.PageSize);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // First result set: Total count
                        if (await reader.ReadAsync())
                        {
                            model.TotalRecords = reader.GetInt32(0);
                        }

                        // Second result set: Audit records
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                model.AuditRecords.Add(new OrderAuditTrail
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    OrderId = reader.GetInt32(reader.GetOrdinal("OrderId")),
                                    OrderNumber = reader.IsDBNull(reader.GetOrdinal("OrderNumber")) ? null : reader.GetString(reader.GetOrdinal("OrderNumber")),
                                    Action = reader.GetString(reader.GetOrdinal("Action")),
                                    EntityType = reader.GetString(reader.GetOrdinal("EntityType")),
                                    EntityId = reader.IsDBNull(reader.GetOrdinal("EntityId")) ? null : reader.GetInt32(reader.GetOrdinal("EntityId")),
                                    FieldName = reader.IsDBNull(reader.GetOrdinal("FieldName")) ? null : reader.GetString(reader.GetOrdinal("FieldName")),
                                    OldValue = reader.IsDBNull(reader.GetOrdinal("OldValue")) ? null : reader.GetString(reader.GetOrdinal("OldValue")),
                                    NewValue = reader.IsDBNull(reader.GetOrdinal("NewValue")) ? null : reader.GetString(reader.GetOrdinal("NewValue")),
                                    ChangedBy = reader.GetInt32(reader.GetOrdinal("ChangedBy")),
                                    ChangedByName = reader.IsDBNull(reader.GetOrdinal("ChangedByName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ChangedByName")),
                                    ChangedDate = reader.GetDateTime(reader.GetOrdinal("ChangedDate")),
                                    IPAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress")) ? null : reader.GetString(reader.GetOrdinal("IPAddress")),
                                    UserAgent = reader.IsDBNull(reader.GetOrdinal("UserAgent")) ? null : reader.GetString(reader.GetOrdinal("UserAgent")),
                                    AdditionalInfo = reader.IsDBNull(reader.GetOrdinal("AdditionalInfo")) ? null : reader.GetString(reader.GetOrdinal("AdditionalInfo"))
                                });
                            }
                        }
                    }
                }
            }

            // Apply search term filter in memory if provided
            if (!string.IsNullOrEmpty(model.SearchTerm))
            {
                var searchLower = model.SearchTerm.ToLower();
                model.AuditRecords = model.AuditRecords.Where(a =>
                    (a.OrderNumber != null && a.OrderNumber.ToLower().Contains(searchLower)) ||
                    a.Action.ToLower().Contains(searchLower) ||
                    a.EntityType.ToLower().Contains(searchLower) ||
                    a.ChangedByName.ToLower().Contains(searchLower) ||
                    (a.FieldName != null && a.FieldName.ToLower().Contains(searchLower))
                ).ToList();
            }
        }

        private async Task LoadFilterOptionsAsync(AuditTrailViewModel model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Load entity types
                using (var command = new SqlCommand("SELECT DISTINCT EntityType FROM OrderAuditTrail ORDER BY EntityType", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        model.EntityTypes.Add(("", "All Types"));
                        while (await reader.ReadAsync())
                        {
                            var entityType = reader.GetString(0);
                            model.EntityTypes.Add((entityType, entityType));
                        }
                    }
                }

                // Load users
                using (var command = new SqlCommand("SELECT DISTINCT ChangedBy, ChangedByName FROM OrderAuditTrail ORDER BY ChangedByName", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        model.Users.Add(("", "All Users"));
                        while (await reader.ReadAsync())
                        {
                            var userId = reader.GetInt32(0).ToString();
                            var userName = reader.GetString(1);
                            model.Users.Add((userId, userName));
                        }
                    }
                }
            }
        }

        private async Task LoadOrderDetailsAsync(AuditTrailViewModel model, int orderId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("SELECT OrderNumber FROM Orders WHERE Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        model.OrderNumber = result.ToString();
                    }
                }
            }
        }

        private async Task LoadStatisticsAsync(AuditTrailStatistics stats)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Total records
                using (var command = new SqlCommand("SELECT COUNT(*) FROM OrderAuditTrail", connection))
                {
                    stats.TotalAuditRecords = (int)await command.ExecuteScalarAsync();
                }

                // Today's modifications
                using (var command = new SqlCommand("SELECT COUNT(DISTINCT OrderId) FROM OrderAuditTrail WHERE CAST(ChangedDate AS DATE) = CAST(GETDATE() AS DATE)", connection))
                {
                    stats.OrdersModifiedToday = (int)await command.ExecuteScalarAsync();
                }

                // This week's modifications
                using (var command = new SqlCommand("SELECT COUNT(DISTINCT OrderId) FROM OrderAuditTrail WHERE ChangedDate >= DATEADD(DAY, -7, GETDATE())", connection))
                {
                    stats.OrdersModifiedThisWeek = (int)await command.ExecuteScalarAsync();
                }

                // This month's modifications
                using (var command = new SqlCommand("SELECT COUNT(DISTINCT OrderId) FROM OrderAuditTrail WHERE ChangedDate >= DATEADD(MONTH, -1, GETDATE())", connection))
                {
                    stats.OrdersModifiedThisMonth = (int)await command.ExecuteScalarAsync();
                }

                // Action breakdown
                using (var command = new SqlCommand("SELECT Action, COUNT(*) as Count FROM OrderAuditTrail GROUP BY Action ORDER BY Count DESC", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            stats.ActionBreakdown[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }

                // Entity type breakdown
                using (var command = new SqlCommand("SELECT EntityType, COUNT(*) as Count FROM OrderAuditTrail GROUP BY EntityType ORDER BY Count DESC", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            stats.EntityTypeBreakdown[reader.GetString(0)] = reader.GetInt32(1);
                        }
                    }
                }

                // Top users
                using (var command = new SqlCommand("SELECT TOP 10 ChangedByName, COUNT(*) as Count FROM OrderAuditTrail GROUP BY ChangedByName ORDER BY Count DESC", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            stats.TopUsers.Add(new TopUserActivity
                            {
                                UserName = reader.GetString(0),
                                ActivityCount = reader.GetInt32(1)
                            });
                        }
                    }
                }
            }
        }

        // Helper method to log audit entries (to be called from other controllers)
        public static async Task LogAuditAsync(string connectionString, int orderId, string orderNumber, string action,
            string entityType, int? entityId, string? fieldName, string? oldValue, string? newValue,
            int changedBy, string changedByName, string? ipAddress, string? userAgent, string? additionalInfo)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_LogOrderAudit", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    command.Parameters.AddWithValue("@OrderNumber", orderNumber ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@Action", action);
                    command.Parameters.AddWithValue("@EntityType", entityType);
                    command.Parameters.AddWithValue("@EntityId", entityId.HasValue ? entityId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("@FieldName", fieldName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@OldValue", oldValue ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@NewValue", newValue ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ChangedBy", changedBy);
                    command.Parameters.AddWithValue("@ChangedByName", changedByName);
                    command.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@UserAgent", userAgent ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@AdditionalInfo", additionalInfo ?? (object)DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
