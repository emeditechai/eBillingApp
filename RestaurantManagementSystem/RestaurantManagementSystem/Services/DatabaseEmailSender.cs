using System;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestaurantManagementSystem.ViewModels;

namespace RestaurantManagementSystem.Services
{
    public class DatabaseEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseEmailSender> _logger;

        public DatabaseEmailSender(IConfiguration configuration, ILogger<DatabaseEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool Success, string? ErrorMessage)> SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string? emailType = null,
            string? sentFrom = null)
        {
            var stopwatch = Stopwatch.StartNew();

            MailConfigurationViewModel? mailConfig = null;
            try
            {
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    return (false, "Recipient email is empty");
                }

                mailConfig = await GetMailConfigurationAsync();
                if (mailConfig == null || !mailConfig.IsActive)
                {
                    stopwatch.Stop();
                    await LogEmailAsync(
                        toEmail,
                        subject,
                        htmlBody,
                        status: "Failed",
                        errorMessage: "Mail configuration is missing or inactive",
                        processingTimeMs: (int)stopwatch.ElapsedMilliseconds,
                        fromEmail: mailConfig?.FromEmail ?? "N/A",
                        smtpServer: mailConfig?.SmtpServer ?? "N/A",
                        smtpPort: mailConfig?.SmtpPort ?? 0,
                        smtpUsername: mailConfig?.SmtpUsername ?? "N/A",
                        enableSsl: mailConfig?.EnableSSL ?? false);

                    return (false, "Mail configuration is missing or inactive");
                }

                var smtpServer = NormalizeSmtpServer(mailConfig.SmtpServer);

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
                        message.Body = htmlBody;
                        message.IsBodyHtml = true;
                        message.Priority = MailPriority.Normal;

                        await client.SendMailAsync(message);
                    }
                }

                stopwatch.Stop();
                await LogEmailAsync(
                    toEmail,
                    subject,
                    htmlBody,
                    status: "Success",
                    errorMessage: null,
                    processingTimeMs: (int)stopwatch.ElapsedMilliseconds,
                    fromEmail: mailConfig.FromEmail,
                    smtpServer: smtpServer,
                    smtpPort: mailConfig.SmtpPort,
                    smtpUsername: mailConfig.SmtpUsername,
                    enableSsl: mailConfig.EnableSSL);

                return (true, null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error sending email to {Email}", toEmail);

                try
                {
                    await LogEmailAsync(
                        toEmail,
                        subject,
                        htmlBody,
                        status: "Failed",
                        errorMessage: ex.Message,
                        processingTimeMs: (int)stopwatch.ElapsedMilliseconds,
                        fromEmail: mailConfig?.FromEmail ?? "N/A",
                        smtpServer: mailConfig?.SmtpServer ?? "N/A",
                        smtpPort: mailConfig?.SmtpPort ?? 0,
                        smtpUsername: mailConfig?.SmtpUsername ?? "N/A",
                        enableSsl: mailConfig?.EnableSSL ?? false);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to log email attempt");
                }

                return (false, ex.Message);
            }
        }

        private async Task<MailConfigurationViewModel?> GetMailConfigurationAsync()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, SmtpServer, SmtpPort, SmtpUsername, SmtpPassword, EnableSSL,
                           FromEmail, FromName, AdminNotificationEmail, IsActive
                    FROM tbl_MailConfiguration
                    WHERE IsActive = 1";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
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
                var encryptionKey = Convert.FromBase64String(_configuration["Encryption:Key"]);
                var encryptionIV = Convert.FromBase64String(_configuration["Encryption:IV"]);

                using (var aes = Aes.Create())
                {
                    aes.Key = encryptionKey;
                    aes.IV = encryptionIV;

                    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    var encryptedBytes = Convert.FromBase64String(encryptedPassword);

                    using (var ms = new System.IO.MemoryStream(encryptedBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new System.IO.StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt SMTP password");
                return encryptedPassword;
            }
        }

        private static string NormalizeSmtpServer(string smtpServer)
        {
            if (string.IsNullOrWhiteSpace(smtpServer)) return smtpServer;

            if (!smtpServer.StartsWith("smtp.", StringComparison.OrdinalIgnoreCase) &&
                !smtpServer.StartsWith("mail.", StringComparison.OrdinalIgnoreCase))
            {
                if (smtpServer.Contains("gmail.com", StringComparison.OrdinalIgnoreCase))
                    return "smtp.gmail.com";
                if (smtpServer.Contains("outlook.com", StringComparison.OrdinalIgnoreCase) ||
                    smtpServer.Contains("hotmail.com", StringComparison.OrdinalIgnoreCase))
                    return "smtp.office365.com";
            }

            return smtpServer;
        }

        private async Task LogEmailAsync(
            string toEmail,
            string subject,
            string body,
            string status,
            string? errorMessage,
            int? processingTimeMs,
            string fromEmail,
            string smtpServer,
            int smtpPort,
            string smtpUsername,
            bool enableSsl)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Keep this insert compatible with the baseline tbl_EmailLog schema.
                    var query = @"
                        INSERT INTO tbl_EmailLog (
                            FromEmail, ToEmail, Subject, EmailBody,
                            SmtpServer, SmtpPort, EnableSSL, SmtpUsername,
                            Status, ErrorMessage,
                            SentAt, ProcessingTimeMs, CreatedAt
                        ) VALUES (
                            @FromEmail, @ToEmail, @Subject, @EmailBody,
                            @SmtpServer, @SmtpPort, @EnableSSL, @SmtpUsername,
                            @Status, @ErrorMessage,
                            @SentAt, @ProcessingTimeMs, @CreatedAt
                        )";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FromEmail", (object?)fromEmail ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ToEmail", (object?)toEmail ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Subject", (object?)subject ?? DBNull.Value);
                        command.Parameters.AddWithValue("@EmailBody", (object?)body ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpServer", (object?)smtpServer ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SmtpPort", smtpPort);
                        command.Parameters.AddWithValue("@EnableSSL", enableSsl);
                        command.Parameters.AddWithValue("@SmtpUsername", (object?)smtpUsername ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
                        command.Parameters.AddWithValue("@ErrorMessage", (object?)errorMessage ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SentAt", DateTime.Now);
                        command.Parameters.AddWithValue("@ProcessingTimeMs", (object?)processingTimeMs ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log email to database");
            }
        }
    }
}
