using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models.Authorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [RequirePermission("NAV_SETTINGS_EMAIL_LOGS", PermissionAction.View)]
    public class EmailLogsController : Controller
    {
        private readonly string _connectionString;
        private readonly Microsoft.Extensions.Logging.ILogger<EmailLogsController> _logger;

        public EmailLogsController(
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            Microsoft.Extensions.Logging.ILogger<EmailLogsController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        // GET: EmailLogs
        public async Task<IActionResult> Index(string status = "", string searchEmail = "", int page = 1)
        {
            try
            {
                int pageSize = 50;
                int skip = (page - 1) * pageSize;
                
                var logs = new List<EmailLog>();
                int totalCount = 0;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Build WHERE clause based on filters
                    var whereConditions = new List<string>();
                    if (!string.IsNullOrEmpty(status) && status != "All")
                    {
                        whereConditions.Add("Status = @Status");
                    }
                    if (!string.IsNullOrEmpty(searchEmail))
                    {
                        whereConditions.Add("(ToEmail LIKE @SearchEmail OR FromEmail LIKE @SearchEmail)");
                    }

                    var whereClause = whereConditions.Count > 0 
                        ? "WHERE " + string.Join(" AND ", whereConditions) 
                        : "";

                    // Get total count
                    var countQuery = $"SELECT COUNT(*) FROM tbl_EmailLog {whereClause}";
                    using (var countCommand = new SqlCommand(countQuery, connection))
                    {
                        if (!string.IsNullOrEmpty(status) && status != "All")
                        {
                            countCommand.Parameters.AddWithValue("@Status", status);
                        }
                        if (!string.IsNullOrEmpty(searchEmail))
                        {
                            countCommand.Parameters.AddWithValue("@SearchEmail", $"%{searchEmail}%");
                        }
                        totalCount = (int)await countCommand.ExecuteScalarAsync();
                    }

                    // Get paginated logs
                    var query = $@"
                        SELECT 
                            EmailLogID, FromEmail, ToEmail, Subject, EmailBody,
                            SmtpServer, SmtpPort, EnableSSL, SmtpUsername,
                            Status, ErrorMessage, ErrorCode,
                            SentAt, ProcessingTimeMs, IPAddress, UserAgent,
                            CreatedBy, CreatedAt
                        FROM tbl_EmailLog
                        {whereClause}
                        ORDER BY SentAt DESC
                        OFFSET @Skip ROWS
                        FETCH NEXT @PageSize ROWS ONLY";

                    using (var command = new SqlCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(status) && status != "All")
                        {
                            command.Parameters.AddWithValue("@Status", status);
                        }
                        if (!string.IsNullOrEmpty(searchEmail))
                        {
                            command.Parameters.AddWithValue("@SearchEmail", $"%{searchEmail}%");
                        }
                        command.Parameters.AddWithValue("@Skip", skip);
                        command.Parameters.AddWithValue("@PageSize", pageSize);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                logs.Add(new EmailLog
                                {
                                    EmailLogID = reader.GetInt32(0),
                                    FromEmail = reader.GetString(1),
                                    ToEmail = reader.GetString(2),
                                    Subject = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    EmailBody = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    SmtpServer = reader.GetString(5),
                                    SmtpPort = reader.GetInt32(6),
                                    EnableSSL = reader.GetBoolean(7),
                                    SmtpUsername = reader.GetString(8),
                                    Status = reader.GetString(9),
                                    ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    ErrorCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                                    SentAt = reader.GetDateTime(12),
                                    ProcessingTimeMs = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                                    IPAddress = reader.IsDBNull(14) ? null : reader.GetString(14),
                                    UserAgent = reader.IsDBNull(15) ? null : reader.GetString(15),
                                    CreatedBy = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                                    CreatedAt = reader.GetDateTime(17)
                                });
                            }
                        }
                    }
                }

                ViewBag.Status = status;
                ViewBag.SearchEmail = searchEmail;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                ViewBag.TotalCount = totalCount;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading email logs");
                TempData["ErrorMessage"] = $"Error loading email logs: {ex.Message}";
                return View(new List<EmailLog>());
            }
        }

        // GET: EmailLogs/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                EmailLog log = null;

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT 
                            EmailLogID, FromEmail, ToEmail, Subject, EmailBody,
                            SmtpServer, SmtpPort, EnableSSL, SmtpUsername,
                            Status, ErrorMessage, ErrorCode,
                            SentAt, ProcessingTimeMs, IPAddress, UserAgent,
                            CreatedBy, CreatedAt
                        FROM tbl_EmailLog
                        WHERE EmailLogID = @EmailLogID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmailLogID", id);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                log = new EmailLog
                                {
                                    EmailLogID = reader.GetInt32(0),
                                    FromEmail = reader.GetString(1),
                                    ToEmail = reader.GetString(2),
                                    Subject = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    EmailBody = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    SmtpServer = reader.GetString(5),
                                    SmtpPort = reader.GetInt32(6),
                                    EnableSSL = reader.GetBoolean(7),
                                    SmtpUsername = reader.GetString(8),
                                    Status = reader.GetString(9),
                                    ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    ErrorCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                                    SentAt = reader.GetDateTime(12),
                                    ProcessingTimeMs = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                                    IPAddress = reader.IsDBNull(14) ? null : reader.GetString(14),
                                    UserAgent = reader.IsDBNull(15) ? null : reader.GetString(15),
                                    CreatedBy = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                                    CreatedAt = reader.GetDateTime(17)
                                };
                            }
                        }
                    }
                }

                if (log == null)
                {
                    return NotFound();
                }

                return View(log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading email log details for ID: {EmailLogID}", id);
                TempData["ErrorMessage"] = $"Error loading email log details: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
