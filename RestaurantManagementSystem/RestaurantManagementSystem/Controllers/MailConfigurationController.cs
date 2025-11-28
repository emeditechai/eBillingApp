using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.ViewModels;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models.Authorization;
using RestaurantManagementSystem.Models;
using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [RequirePermission("NAV_SETTINGS_MAIL", PermissionAction.View)]
    public class MailConfigurationController : Controller
    {
        private readonly string _connectionString;
        private readonly Microsoft.Extensions.Logging.ILogger<MailConfigurationController> _logger;
        private readonly byte[] _encryptionKey;
        private readonly byte[] _encryptionIV;

        public MailConfigurationController(
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            Microsoft.Extensions.Logging.ILogger<MailConfigurationController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            
            // Get encryption key and IV from configuration
            _encryptionKey = Convert.FromBase64String(configuration["Encryption:Key"]);
            _encryptionIV = Convert.FromBase64String(configuration["Encryption:IV"]);
        }

        // GET: MailConfiguration/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var config = await GetMailConfigurationAsync();
                if (config == null)
                {
                    // Return empty config for first time setup
                    config = new MailConfigurationViewModel
                    {
                        SmtpPort = 587,
                        EnableSSL = true,
                        IsActive = false,
                        FromName = "Restaurant Management System",
                        FromEmail = "noreply@restaurant.com"
                    };
                }
                return View(config);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading mail configuration: {ex.Message}";
                return View(new MailConfigurationViewModel());
            }
        }

        // POST: MailConfiguration/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("NAV_SETTINGS_MAIL", PermissionAction.Edit)]
        public async Task<IActionResult> Save(MailConfigurationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            try
            {
                // Encrypt password before storing
                var encryptedPassword = EncryptPassword(model.SmtpPassword);
                
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    // Check if configuration exists
                    var checkSql = "SELECT COUNT(*) FROM dbo.tbl_MailConfiguration";
                    using (var checkCmd = new SqlCommand(checkSql, connection))
                    {
                        var count = (int)await checkCmd.ExecuteScalarAsync();
                        
                        string sql;
                        if (count == 0)
                        {
                            // Insert new configuration
                            sql = @"INSERT INTO dbo.tbl_MailConfiguration 
                                    (SmtpServer, SmtpPort, SmtpUsername, SmtpPassword, EnableSSL, 
                                     FromEmail, FromName, AdminNotificationEmail, IsActive, 
                                     CreatedAt, UpdatedAt)
                                    VALUES 
                                    (@SmtpServer, @SmtpPort, @SmtpUsername, @SmtpPassword, @EnableSSL, 
                                     @FromEmail, @FromName, @AdminNotificationEmail, @IsActive, 
                                     SYSUTCDATETIME(), SYSUTCDATETIME())";
                        }
                        else
                        {
                            // Update existing configuration
                            sql = @"UPDATE dbo.tbl_MailConfiguration 
                                    SET SmtpServer = @SmtpServer,
                                        SmtpPort = @SmtpPort,
                                        SmtpUsername = @SmtpUsername,
                                        SmtpPassword = @SmtpPassword,
                                        EnableSSL = @EnableSSL,
                                        FromEmail = @FromEmail,
                                        FromName = @FromName,
                                        AdminNotificationEmail = @AdminNotificationEmail,
                                        IsActive = @IsActive,
                                        UpdatedAt = SYSUTCDATETIME()";
                        }
                        
                        using (var cmd = new SqlCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@SmtpServer", model.SmtpServer);
                            cmd.Parameters.AddWithValue("@SmtpPort", model.SmtpPort);
                            cmd.Parameters.AddWithValue("@SmtpUsername", model.SmtpUsername);
                            cmd.Parameters.AddWithValue("@SmtpPassword", encryptedPassword);
                            cmd.Parameters.AddWithValue("@EnableSSL", model.EnableSSL);
                            cmd.Parameters.AddWithValue("@FromEmail", model.FromEmail);
                            cmd.Parameters.AddWithValue("@FromName", model.FromName);
                            cmd.Parameters.AddWithValue("@AdminNotificationEmail", 
                                string.IsNullOrEmpty(model.AdminNotificationEmail) ? (object)DBNull.Value : model.AdminNotificationEmail);
                            cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                            
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                
                TempData["SuccessMessage"] = "Mail configuration saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error saving mail configuration: {ex.Message}";
                return View("Index", model);
            }
        }

        // POST: MailConfiguration/TestConfiguration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestConfiguration([FromBody] TestEmailRequest request)
        {
            MailConfigurationViewModel config = null;
            var stopwatch = Stopwatch.StartNew();
            string emailBody = string.Empty;
            string smtpServerUsed = string.Empty;
            
            try
            {
                config = await GetMailConfigurationAsync();
                if (config == null)
                {
                    return Json(new { success = false, message = "Mail configuration not found. Please save configuration first." });
                }

                // Validate configuration
                if (string.IsNullOrWhiteSpace(config.SmtpServer))
                {
                    return Json(new { success = false, message = "SMTP Server is not configured." });
                }

                if (string.IsNullOrWhiteSpace(config.SmtpUsername))
                {
                    return Json(new { success = false, message = "SMTP Username is not configured." });
                }

                if (string.IsNullOrWhiteSpace(config.SmtpPassword))
                {
                    return Json(new { success = false, message = "SMTP Password is not configured." });
                }

                // Decrypt password
                var decryptedPassword = DecryptPassword(config.SmtpPassword);
                
                // Log configuration details (without sensitive data)
                _logger.LogInformation("Testing email with Server: {Server}, Port: {Port}, SSL: {SSL}, Username: {Username}", 
                    config.SmtpServer, config.SmtpPort, config.EnableSSL, config.SmtpUsername);

                // Determine the correct SMTP server format
                string smtpServer = config.SmtpServer;
                
                // If server doesn't start with smtp/mail prefix and isn't an IP, prepend smtp.
                if (!smtpServer.StartsWith("smtp.", StringComparison.OrdinalIgnoreCase) && 
                    !smtpServer.StartsWith("mail.", StringComparison.OrdinalIgnoreCase) &&
                    !System.Net.IPAddress.TryParse(smtpServer, out _))
                {
                    // Check if it's a common provider domain
                    if (smtpServer.Contains("gmail.com"))
                        smtpServer = "smtp.gmail.com";
                    else if (smtpServer.Contains("outlook.com") || smtpServer.Contains("hotmail.com") || smtpServer.Contains("live.com"))
                        smtpServer = "smtp.office365.com";
                    else if (smtpServer.Contains("yahoo.com"))
                        smtpServer = "smtp.mail.yahoo.com";
                    else
                        smtpServer = $"smtp.{smtpServer}"; // Generic SMTP prefix
                }

                // Log credential information (password length only for security)
                _logger.LogInformation("SMTP Credentials - Username: {Username}, Password Length: {Length} chars", 
                    config.SmtpUsername, decryptedPassword?.Length ?? 0);
                
                // TEMPORARY: Console output for debugging
                Console.WriteLine("=== MAIL CONFIG DEBUG ===");
                Console.WriteLine($"Server: {smtpServer}");
                Console.WriteLine($"Port: {config.SmtpPort}");
                Console.WriteLine($"SSL: {config.EnableSSL}");
                Console.WriteLine($"Username: {config.SmtpUsername}");
                Console.WriteLine($"Password Length: {decryptedPassword?.Length ?? 0}");
                Console.WriteLine($"Password Has Spaces: {(decryptedPassword?.Contains(" ") ?? false)}");
                Console.WriteLine($"Password First 4 chars: {(decryptedPassword?.Length >= 4 ? decryptedPassword.Substring(0, 4) : "N/A")}");
                Console.WriteLine("========================");
                
                // Store the server being used for logging
                smtpServerUsed = smtpServer;
                
                // Create SMTP client with enhanced settings
                using (var client = new SmtpClient(smtpServer, config.SmtpPort))
                {
                    client.EnableSsl = config.EnableSSL;
                    client.UseDefaultCredentials = false; // Must be false for custom credentials
                    client.Credentials = new NetworkCredential(config.SmtpUsername, decryptedPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 30000; // 30 seconds timeout for better reliability
                    
                    _logger.LogInformation("SmtpClient configured - Host: {Host}, Port: {Port}, SSL: {SSL}, Timeout: {Timeout}ms", 
                        client.Host, client.Port, client.EnableSsl, client.Timeout);

                    // Create test email message
                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(config.FromEmail, config.FromName);
                        message.To.Add(request.TestEmail);
                        message.Subject = "Test Email from Restaurant Management System";
                        emailBody = $@"
                            <html>
                            <body style='font-family: Arial, sans-serif; padding: 20px;'>
                                <h2 style='color: #4CAF50;'>✓ Test Email Successful</h2>
                                <p>This is a test email from Restaurant Management System.</p>
                                <div style='background-color: #f5f5f5; padding: 15px; border-left: 4px solid #2196F3; margin: 20px 0;'>
                                    <p style='margin: 5px 0;'><strong>SMTP Server:</strong> {config.SmtpServer}</p>
                                    <p style='margin: 5px 0;'><strong>SMTP Port:</strong> {config.SmtpPort}</p>
                                    <p style='margin: 5px 0;'><strong>SSL Enabled:</strong> {(config.EnableSSL ? "Yes" : "No")}</p>
                                    <p style='margin: 5px 0;'><strong>From:</strong> {config.FromName} &lt;{config.FromEmail}&gt;</p>
                                    <p style='margin: 5px 0;'><strong>Sent at:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                                </div>
                                <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                                <p style='color: #666; font-size: 12px;'>
                                    <strong>✓ Configuration Verified:</strong> If you received this email, your mail configuration is working correctly and ready to send notifications.
                                </p>
                            </body>
                            </html>
                        ";
                        message.Body = emailBody;
                        message.IsBodyHtml = true;
                        message.Priority = MailPriority.Normal;

                        // Send the email
                        await client.SendMailAsync(message);
                        stopwatch.Stop();
                        
                        // Log successful email
                        await LogEmailAsync(new EmailLog
                        {
                            FromEmail = config.FromEmail,
                            ToEmail = request.TestEmail,
                            Subject = "Test Email from Restaurant Management System",
                            EmailBody = emailBody,
                            SmtpServer = smtpServerUsed,
                            SmtpPort = config.SmtpPort,
                            EnableSSL = config.EnableSSL,
                            SmtpUsername = config.SmtpUsername,
                            Status = "Success",
                            ErrorMessage = null,
                            ErrorCode = null,
                            SentAt = DateTime.Now,
                            ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                            IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                            CreatedBy = GetCurrentUserId()
                        });
                    }
                }

                return Json(new { success = true, message = $"Test email sent successfully to {request.TestEmail}. Please check your inbox." });
            }
            catch (SmtpFailedRecipientsException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to send to recipient: {Email}. StatusCode: {StatusCode}", 
                    request.TestEmail, ex.StatusCode);
                
                // Log failed email attempt
                await LogEmailAsync(new EmailLog
                {
                    FromEmail = config?.FromEmail ?? "N/A",
                    ToEmail = request.TestEmail,
                    Subject = "Test Email from Restaurant Management System",
                    EmailBody = emailBody,
                    SmtpServer = smtpServerUsed ?? config?.SmtpServer ?? "N/A",
                    SmtpPort = config?.SmtpPort ?? 0,
                    EnableSSL = config?.EnableSSL ?? false,
                    SmtpUsername = config?.SmtpUsername ?? "N/A",
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    ErrorCode = ex.StatusCode.ToString(),
                    SentAt = DateTime.Now,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                    CreatedBy = GetCurrentUserId()
                });
                
                return Json(new { success = false, message = $"Failed to send to recipient: {ex.Message}" });
            }
            catch (SmtpException ex)
            {
                stopwatch.Stop();
                // Log the FULL exception details for troubleshooting
                _logger.LogError(ex, "SMTP Exception - StatusCode: {StatusCode}, InnerException: {InnerException}", 
                    ex.StatusCode, ex.InnerException?.Message ?? "None");
                
                var errorMsg = ex.Message;
                var isGmail = config != null && config.SmtpServer != null && 
                              config.SmtpServer.Contains("gmail.com", StringComparison.OrdinalIgnoreCase);
                
                if (ex.Message.Contains("5.7.0") || ex.Message.Contains("Authentication Required") || ex.Message.Contains("authentication") || ex.Message.Contains("5.7.8"))
                {
                    if (isGmail)
                    {
                        errorMsg = "Gmail Authentication Failed. You need to:\n" +
                                   "1. Enable 2-Step Verification in your Google Account\n" +
                                   "2. Generate an App Password (Security → App Passwords)\n" +
                                   "3. Use the 16-character App Password instead of your regular password";
                    }
                    else
                    {
                        errorMsg = $"Authentication Failed for {config?.SmtpServer}.\n\n" +
                                   "Please verify:\n" +
                                   $"✗ SMTP Server: {config?.SmtpServer}\n" +
                                   $"✗ Port: {config?.SmtpPort} (465 for SSL, 587 for TLS)\n" +
                                   $"✗ Username: {config?.SmtpUsername}\n" +
                                   $"✗ Password: Incorrect or SMTP not enabled\n" +
                                   $"✗ SSL: {(config?.EnableSSL == true ? "Enabled" : "Disabled")}\n\n" +
                                   "Common Solutions:\n" +
                                   "• Verify password is correct in Mail Configuration page\n" +
                                   "• Check if SMTP is enabled in your email hosting (cPanel/Webmail)\n" +
                                   "• Try username without @domain (just 'custcare' instead of full email)\n" +
                                   "• Contact your email hosting provider for correct SMTP settings";
                    }
                }
                else if (ex.Message.Contains("5.5.1"))
                {
                    errorMsg = "SMTP Command Error. Please verify:\n" +
                               "- SMTP Server address is correct\n" +
                               "- Port matches SSL setting (587 for TLS, 465 for SSL)\n" +
                               "- Username format is correct (usually full email address)";
                }
                else if (ex.Message.Contains("Syntax error") || ex.Message.Contains("command unrecognized"))
                {
                    errorMsg = "SMTP server connection error. Please verify:\n" +
                               "- SMTP Server address is correct (e.g., smtp.domain.com, not just domain.com)\n" +
                               "- Port number matches SSL setting (587 for TLS, 465 for SSL)\n" +
                               "- Try using 'smtp.' or 'mail.' prefix before domain name\n" +
                               "- Contact your hosting provider for correct SMTP server address";
                }
                
                // Log failed email attempt
                await LogEmailAsync(new EmailLog
                {
                    FromEmail = config?.FromEmail ?? "N/A",
                    ToEmail = request.TestEmail,
                    Subject = "Test Email from Restaurant Management System",
                    EmailBody = emailBody,
                    SmtpServer = smtpServerUsed ?? config?.SmtpServer ?? "N/A",
                    SmtpPort = config?.SmtpPort ?? 0,
                    EnableSSL = config?.EnableSSL ?? false,
                    SmtpUsername = config?.SmtpUsername ?? "N/A",
                    Status = "Failed",
                    ErrorMessage = errorMsg,
                    ErrorCode = ex.StatusCode.ToString(),
                    SentAt = DateTime.Now,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                    CreatedBy = GetCurrentUserId()
                });
                
                return Json(new { success = false, message = $"SMTP Error: {errorMsg}" });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // Log full exception details to server console
                _logger.LogError(ex, "Failed to send test email. Server: {Server}, Port: {Port}, SSL: {SSL}", 
                    config?.SmtpServer, config?.SmtpPort, config?.EnableSSL);
                
                var errorMsg = ex.Message;
                
                // Get detailed error information
                if (ex.InnerException != null)
                {
                    errorMsg += $"\n\nDetails: {ex.InnerException.Message}";
                    
                    if (ex.InnerException.InnerException != null)
                    {
                        errorMsg += $"\n\nMore details: {ex.InnerException.InnerException.Message}";
                    }
                }
                
                // Add helpful suggestions based on common issues
                if (errorMsg.Contains("Connection") || errorMsg.Contains("timed out") || errorMsg.Contains("refused"))
                {
                    errorMsg += "\n\nPossible solutions:\n" +
                               "- Verify SMTP server address is correct\n" +
                               "- Check if firewall is blocking SMTP port\n" +
                               "- Try different port (587 for TLS or 465 for SSL)";
                }
                else if (errorMsg.Contains("SSL") || errorMsg.Contains("TLS") || errorMsg.Contains("secure"))
                {
                    errorMsg += "\n\nSSL/TLS issue:\n" +
                               "- Port 465 requires SSL enabled\n" +
                               "- Port 587 requires SSL enabled (STARTTLS)\n" +
                               "- Verify SSL checkbox matches your port setting";
                }
                
                // If still generic error, add current config info
                if (errorMsg == "Failure sending mail." && config != null)
                {
                    errorMsg += $"\n\nCurrent Configuration:\n" +
                               $"- Server: {config.SmtpServer}\n" +
                               $"- Port: {config.SmtpPort}\n" +
                               $"- SSL: {(config.EnableSSL ? "Enabled" : "Disabled")}\n" +
                               $"- Username: {config.SmtpUsername}\n\n" +
                               "Please check server console logs for detailed error information.";
                }
                
                // Log failed email attempt
                await LogEmailAsync(new EmailLog
                {
                    FromEmail = config?.FromEmail ?? "N/A",
                    ToEmail = request.TestEmail,
                    Subject = "Test Email from Restaurant Management System",
                    EmailBody = emailBody,
                    SmtpServer = smtpServerUsed ?? config?.SmtpServer ?? "N/A",
                    SmtpPort = config?.SmtpPort ?? 0,
                    EnableSSL = config?.EnableSSL ?? false,
                    SmtpUsername = config?.SmtpUsername ?? "N/A",
                    Status = "Failed",
                    ErrorMessage = errorMsg,
                    ErrorCode = "EXCEPTION",
                    SentAt = DateTime.Now,
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                    CreatedBy = GetCurrentUserId()
                });
                
                return Json(new { success = false, message = $"Failed to send test email: {errorMsg}" });
            }
        }

        private async Task<MailConfigurationViewModel> GetMailConfigurationAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                var sql = @"SELECT TOP 1 
                            Id, SmtpServer, SmtpPort, SmtpUsername, SmtpPassword, EnableSSL, 
                            FromEmail, FromName, AdminNotificationEmail, IsActive, 
                            CreatedAt, UpdatedAt 
                            FROM dbo.tbl_MailConfiguration";
                
                using (var cmd = new SqlCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new MailConfigurationViewModel
                        {
                            Id = reader.GetInt32(0),
                            SmtpServer = reader.GetString(1),
                            SmtpPort = reader.GetInt32(2),
                            SmtpUsername = reader.GetString(3),
                            SmtpPassword = DecryptPassword(reader.GetString(4)),
                            EnableSSL = reader.GetBoolean(5),
                            FromEmail = reader.GetString(6),
                            FromName = reader.GetString(7),
                            AdminNotificationEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
                            IsActive = reader.GetBoolean(9),
                            CreatedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                            UpdatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11)
                        };
                    }
                }
            }
            
            return null;
        }

        private string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return password;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.IV = _encryptionIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(password);
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting password");
                throw;
            }
        }

        private string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
                return encryptedPassword;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.IV = _encryptionIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var cipherBytes = Convert.FromBase64String(encryptedPassword);

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting password");
                // Return as-is if decryption fails (might be plain text from migration)
                return encryptedPassword;
            }
        }

        // Helper method to log email attempts to database
        private async Task LogEmailAsync(EmailLog emailLog)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    var query = @"
                        INSERT INTO tbl_EmailLog (
                            FromEmail, ToEmail, Subject, EmailBody, 
                            SmtpServer, SmtpPort, EnableSSL, SmtpUsername,
                            Status, ErrorMessage, ErrorCode,
                            SentAt, ProcessingTimeMs, IPAddress, UserAgent,
                            CreatedBy, CreatedAt
                        ) VALUES (
                            @FromEmail, @ToEmail, @Subject, @EmailBody,
                            @SmtpServer, @SmtpPort, @EnableSSL, @SmtpUsername,
                            @Status, @ErrorMessage, @ErrorCode,
                            @SentAt, @ProcessingTimeMs, @IPAddress, @UserAgent,
                            @CreatedBy, @CreatedAt
                        )";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FromEmail", emailLog.FromEmail);
                        command.Parameters.AddWithValue("@ToEmail", emailLog.ToEmail);
                        command.Parameters.AddWithValue("@Subject", (object)emailLog.Subject ?? DBNull.Value);
                        command.Parameters.AddWithValue("@EmailBody", (object)emailLog.EmailBody ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpServer", emailLog.SmtpServer);
                        command.Parameters.AddWithValue("@SmtpPort", emailLog.SmtpPort);
                        command.Parameters.AddWithValue("@EnableSSL", emailLog.EnableSSL);
                        command.Parameters.AddWithValue("@SmtpUsername", emailLog.SmtpUsername);
                        command.Parameters.AddWithValue("@Status", emailLog.Status);
                        command.Parameters.AddWithValue("@ErrorMessage", (object)emailLog.ErrorMessage ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ErrorCode", (object)emailLog.ErrorCode ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentAt", emailLog.SentAt);
                        command.Parameters.AddWithValue("@ProcessingTimeMs", (object)emailLog.ProcessingTimeMs ?? DBNull.Value);
                        command.Parameters.AddWithValue("@IPAddress", (object)emailLog.IPAddress ?? DBNull.Value);
                        command.Parameters.AddWithValue("@UserAgent", (object)emailLog.UserAgent ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CreatedBy", (object)emailLog.CreatedBy ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CreatedAt", emailLog.CreatedAt);
                        
                        await command.ExecuteNonQueryAsync();
                    }
                }
                
                _logger.LogInformation("Email log saved - Status: {Status}, To: {To}, ProcessingTime: {Time}ms", 
                    emailLog.Status, emailLog.ToEmail, emailLog.ProcessingTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save email log to database");
                // Don't throw - logging failure shouldn't break the main flow
            }
        }

        // Helper method to get current user ID from session/claims
        private int? GetCurrentUserId()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserID")?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    return userId;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    public class TestEmailRequest
    {
        public string TestEmail { get; set; }
    }
}
