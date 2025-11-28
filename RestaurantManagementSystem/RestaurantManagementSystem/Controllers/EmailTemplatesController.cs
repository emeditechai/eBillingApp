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
    [RequirePermission("NAV_SETTINGS_EMAIL_TEMPLATES", PermissionAction.View)]
    public class EmailTemplatesController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<EmailTemplatesController> _logger;

        public EmailTemplatesController(
            IConfiguration configuration,
            ILogger<EmailTemplatesController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
        }

        // GET: EmailTemplates
        public async Task<IActionResult> Index()
        {
            try
            {
                var templates = await GetAllTemplatesAsync();
                return View(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading email templates");
                TempData["ErrorMessage"] = $"Error loading templates: {ex.Message}";
                return View(new List<EmailTemplate>());
            }
        }

        // GET: EmailTemplates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: EmailTemplates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmailTemplate template)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await CreateTemplateAsync(template);
                    TempData["SuccessMessage"] = "Email template created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                return View(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating email template");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(template);
            }
        }

        // GET: EmailTemplates/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var template = await GetTemplateByIdAsync(id);
                if (template == null)
                {
                    TempData["ErrorMessage"] = "Template not found";
                    return RedirectToAction(nameof(Index));
                }
                return View(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template for edit");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: EmailTemplates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmailTemplate template)
        {
            try
            {
                if (id != template.EmailTemplateID)
                {
                    return NotFound();
                }

                if (ModelState.IsValid)
                {
                    await UpdateTemplateAsync(template);
                    TempData["SuccessMessage"] = "Email template updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                return View(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating email template");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(template);
            }
        }

        // POST: EmailTemplates/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await DeleteTemplateAsync(id);
                TempData["SuccessMessage"] = "Email template deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting email template");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: EmailTemplates/SetDefault/5
        [HttpPost]
        public async Task<IActionResult> SetDefault(int id, string templateType)
        {
            try
            {
                await SetDefaultTemplateAsync(id, templateType);
                return Json(new { success = true, message = "Default template set successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default template");
                return Json(new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

        private async Task<List<EmailTemplate>> GetAllTemplatesAsync()
        {
            var templates = new List<EmailTemplate>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT EmailTemplateID, TemplateName, TemplateType, Subject, BodyHtml, 
                           IsActive, IsDefault, CreatedAt, UpdatedAt
                    FROM tbl_EmailTemplates
                    ORDER BY TemplateType, TemplateName";

                using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        templates.Add(new EmailTemplate
                        {
                            EmailTemplateID = reader.GetInt32(0),
                            TemplateName = reader.GetString(1),
                            TemplateType = reader.GetString(2),
                            Subject = reader.GetString(3),
                            BodyHtml = reader.GetString(4),
                            IsActive = reader.GetBoolean(5),
                            IsDefault = reader.GetBoolean(6),
                            CreatedAt = reader.GetDateTime(7),
                            UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                        });
                    }
                }
            }

            return templates;
        }

        private async Task<EmailTemplate?> GetTemplateByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT EmailTemplateID, TemplateName, TemplateType, Subject, BodyHtml, 
                           IsActive, IsDefault
                    FROM tbl_EmailTemplates
                    WHERE EmailTemplateID = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);

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

        private async Task CreateTemplateAsync(EmailTemplate template)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    INSERT INTO tbl_EmailTemplates (
                        TemplateName, TemplateType, Subject, BodyHtml, 
                        IsActive, IsDefault, CreatedBy, CreatedAt
                    ) VALUES (
                        @TemplateName, @TemplateType, @Subject, @BodyHtml,
                        @IsActive, @IsDefault, @CreatedBy, GETDATE()
                    )";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TemplateName", template.TemplateName);
                    command.Parameters.AddWithValue("@TemplateType", template.TemplateType);
                    command.Parameters.AddWithValue("@Subject", template.Subject);
                    command.Parameters.AddWithValue("@BodyHtml", template.BodyHtml);
                    command.Parameters.AddWithValue("@IsActive", template.IsActive);
                    command.Parameters.AddWithValue("@IsDefault", template.IsDefault);
                    command.Parameters.AddWithValue("@CreatedBy", (object?)GetCurrentUserId() ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateTemplateAsync(EmailTemplate template)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    UPDATE tbl_EmailTemplates
                    SET TemplateName = @TemplateName,
                        TemplateType = @TemplateType,
                        Subject = @Subject,
                        BodyHtml = @BodyHtml,
                        IsActive = @IsActive,
                        IsDefault = @IsDefault,
                        UpdatedBy = @UpdatedBy,
                        UpdatedAt = GETDATE()
                    WHERE EmailTemplateID = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", template.EmailTemplateID);
                    command.Parameters.AddWithValue("@TemplateName", template.TemplateName);
                    command.Parameters.AddWithValue("@TemplateType", template.TemplateType);
                    command.Parameters.AddWithValue("@Subject", template.Subject);
                    command.Parameters.AddWithValue("@BodyHtml", template.BodyHtml);
                    command.Parameters.AddWithValue("@IsActive", template.IsActive);
                    command.Parameters.AddWithValue("@IsDefault", template.IsDefault);
                    command.Parameters.AddWithValue("@UpdatedBy", (object?)GetCurrentUserId() ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DeleteTemplateAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "DELETE FROM tbl_EmailTemplates WHERE EmailTemplateID = @Id";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task SetDefaultTemplateAsync(int id, string templateType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // First, remove default from all templates of this type
                var query1 = @"
                    UPDATE tbl_EmailTemplates
                    SET IsDefault = 0
                    WHERE TemplateType = @TemplateType";

                using (var command = new SqlCommand(query1, connection))
                {
                    command.Parameters.AddWithValue("@TemplateType", templateType);
                    await command.ExecuteNonQueryAsync();
                }

                // Then set this template as default
                var query2 = @"
                    UPDATE tbl_EmailTemplates
                    SET IsDefault = 1
                    WHERE EmailTemplateID = @Id";

                using (var command = new SqlCommand(query2, connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    await command.ExecuteNonQueryAsync();
                }
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

        #endregion
    }
}
