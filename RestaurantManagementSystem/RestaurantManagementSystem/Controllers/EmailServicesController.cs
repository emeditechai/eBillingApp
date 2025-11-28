using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.ViewModels;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models.Authorization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [RequirePermission("NAV_SETTINGS_EMAIL_SERVICES", PermissionAction.View)]
    public class EmailServicesController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<EmailServicesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly byte[] _encryptionKey;
        private readonly byte[] _encryptionIV;

        public EmailServicesController(
            IConfiguration configuration,
            ILogger<EmailServicesController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
            _configuration = configuration;
            
            // Get encryption keys from configuration
            var keyString = configuration["Encryption:Key"];
            var ivString = configuration["Encryption:IV"];
            
            if (string.IsNullOrEmpty(keyString) || string.IsNullOrEmpty(ivString))
            {
                throw new InvalidOperationException("Encryption keys not configured");
            }
            
            _encryptionKey = Convert.FromBase64String(keyString);
            _encryptionIV = Convert.FromBase64String(ivString);
        }

        // GET: EmailServices
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new EmailServicesViewModel
                {
                    TodayBirthdays = await GetTodayBirthdaysAsync(),
                    TodayAnniversaries = await GetTodayAnniversariesAsync(),
                    AllGuests = await GetAllGuestsWithEmailAsync(),
                    CustomTemplates = await GetCustomTemplatesAsync()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Email Services page");
                TempData["ErrorMessage"] = $"Error loading email services: {ex.Message}";
                return View(new EmailServicesViewModel());
            }
        }

        // POST: EmailServices/AutoFireEmails
        [HttpPost]
        public async Task<IActionResult> AutoFireEmails([FromBody] AutoFireEmailRequest request)
        {
            _logger.LogInformation("AutoFireEmails called - EmailType: {EmailType}, GuestCount: {Count}", 
                request?.EmailType, request?.GuestIds?.Count ?? 0);
            
            var stopwatch = Stopwatch.StartNew();
            var result = new EmailCampaignResultViewModel();

            try
            {
                if (request == null)
                {
                    _logger.LogWarning("AutoFireEmails - Request is null");
                    return Json(new { success = false, message = "Invalid request" });
                }
                
                if (request.GuestIds == null || !request.GuestIds.Any())
                {
                    _logger.LogWarning("AutoFireEmails - No guests selected");
                    return Json(new { success = false, message = "No guests selected" });
                }

                // Get mail configuration
                var mailConfig = await GetMailConfigurationAsync();
                if (mailConfig == null)
                {
                    return Json(new { success = false, message = "Mail configuration not found. Please configure email settings first." });
                }

                // Get template
                var template = await GetDefaultTemplateAsync(request.EmailType);
                if (template == null)
                {
                    return Json(new { success = false, message = $"No default {request.EmailType} template found" });
                }

                // Get guests
                var guests = await GetGuestsByIdsAsync(request.GuestIds);
                result.TotalAttempted = guests.Count;

                foreach (var guest in guests)
                {
                    try
                    {
                        // Replace placeholders in template
                        var subject = ReplacePlaceholders(template.Subject, guest, mailConfig);
                        var body = ReplacePlaceholders(template.BodyHtml, guest, mailConfig);

                        // Send email
                        var emailResult = await SendEmailAsync(mailConfig, guest.Email, subject, body);

                        if (emailResult.Success)
                        {
                            result.SuccessCount++;
                            
                            // Log to tbl_EmailLog
                            await LogEmailAsync(
                                toEmail: guest.Email ?? string.Empty,
                                subject: subject,
                                body: body,
                                status: "Success",
                                errorMessage: null,
                                processingTimeMs: emailResult.ProcessingTimeMs,
                                fromEmail: mailConfig.FromEmail,
                                fromName: mailConfig.FromName,
                                smtpServer: mailConfig.SmtpServer,
                                smtpPort: mailConfig.SmtpPort,
                                emailType: $"{request.EmailType} Campaign"
                            );
                            
                            // Log to campaign history
                            await LogCampaignHistoryAsync(new EmailCampaignHistory
                            {
                                CampaignType = request.EmailType,
                                GuestId = guest.Id,
                                GuestName = guest.GuestName ?? "Unknown",
                                GuestEmail = guest.Email ?? string.Empty,
                                EmailSubject = subject,
                                EmailBody = body,
                                SentAt = DateTime.Now,
                                Status = "Success",
                                ProcessingTimeMs = emailResult.ProcessingTimeMs,
                                SentBy = GetCurrentUserId()
                            });
                        }
                        else
                        {
                            result.FailureCount++;
                            result.Errors.Add($"{guest.GuestName}: {emailResult.ErrorMessage}");
                            
                            // Log to tbl_EmailLog
                            await LogEmailAsync(
                                toEmail: guest.Email ?? string.Empty,
                                subject: subject,
                                body: body,
                                status: "Failed",
                                errorMessage: emailResult.ErrorMessage,
                                processingTimeMs: emailResult.ProcessingTimeMs,
                                fromEmail: mailConfig.FromEmail,
                                fromName: mailConfig.FromName,
                                smtpServer: mailConfig.SmtpServer,
                                smtpPort: mailConfig.SmtpPort,
                                emailType: $"{request.EmailType} Campaign"
                            );
                            
                            // Log failed attempt to campaign history
                            await LogCampaignHistoryAsync(new EmailCampaignHistory
                            {
                                CampaignType = request.EmailType,
                                GuestId = guest.Id,
                                GuestName = guest.GuestName ?? "Unknown",
                                GuestEmail = guest.Email ?? string.Empty,
                                EmailSubject = subject,
                                EmailBody = body,
                                SentAt = DateTime.Now,
                                Status = "Failed",
                                ErrorMessage = emailResult.ErrorMessage,
                                SentBy = GetCurrentUserId()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"{guest.GuestName}: {ex.Message}");
                        _logger.LogError(ex, "Error sending email to guest {GuestId}", guest.Id);
                    }
                }

                stopwatch.Stop();
                result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                return Json(new
                {
                    success = true,
                    result = result,
                    message = $"Email campaign completed: {result.SuccessCount} sent, {result.FailureCount} failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoFireEmails");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: EmailServices/SendCustomEmail
        [HttpPost]
        public async Task<IActionResult> SendCustomEmail([FromBody] SendCustomEmailRequest request)
        {
            _logger.LogInformation("SendCustomEmail called - GuestCount: {Count}", request?.GuestIds?.Count ?? 0);
            
            var stopwatch = Stopwatch.StartNew();
            var result = new EmailCampaignResultViewModel();

            try
            {
                if (request == null)
                {
                    _logger.LogWarning("SendCustomEmail - Request is null");
                    return Json(new { success = false, message = "Invalid request" });
                }
                
                if (request.GuestIds == null || !request.GuestIds.Any())
                {
                    _logger.LogWarning("SendCustomEmail - No guests selected");
                    return Json(new { success = false, message = "No guests selected" });
                }

                // Get mail configuration
                var mailConfig = await GetMailConfigurationAsync();
                if (mailConfig == null)
                {
                    return Json(new { success = false, message = "Mail configuration not found" });
                }

                string subject, body;

                if (request.TemplateId.HasValue)
                {
                    // Use template
                    var template = await GetTemplateByIdAsync(request.TemplateId.Value);
                    if (template == null)
                    {
                        return Json(new { success = false, message = "Template not found" });
                    }
                    subject = template.Subject;
                    body = template.BodyHtml;
                }
                else
                {
                    // Use custom subject and body
                    if (string.IsNullOrWhiteSpace(request.CustomSubject) || string.IsNullOrWhiteSpace(request.CustomBody))
                    {
                        return Json(new { success = false, message = "Please provide either a template or custom subject and body" });
                    }
                    subject = request.CustomSubject;
                    body = request.CustomBody;
                }

                // Get guests
                var guests = await GetGuestsByIdsAsync(request.GuestIds);
                result.TotalAttempted = guests.Count;

                foreach (var guest in guests)
                {
                    try
                    {
                        // Replace placeholders
                        var personalizedSubject = ReplacePlaceholders(subject, guest, mailConfig);
                        var personalizedBody = ReplacePlaceholders(body, guest, mailConfig);

                        // Send email
                        var emailResult = await SendEmailAsync(mailConfig, guest.Email, personalizedSubject, personalizedBody);

                        if (emailResult.Success)
                        {
                            result.SuccessCount++;
                            
                            // Log to tbl_EmailLog
                            await LogEmailAsync(
                                toEmail: guest.Email ?? string.Empty,
                                subject: personalizedSubject,
                                body: personalizedBody,
                                status: "Success",
                                errorMessage: null,
                                processingTimeMs: emailResult.ProcessingTimeMs,
                                fromEmail: mailConfig.FromEmail,
                                fromName: mailConfig.FromName,
                                smtpServer: mailConfig.SmtpServer,
                                smtpPort: mailConfig.SmtpPort,
                                emailType: "Custom Campaign"
                            );
                            
                            await LogCampaignHistoryAsync(new EmailCampaignHistory
                            {
                                CampaignType = "Custom",
                                GuestId = guest.Id,
                                GuestName = guest.GuestName ?? "Unknown",
                                GuestEmail = guest.Email ?? string.Empty,
                                EmailSubject = personalizedSubject,
                                EmailBody = personalizedBody,
                                SentAt = DateTime.Now,
                                Status = "Success",
                                ProcessingTimeMs = emailResult.ProcessingTimeMs,
                                SentBy = GetCurrentUserId()
                            });
                        }
                        else
                        {
                            result.FailureCount++;
                            result.Errors.Add($"{guest.GuestName}: {emailResult.ErrorMessage}");
                            
                            // Log to tbl_EmailLog
                            await LogEmailAsync(
                                toEmail: guest.Email ?? string.Empty,
                                subject: personalizedSubject,
                                body: personalizedBody,
                                status: "Failed",
                                errorMessage: emailResult.ErrorMessage,
                                processingTimeMs: emailResult.ProcessingTimeMs,
                                fromEmail: mailConfig.FromEmail,
                                fromName: mailConfig.FromName,
                                smtpServer: mailConfig.SmtpServer,
                                smtpPort: mailConfig.SmtpPort,
                                emailType: "Custom Campaign"
                            );
                            
                            await LogCampaignHistoryAsync(new EmailCampaignHistory
                            {
                                CampaignType = "Custom",
                                GuestId = guest.Id,
                                GuestName = guest.GuestName ?? "Unknown",
                                GuestEmail = guest.Email ?? string.Empty,
                                EmailSubject = personalizedSubject,
                                EmailBody = personalizedBody,
                                SentAt = DateTime.Now,
                                Status = "Failed",
                                ErrorMessage = emailResult.ErrorMessage,
                                SentBy = GetCurrentUserId()
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"{guest.GuestName}: {ex.Message}");
                        _logger.LogError(ex, "Error sending custom email to guest {GuestId}", guest.Id);
                    }
                }

                stopwatch.Stop();
                result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                return Json(new
                {
                    success = true,
                    result = result,
                    message = $"Custom email campaign completed: {result.SuccessCount} sent, {result.FailureCount} failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendCustomEmail");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        #region Helper Methods

        private async Task<List<BirthdayGuestViewModel>> GetTodayBirthdaysAsync()
        {
            var birthdays = new List<BirthdayGuestViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        gf.Id, 
                        gf.GuestName, 
                        gf.Email, 
                        gf.GuestBirthDate,
                        DATEDIFF(YEAR, gf.GuestBirthDate, GETDATE()) - 
                        CASE WHEN (MONTH(gf.GuestBirthDate) > MONTH(GETDATE())) OR 
                                  (MONTH(gf.GuestBirthDate) = MONTH(GETDATE()) AND DAY(gf.GuestBirthDate) > DAY(GETDATE()))
                        THEN 1 ELSE 0 END as Age,
                        (SELECT TOP 1 SentAt FROM tbl_EmailCampaignHistory 
                         WHERE GuestId = gf.Id AND CampaignType = 'Birthday' 
                         AND YEAR(SentAt) = YEAR(GETDATE()) 
                         AND Status = 'Success'
                         ORDER BY SentAt DESC) as LastSentDate
                    FROM GuestFeedback gf
                    WHERE gf.Email IS NOT NULL 
                    AND gf.Email <> ''
                    AND gf.GuestBirthDate IS NOT NULL
                    AND MONTH(gf.GuestBirthDate) = MONTH(GETDATE())
                    AND DAY(gf.GuestBirthDate) = DAY(GETDATE())
                    ORDER BY gf.GuestName";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        birthdays.Add(new BirthdayGuestViewModel
                        {
                            GuestId = reader.GetInt32(0),
                            GuestName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            BirthDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            Age = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            LastSentDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            AlreadySent = !reader.IsDBNull(5)
                        });
                    }
                }
            }

            return birthdays;
        }

        private async Task<List<AnniversaryGuestViewModel>> GetTodayAnniversariesAsync()
        {
            var anniversaries = new List<AnniversaryGuestViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        gf.Id, 
                        gf.GuestName, 
                        gf.Email, 
                        gf.AnniversaryDate,
                        DATEDIFF(YEAR, gf.AnniversaryDate, GETDATE()) - 
                        CASE WHEN (MONTH(gf.AnniversaryDate) > MONTH(GETDATE())) OR 
                                  (MONTH(gf.AnniversaryDate) = MONTH(GETDATE()) AND DAY(gf.AnniversaryDate) > DAY(GETDATE()))
                        THEN 1 ELSE 0 END as Years,
                        (SELECT TOP 1 SentAt FROM tbl_EmailCampaignHistory 
                         WHERE GuestId = gf.Id AND CampaignType = 'Anniversary' 
                         AND YEAR(SentAt) = YEAR(GETDATE()) 
                         AND Status = 'Success'
                         ORDER BY SentAt DESC) as LastSentDate
                    FROM GuestFeedback gf
                    WHERE gf.Email IS NOT NULL 
                    AND gf.Email <> ''
                    AND gf.AnniversaryDate IS NOT NULL
                    AND MONTH(gf.AnniversaryDate) = MONTH(GETDATE())
                    AND DAY(gf.AnniversaryDate) = DAY(GETDATE())
                    ORDER BY gf.GuestName";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        anniversaries.Add(new AnniversaryGuestViewModel
                        {
                            GuestId = reader.GetInt32(0),
                            GuestName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            AnniversaryDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            Years = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            LastSentDate = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            AlreadySent = !reader.IsDBNull(5)
                        });
                    }
                }
            }

            return anniversaries;
        }

        private async Task<List<GuestEmailViewModel>> GetAllGuestsWithEmailAsync()
        {
            var guests = new List<GuestEmailViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT DISTINCT
                        gf.Id,
                        gf.GuestName,
                        gf.Email,
                        (SELECT MAX(VisitDate) FROM GuestFeedback WHERE Email = gf.Email) as LastVisitDate,
                        (SELECT COUNT(*) FROM GuestFeedback WHERE Email = gf.Email) as TotalVisits
                    FROM GuestFeedback gf
                    WHERE gf.Email IS NOT NULL 
                    AND gf.Email <> ''
                    AND gf.GuestName IS NOT NULL
                    ORDER BY gf.GuestName";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        guests.Add(new GuestEmailViewModel
                        {
                            GuestId = reader.GetInt32(0),
                            GuestName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            LastVisitDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            TotalVisits = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                        });
                    }
                }
            }

            return guests;
        }

        private async Task<List<EmailTemplateViewModel>> GetCustomTemplatesAsync()
        {
            var templates = new List<EmailTemplateViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT EmailTemplateID, TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault
                    FROM tbl_EmailTemplates
                    WHERE TemplateType IN ('Custom', 'Promotional')
                    AND IsActive = 1
                    ORDER BY TemplateName";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        templates.Add(new EmailTemplateViewModel
                        {
                            EmailTemplateID = reader.GetInt32(0),
                            TemplateName = reader.GetString(1),
                            TemplateType = reader.GetString(2),
                            Subject = reader.GetString(3),
                            BodyHtml = reader.GetString(4),
                            IsActive = reader.GetBoolean(5),
                            IsDefault = reader.GetBoolean(6)
                        });
                    }
                }
            }

            return templates;
        }

        private async Task<EmailTemplate?> GetDefaultTemplateAsync(string templateType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP 1 EmailTemplateID, TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault
                    FROM tbl_EmailTemplates
                    WHERE TemplateType = @TemplateType
                    AND IsActive = 1
                    AND IsDefault = 1";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TemplateType", templateType);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new EmailTemplate
                            {
                                EmailTemplateID = reader.GetInt32(0),
                                TemplateName = reader.GetString(1),
                                TemplateType = reader.GetString(2),
                                Subject = reader.GetString(3),
                                BodyHtml = reader.GetString(4),
                                IsActive = reader.GetBoolean(5),
                                IsDefault = reader.GetBoolean(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        private async Task<EmailTemplate?> GetTemplateByIdAsync(int templateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT EmailTemplateID, TemplateName, TemplateType, Subject, BodyHtml, IsActive, IsDefault
                    FROM tbl_EmailTemplates
                    WHERE EmailTemplateID = @EmailTemplateID";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EmailTemplateID", templateId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new EmailTemplate
                            {
                                EmailTemplateID = reader.GetInt32(0),
                                TemplateName = reader.GetString(1),
                                TemplateType = reader.GetString(2),
                                Subject = reader.GetString(3),
                                BodyHtml = reader.GetString(4),
                                IsActive = reader.GetBoolean(5),
                                IsDefault = reader.GetBoolean(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        private async Task<List<GuestFeedback>> GetGuestsByIdsAsync(List<int> guestIds)
        {
            var guests = new List<GuestFeedback>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var ids = string.Join(",", guestIds);
                var query = $@"
                    SELECT Id, GuestName, Email, GuestBirthDate, AnniversaryDate
                    FROM GuestFeedback
                    WHERE Id IN ({ids})";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        guests.Add(new GuestFeedback
                        {
                            Id = reader.GetInt32(0),
                            GuestName = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                            GuestBirthDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            AnniversaryDate = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                        });
                    }
                }
            }

            return guests;
        }

        private string ReplacePlaceholders(string template, GuestFeedback guest, MailConfigurationViewModel mailConfig)
        {
            var result = template;
            
            result = result.Replace("{GuestName}", guest.GuestName ?? "Valued Guest");
            result = result.Replace("{RestaurantName}", mailConfig.FromName ?? "Restaurant");
            result = result.Replace("{Year}", DateTime.Now.Year.ToString());
            
            if (guest.GuestBirthDate.HasValue)
            {
                var age = DateTime.Now.Year - guest.GuestBirthDate.Value.Year;
                if (guest.GuestBirthDate.Value > DateTime.Now.AddYears(-age)) age--;
                result = result.Replace("{Age}", age.ToString());
            }
            
            if (guest.AnniversaryDate.HasValue)
            {
                var years = DateTime.Now.Year - guest.AnniversaryDate.Value.Year;
                if (guest.AnniversaryDate.Value > DateTime.Now.AddYears(-years)) years--;
                result = result.Replace("{Years}", years.ToString());
            }
            
            return result;
        }

        private async Task<(bool Success, string? ErrorMessage, int ProcessingTimeMs)> SendEmailAsync(
            MailConfigurationViewModel mailConfig, string? toEmail, string subject, string body)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                {
                    return (false, "Email address is empty", 0);
                }

                var smtpServer = mailConfig.SmtpServer;
                if (!smtpServer.StartsWith("smtp.", StringComparison.OrdinalIgnoreCase) && 
                    !smtpServer.StartsWith("mail.", StringComparison.OrdinalIgnoreCase))
                {
                    if (smtpServer.Contains("gmail.com"))
                        smtpServer = "smtp.gmail.com";
                    else if (smtpServer.Contains("outlook.com") || smtpServer.Contains("hotmail.com"))
                        smtpServer = "smtp.office365.com";
                }

                using (var client = new SmtpClient(smtpServer, mailConfig.SmtpPort))
                {
                    client.EnableSsl = mailConfig.EnableSSL;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(mailConfig.SmtpUsername, mailConfig.SmtpPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 30000;

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(mailConfig.FromEmail, mailConfig.FromName);
                        message.To.Add(toEmail);
                        message.Subject = subject;
                        message.Body = body;
                        message.IsBodyHtml = true;
                        message.Priority = MailPriority.Normal;

                        await client.SendMailAsync(message);
                    }
                }

                stopwatch.Stop();
                return (true, null, (int)stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error sending email to {Email}", toEmail);
                return (false, ex.Message, (int)stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task<MailConfigurationViewModel?> GetMailConfigurationAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, SmtpServer, SmtpPort, SmtpUsername, SmtpPassword, EnableSSL, 
                           FromEmail, FromName, AdminNotificationEmail, IsActive 
                    FROM tbl_MailConfiguration
                    WHERE IsActive = 1";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var encryptedPassword = reader.GetString(4);
                        var decryptedPassword = DecryptPassword(encryptedPassword);
                        
                        return new MailConfigurationViewModel
                        {
                            Id = reader.GetInt32(0),
                            SmtpServer = reader.GetString(1),
                            SmtpPort = reader.GetInt32(2),
                            SmtpUsername = reader.GetString(3),
                            SmtpPassword = decryptedPassword,
                            EnableSSL = reader.GetBoolean(5),
                            FromEmail = reader.GetString(6),
                            FromName = reader.GetString(7),
                            AdminNotificationEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
                            IsActive = reader.GetBoolean(9)
                        };
                    }
                }
            }

            return null;
        }

        private string DecryptPassword(string encryptedPassword)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _encryptionKey;
                    aes.IV = _encryptionIV;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    var cipherBytes = Convert.FromBase64String(encryptedPassword);

                    using (var msDecrypt = new MemoryStream(cipherBytes))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt password, returning as-is");
                return encryptedPassword;
            }
        }

        private async Task LogCampaignHistoryAsync(EmailCampaignHistory history)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        INSERT INTO tbl_EmailCampaignHistory (
                            CampaignType, GuestId, GuestName, GuestEmail, EmailSubject, EmailBody,
                            SentAt, Status, ErrorMessage, ProcessingTimeMs, SentBy
                        ) VALUES (
                            @CampaignType, @GuestId, @GuestName, @GuestEmail, @EmailSubject, @EmailBody,
                            @SentAt, @Status, @ErrorMessage, @ProcessingTimeMs, @SentBy
                        )";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CampaignType", history.CampaignType);
                        command.Parameters.AddWithValue("@GuestId", history.GuestId);
                        command.Parameters.AddWithValue("@GuestName", history.GuestName);
                        command.Parameters.AddWithValue("@GuestEmail", history.GuestEmail);
                        command.Parameters.AddWithValue("@EmailSubject", history.EmailSubject);
                        command.Parameters.AddWithValue("@EmailBody", history.EmailBody);
                        command.Parameters.AddWithValue("@SentAt", history.SentAt);
                        command.Parameters.AddWithValue("@Status", history.Status);
                        command.Parameters.AddWithValue("@ErrorMessage", (object?)history.ErrorMessage ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ProcessingTimeMs", (object?)history.ProcessingTimeMs ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentBy", (object?)history.SentBy ?? DBNull.Value);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging campaign history");
            }
        }

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

        private async Task LogEmailAsync(
            string toEmail,
            string subject,
            string body,
            string status,
            string? errorMessage,
            int? processingTimeMs,
            string? fromEmail,
            string? fromName,
            string? smtpServer,
            int? smtpPort,
            string? emailType = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        INSERT INTO tbl_EmailLog (
                            ToEmail, FromEmail, FromName, Subject, Body, Status, ErrorMessage,
                            SentAt, ProcessingTimeMs, SmtpServer, SmtpPort, SmtpUsername,
                            SmtpUseSsl, SmtpTimeout, EmailType, SentBy, SentFrom
                        ) VALUES (
                            @ToEmail, @FromEmail, @FromName, @Subject, @Body, @Status, @ErrorMessage,
                            @SentAt, @ProcessingTimeMs, @SmtpServer, @SmtpPort, @SmtpUsername,
                            @SmtpUseSsl, @SmtpTimeout, @EmailType, @SentBy, @SentFrom
                        )";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ToEmail", toEmail);
                        command.Parameters.AddWithValue("@FromEmail", (object?)fromEmail ?? DBNull.Value);
                        command.Parameters.AddWithValue("@FromName", (object?)fromName ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Subject", subject);
                        command.Parameters.AddWithValue("@Body", body);
                        command.Parameters.AddWithValue("@Status", status);
                        command.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentAt", DateTime.Now);
                        command.Parameters.AddWithValue("@ProcessingTimeMs", (object?)processingTimeMs ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpServer", (object?)smtpServer ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpPort", (object?)smtpPort ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpUsername", (object?)fromEmail ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpUseSsl", true);
                        command.Parameters.AddWithValue("@SmtpTimeout", 30000);
                        command.Parameters.AddWithValue("@EmailType", (object?)emailType ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentBy", (object?)GetCurrentUserId() ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentFrom", "Email Services");

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging email to tbl_EmailLog");
            }
        }

        #endregion
    }
}
