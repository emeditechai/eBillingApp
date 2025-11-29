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
    [Authorize(Roles = "Administrator,Manager,Floor Manager")]
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
        public async Task<IActionResult> Index(
            string status = "", 
            string searchEmail = "", 
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            int page = 1)
        {
            try
            {
                // Default to today's date if no dates provided
                if (!fromDate.HasValue && !toDate.HasValue)
                {
                    fromDate = DateTime.Today;
                    toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
                }
                else if (fromDate.HasValue && !toDate.HasValue)
                {
                    toDate = fromDate.Value.AddDays(1).AddSeconds(-1);
                }
                else if (!fromDate.HasValue && toDate.HasValue)
                {
                    fromDate = toDate.Value.Date;
                }

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
                    if (fromDate.HasValue)
                    {
                        whereConditions.Add("SentAt >= @FromDate");
                    }
                    if (toDate.HasValue)
                    {
                        whereConditions.Add("SentAt <= @ToDate");
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
                        if (fromDate.HasValue)
                        {
                            countCommand.Parameters.AddWithValue("@FromDate", fromDate.Value);
                        }
                        if (toDate.HasValue)
                        {
                            countCommand.Parameters.AddWithValue("@ToDate", toDate.Value);
                        }
                        totalCount = (int)await countCommand.ExecuteScalarAsync();
                    }

                    // Get paginated logs
                    var query = $@"
                        SELECT 
                            EmailLogID, FromEmail, FromName, ToEmail, Subject, EmailBody, Body,
                            SmtpServer, SmtpPort, EnableSSL, SmtpUseSsl, SmtpTimeout, SmtpUsername,
                            Status, ErrorMessage, ErrorCode,
                            SentAt, ProcessingTimeMs, IPAddress, UserAgent,
                            EmailType, SentFrom, CreatedBy, SentBy, CreatedAt
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
                        if (fromDate.HasValue)
                        {
                            command.Parameters.AddWithValue("@FromDate", fromDate.Value);
                        }
                        if (toDate.HasValue)
                        {
                            command.Parameters.AddWithValue("@ToDate", toDate.Value);
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
                                    FromName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    ToEmail = reader.GetString(3),
                                    Subject = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    EmailBody = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Body = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    SmtpServer = reader.GetString(7),
                                    SmtpPort = reader.GetInt32(8),
                                    EnableSSL = reader.GetBoolean(9),
                                    SmtpUseSsl = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
                                    SmtpTimeout = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                                    SmtpUsername = reader.GetString(12),
                                    Status = reader.GetString(13),
                                    ErrorMessage = reader.IsDBNull(14) ? null : reader.GetString(14),
                                    ErrorCode = reader.IsDBNull(15) ? null : reader.GetString(15),
                                    SentAt = reader.GetDateTime(16),
                                    ProcessingTimeMs = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                                    IPAddress = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    UserAgent = reader.IsDBNull(19) ? null : reader.GetString(19),
                                    EmailType = reader.IsDBNull(20) ? null : reader.GetString(20),
                                    SentFrom = reader.IsDBNull(21) ? null : reader.GetString(21),
                                    CreatedBy = reader.IsDBNull(22) ? null : reader.GetInt32(22),
                                    SentBy = reader.IsDBNull(23) ? null : reader.GetInt32(23),
                                    CreatedAt = reader.GetDateTime(24)
                                });
                            }
                        }
                    }
                }

                ViewBag.Status = status;
                ViewBag.SearchEmail = searchEmail;
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
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
                            EmailLogID, FromEmail, FromName, ToEmail, Subject, EmailBody, Body,
                            SmtpServer, SmtpPort, EnableSSL, SmtpUseSsl, SmtpTimeout, SmtpUsername,
                            Status, ErrorMessage, ErrorCode,
                            SentAt, ProcessingTimeMs, IPAddress, UserAgent,
                            EmailType, SentFrom, CreatedBy, SentBy, CreatedAt
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
                                    FromName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    ToEmail = reader.GetString(3),
                                    Subject = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    EmailBody = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    Body = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    SmtpServer = reader.GetString(7),
                                    SmtpPort = reader.GetInt32(8),
                                    EnableSSL = reader.GetBoolean(9),
                                    SmtpUseSsl = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
                                    SmtpTimeout = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                                    SmtpUsername = reader.GetString(12),
                                    Status = reader.GetString(13),
                                    ErrorMessage = reader.IsDBNull(14) ? null : reader.GetString(14),
                                    ErrorCode = reader.IsDBNull(15) ? null : reader.GetString(15),
                                    SentAt = reader.GetDateTime(16),
                                    ProcessingTimeMs = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                                    IPAddress = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    UserAgent = reader.IsDBNull(19) ? null : reader.GetString(19),
                                    EmailType = reader.IsDBNull(20) ? null : reader.GetString(20),
                                    SentFrom = reader.IsDBNull(21) ? null : reader.GetString(21),
                                    CreatedBy = reader.IsDBNull(22) ? null : reader.GetInt32(22),
                                    SentBy = reader.IsDBNull(23) ? null : reader.GetInt32(23),
                                    CreatedAt = reader.GetDateTime(24)
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
