using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Services;
using RestaurantManagementSystem.Utilities;
using RestaurantManagementSystem.ViewModels.Authorization;
using RestaurantManagementSystem.ViewModels;

namespace RestaurantManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly UserRoleService _userRoleService;
        private readonly ILogger<AccountController> _logger;
        private readonly RestaurantDbContext _db;
        private readonly UrlEncryptionService _urlEncryption;
        private readonly IEmailSender _emailSender;
        
        public AccountController(
            IAuthService authService,
            UserRoleService userRoleService,
            ILogger<AccountController> logger,
            RestaurantDbContext db,
            UrlEncryptionService urlEncryption,
            IEmailSender emailSender)
        {
            _authService = authService;
            _userRoleService = userRoleService;
            _logger = logger;
            _db = db;
            _urlEncryption = urlEncryption;
            _emailSender = emailSender;
        }

        [HttpPost]
        public async Task<IActionResult> ReportClientIp([FromForm] string token, [FromForm] string ip)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(ip)) return BadRequest();

            try
            {
                var updated = await (_authService as Services.AuthService)?.UpdateSessionIpAsync(token, ip);
                if (updated == true) return Ok();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating client IP");
            }

            return StatusCode(500);
        }
        
        [HttpGetAttribute]
        [AllowAnonymousAttribute]
        public IActionResult Login(string returnUrl = null)
        {
            // If user is already authenticated, redirect to home
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }
        
        [HttpPostAttribute]
        [HttpPost]
        [AllowAnonymousAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var (success, message, principal) = await _authService.AuthenticateUserAsync(model.Username, model.Password);
                
                if (success && principal != null)
                {
                    // Check if MFA is required
                    if (principal.HasClaim(c => c.Type == "RequiresMFA" && c.Value == "true"))
                    {
                        // Redirect to MFA verification
                        // In a real application, you would generate and send an MFA code here
                        // For now, we'll simulate MFA by just showing the MFA view
                        TempData["Username"] = principal.Identity.Name;
                        TempData["RememberMe"] = model.RememberMe;
                        TempData["ReturnUrl"] = model.ReturnUrl;
                        
                        return RedirectToAction("VerifyMFA");
                    }
                    
                    // Sign in the user
                    await _authService.SignInUserAsync(principal, model.RememberMe);
                    
                    // Redirect to returnUrl or home page
                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    
                    return RedirectToAction("Index", "Home");
                }
                
                ModelState.AddModelError(string.Empty, message);
            }
            
            return View(model);
        }
        
        [HttpGetAttribute]
        [AllowAnonymousAttribute]
        public IActionResult VerifyMFA()
        {
            // Check if we have the necessary TempData
            if (TempData["UserId"] == null)
            {
                return RedirectToAction("Login");
            }
            
            var model = new MFAViewModel
            {
                UserId = TempData["UserId"].ToString(),
                ReturnUrl = TempData["ReturnUrl"]?.ToString(),
                FactorType = "Email" // Default to Email MFA
            };
            
            // Preserve the TempData for the post action
            TempData.Keep("UserId");
            TempData.Keep("RememberMe");
            TempData.Keep("ReturnUrl");
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [AllowAnonymousAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> VerifyMFA(MFAViewModel model)
        {
            if (ModelState.IsValid)
            {
                // In a real application, you would validate the MFA code here
                // For this demo, we'll accept any 6-digit code
                if (model.VerificationCode.Length == 6 && int.TryParse(model.VerificationCode, out _))
                {
                    // Get the user from the database
                    int userId = int.Parse(TempData["UserId"].ToString());
                    bool rememberMe = (bool)TempData["RememberMe"];
                    
                    // For a real app, you would validate the MFA code against what was sent to the user
                    // Here we're just simulating a successful MFA
                    
                    // Get the user details
                    var (_, _, user) = await _authService.AuthenticateUserAsync("admin", "Admin@123"); // This is just to get the user object
                    
                    if (user != null)
                    {
                        // Sign in the user
                        await _authService.SignInUserAsync(user, rememberMe);
                        
                        // Redirect to returnUrl or home page
                        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                        {
                            return Redirect(model.ReturnUrl);
                        }
                        
                        return RedirectToAction("Index", "Home");
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Invalid verification code");
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> Logout()
        {
            await _authService.SignOutUserAsync();
            return RedirectToAction("Login");
        }
        
        [HttpGetAttribute]
        [AllowAnonymousAttribute]
        public IActionResult Register()
        {
            // If user is already authenticated, redirect to home
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            
            return View();
        }
        
        [HttpPostAttribute]
        [AllowAnonymousAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Convert RegisterViewModel to User model
                var user = new User
                {
                    Username = model.Username,
                    Password = model.Password,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.PhoneNumber,
                    IsActive = true,
                    RequiresMFA = false,
                    SelectedRoleIds = new List<int> { 2 } // Default to regular user role
                };
                
                // Set default role to Staff
                string roleName = "Staff";
                
                var result = await _authService.RegisterUserAsync(user, user.Password, roleName);
                
                if (result.success)
                {
                    // Registration successful, redirect to login
                    TempData["SuccessMessage"] = "Registration successful. You can now log in.";
                    return RedirectToAction("Login");
                }
                
                ModelState.AddModelError(string.Empty, result.message);
            }
            
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = (model.Email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email is required");
                return View(model);
            }

            try
            {
                var user = await FindUserForPasswordResetByEmailAsync(email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "You are Not Registerd");
                    return View(model);
                }

                if (!user.Value.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact administrator.");
                    return View(model);
                }

                if (user.Value.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Your account is locked. Please contact administrator.");
                    return View(model);
                }

                var recipient = (user.Value.Email ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    ModelState.AddModelError(string.Empty, "Your user email is not set. Please contact administrator.");
                    return View(model);
                }

                var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1);
                var token = _urlEncryption.EncryptParameters(new Dictionary<string, string>
                {
                    ["userId"] = user.Value.Id.ToString(),
                    ["email"] = recipient,
                    ["exp"] = expiresAtUtc.ToUnixTimeSeconds().ToString(),
                    ["nonce"] = Guid.NewGuid().ToString("N")
                });

                var resetUrl = Url.Action(
                    "ResetPassword",
                    "Account",
                    new { token },
                    protocol: Request.Scheme);

                if (string.IsNullOrWhiteSpace(resetUrl))
                {
                    ModelState.AddModelError(string.Empty, "Unable to generate reset link. Please contact administrator.");
                    return View(model);
                }

                var subject = "Reset your password";
                                var body = BuildPasswordResetEmailHtml(resetUrl);

                var sendResult = await _emailSender.SendAsync(recipient, subject, body, emailType: "PasswordReset", sentFrom: "Account");
                if (!sendResult.Success)
                {
                    _logger?.LogWarning("Password reset email send failed for {Email}: {Error}", recipient, sendResult.ErrorMessage);
                    ModelState.AddModelError(string.Empty, $"Unable to send reset email. {sendResult.ErrorMessage}");
                    return View(model);
                }

                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing forgot password for {Email}", email);
                ModelState.AddModelError(string.Empty, "Unable to process your request. Please try again.");
                return View(model);
            }
        }

        private async Task<(int Id, string? Email, bool IsActive, bool IsLockedOut)?> FindUserForPasswordResetByEmailAsync(string email)
        {
            var trimmedEmail = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedEmail)) return null;

            // Avoid materializing the EF User entity here.
            // Some deployments have legacy/null values in bit columns (or schema drift) that can throw
            // during entity materialization. This ADO.NET lookup is resilient and scoped.
            await using var connection = _db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TOP (1)
    Id,
    Email,
    ISNULL(CAST(IsActive AS bit), 1) AS IsActive,
    ISNULL(CAST(IsLockedOut AS bit), 0) AS IsLockedOut
FROM Users
WHERE Email IS NOT NULL
  AND (Email = @Email OR LTRIM(RTRIM(Email)) = @Email)
ORDER BY Id ASC";

            var p = command.CreateParameter();
            p.ParameterName = "@Email";
            p.Value = trimmedEmail;
            command.Parameters.Add(p);

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await reader.ReadAsync()) return null;

            var id = reader.GetInt32(0);
            var dbEmail = reader.IsDBNull(1) ? null : reader.GetString(1);
            var isActive = !reader.IsDBNull(2) && reader.GetBoolean(2);
            var isLockedOut = !reader.IsDBNull(3) && reader.GetBoolean(3);

            return (id, dbEmail, isActive, isLockedOut);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return View(new ResetPasswordViewModel { Token = token, Email = string.Empty });
            }

            try
            {
                var payload = _urlEncryption.DecryptParameters(token);
                payload.TryGetValue("email", out var email);
                payload.TryGetValue("exp", out var exp);

                if (!IsTokenValid(exp))
                {
                    ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
                }

                return View(new ResetPasswordViewModel { Token = token, Email = email ?? string.Empty });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
                return View(new ResetPasswordViewModel { Token = token, Email = string.Empty });
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var payload = _urlEncryption.DecryptParameters(model.Token);

                payload.TryGetValue("userId", out var userIdRaw);
                payload.TryGetValue("email", out var email);
                payload.TryGetValue("exp", out var exp);

                if (!IsTokenValid(exp) ||
                    string.IsNullOrWhiteSpace(userIdRaw) ||
                    !int.TryParse(userIdRaw, out var userId))
                {
                    ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
                    return View(model);
                }

                // Ensure the posted email matches the token email (prevents token reuse with a different email field)
                if (!string.Equals((model.Email ?? string.Empty).Trim(), (email ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
                    return View(model);
                }

                var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && !u.IsLockedOut);
                if (!userExists)
                {
                    ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired.");
                    return View(model);
                }

                var result = await _authService.UpdatePasswordAsync(userId, model.Password);
                if (!result.success)
                {
                    ModelState.AddModelError(string.Empty, result.message);
                    return View(model);
                }

                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resetting password");
                ModelState.AddModelError(string.Empty, "Unable to reset password. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestPasswordResetEmail([FromForm] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new { success = false, message = "Email is required" });
            }

            var trimmedEmail = email.Trim();
            var normalizedEmail = trimmedEmail.ToLowerInvariant();

            try
            {
                var user = await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.Email != null &&
                        u.Email.Trim().ToLower() == normalizedEmail);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "No user found with this email" });
                }

                var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1);
                var token = _urlEncryption.EncryptParameters(new Dictionary<string, string>
                {
                    ["userId"] = user.Id.ToString(),
                    ["email"] = user.Email ?? string.Empty,
                    ["exp"] = expiresAtUtc.ToUnixTimeSeconds().ToString(),
                    ["nonce"] = Guid.NewGuid().ToString("N")
                });

                var resetUrl = Url.Action(
                    "ResetPassword",
                    "Account",
                    new { token },
                    protocol: Request.Scheme);

                if (string.IsNullOrWhiteSpace(resetUrl))
                {
                    return StatusCode(500, new { success = false, message = "Failed to generate reset URL" });
                }

                var subject = "Reset your password";
                var body = BuildPasswordResetEmailHtml(resetUrl, isTestEmail: true);

                var sendResult = await _emailSender.SendAsync(user.Email, subject, body, emailType: "PasswordReset", sentFrom: "Account-Test");

                return Ok(new
                {
                    success = sendResult.Success,
                    message = sendResult.Success ? "Email sent" : "Email failed",
                    error = sendResult.ErrorMessage,
                    resetUrl
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "TestPasswordResetEmail failed for {Email}", trimmedEmail);
                return StatusCode(500, new { success = false, message = "Error occurred", error = ex.Message });
            }
        }

        private static bool IsTokenValid(string? expUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(expUnixSeconds))
            {
                return false;
            }

            if (!long.TryParse(expUnixSeconds, out var exp))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return exp > now;
        }

                private string BuildPasswordResetEmailHtml(string resetUrl, bool isTestEmail = false)
                {
                        var safeUrl = WebUtility.HtmlEncode(resetUrl ?? string.Empty);
                        var year = DateTime.UtcNow.Year;
                        var titleLine = isTestEmail ? "Password reset (test)" : "Password reset";

                        // Table-based layout + inline styles for broad email client compatibility.
                        return $@"
<!doctype html>
<html lang='en'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <meta name='x-apple-disable-message-reformatting'>
    <title>{titleLine}</title>
</head>
<body style='margin:0;padding:0;background-color:#f3f4f6;'>
    <div style='display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;'>
        Use the link below to reset your password. This link expires in 1 hour.
    </div>

    <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='background-color:#f3f4f6;'>
        <tr>
            <td align='center' style='padding:24px 12px;'>
                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='560' style='width:100%;max-width:560px;background:#ffffff;border-radius:14px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);'>
                    <tr>
                        <td style='padding:22px 24px;background:linear-gradient(135deg,#ff3b30,#ff2d55);'>
                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:22px;font-weight:700;letter-spacing:0.2px;color:#ffffff;'>eRestoPOS</div>
                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:rgba(255,255,255,0.92);margin-top:4px;'>Restaurant Management System</div>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:24px;'>
                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:18px;font-weight:700;color:#111827;margin:0 0 10px 0;'>{titleLine}</div>
                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.6;color:#374151;'>
                                We received a request to reset your password. Click the button below to continue.
                            </div>

                            <table role='presentation' cellspacing='0' cellpadding='0' border='0' style='margin:18px 0 18px 0;'>
                                <tr>
                                    <td align='center' bgcolor='#2563eb' style='border-radius:10px;'>
                                        <a href='{safeUrl}'
                                             style='display:inline-block;font-family:Segoe UI,Arial,sans-serif;font-size:14px;font-weight:700;line-height:1;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:10px;'>
                                            Reset Password
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:13px;line-height:1.6;color:#6b7280;'>
                                This link expires in <b>1 hour</b>. If you did not request this, you can safely ignore this email.
                            </div>

                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:12px;line-height:1.6;color:#9ca3af;margin-top:16px;'>
                                Having trouble with the button? Copy and paste this link into your browser:<br>
                                <a href='{safeUrl}' style='color:#2563eb;text-decoration:underline;word-break:break-all;'>{safeUrl}</a>
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:16px 24px;background:#f9fafb;border-top:1px solid #e5e7eb;'>
                            <div style='font-family:Segoe UI,Arial,sans-serif;font-size:12px;line-height:1.6;color:#6b7280;'>
                                &copy; {year} Restaurant Management System. All rights reserved.
                            </div>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
                }
        
        [HttpGetAttribute]
        [AuthorizeAttribute]
        public IActionResult ChangePassword()
        {
            return View();
        }
        
        [HttpPostAttribute]
        [AuthorizeAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var result = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
                    
                    if (result.success)
                    {
                        TempData["SuccessMessage"] = "Password changed successfully.";
                        return RedirectToAction("Index", "Home");
                    }
                    
                    ModelState.AddModelError(string.Empty, result.message);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "User ID not found");
                }
            }
            
            return View(model);
        }
        
        [HttpGetAttribute]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UserList()
        {
            var users = await _authService.GetUsersAsync();
            var model = new UserListViewModel
            {
                Users = users,
                Pagination = new PaginationViewModel
                {
                    CurrentPage = 1,
                    ItemsPerPage = 20,
                    TotalItems = users.Count
                }
            };
            
            return View(model);
        }
        
        [HttpGetAttribute]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> EditUser(int id)
        {
            var model = await _authService.GetUserForEditAsync(id);
            
            if (model == null)
            {
                return NotFound();
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> EditUser(User model)
        {
            if (ModelState.IsValid)
            {
                // Get the current user ID for audit
                int updatedByUserId = 1; // Default to system user
                if (User.Identity.IsAuthenticated && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                {
                    updatedByUserId = userId;
                }
                
                var result = await _authService.UpdateUserAsync(model, updatedByUserId);
                
                if (result.success)
                {
                    TempData["SuccessMessage"] = "User updated successfully.";
                    return RedirectToAction("UserList");
                }
                
                ModelState.AddModelError(string.Empty, result.message);
            }
            
            // Re-populate available roles for the dropdown
            ViewBag.Roles = await _userRoleService.GetAllRolesAsync();
            
            return View(model);
        }
        
        [HttpGetAttribute]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> UserRoles()
        {
            var userId = User.GetUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var roles = await _userRoleService.GetUserRolesAsync(userId.Value);
            var activeRoleId = User.GetActiveRoleId();

            var payload = roles.Select(role => new RoleSelectionOptionViewModel
            {
                RoleId = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = activeRoleId.HasValue && activeRoleId.Value == role.Id
            }).ToList();

            return Json(payload);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchRole([FromBody] SwitchRoleRequest request)
        {
            if (request == null || request.RoleId <= 0)
            {
                return BadRequest(new { message = "Invalid role selection." });
            }

            var result = await _authService.SwitchRoleAsync(User, request.RoleId);
            if (!result.success)
            {
                return BadRequest(new { message = result.message });
            }

            return Ok(new { message = result.message });
        }
    }
}
