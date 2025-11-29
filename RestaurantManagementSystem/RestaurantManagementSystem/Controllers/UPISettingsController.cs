using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize]
    public class UPISettingsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public UPISettingsController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        // GET: UPISettings
        public IActionResult Index()
        {
            var model = new UPISettingsViewModel();

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    string query = "SELECT TOP 1 UPIId, PayeeName, IsEnabled FROM UPISettings ORDER BY Id DESC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.UPIId = reader.GetString(0);
                            model.PayeeName = reader.GetString(1);
                            model.IsEnabled = reader.GetBoolean(2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                model.Message = $"Error loading settings: {ex.Message}";
                model.IsSuccess = false;
            }

            return View(model);
        }

        // POST: UPISettings/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(UPISettingsViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.UPIId))
            {
                model.Message = "UPI ID is required";
                model.IsSuccess = false;
                return View("Index", model);
            }

            if (string.IsNullOrWhiteSpace(model.PayeeName))
            {
                model.Message = "Payee Name is required";
                model.IsSuccess = false;
                return View("Index", model);
            }

            // Validate UPI ID format (basic validation)
            if (!model.UPIId.Contains("@"))
            {
                model.Message = "Invalid UPI ID format. Should be like: username@bank";
                model.IsSuccess = false;
                return View("Index", model);
            }

            try
            {
                int? currentUserId = GetCurrentUserId();

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Check if record exists
                    string checkQuery = "SELECT COUNT(*) FROM UPISettings";
                    bool recordExists = false;

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        recordExists = (int)checkCommand.ExecuteScalar() > 0;
                    }

                    string query;
                    if (recordExists)
                    {
                        query = @"UPDATE UPISettings 
                                 SET UPIId = @UPIId, 
                                     PayeeName = @PayeeName, 
                                     IsEnabled = @IsEnabled, 
                                     UpdatedAt = GETDATE(),
                                     UpdatedBy = @UpdatedBy
                                 WHERE Id = (SELECT TOP 1 Id FROM UPISettings ORDER BY Id DESC)";
                    }
                    else
                    {
                        query = @"INSERT INTO UPISettings (UPIId, PayeeName, IsEnabled, UpdatedBy) 
                                 VALUES (@UPIId, @PayeeName, @IsEnabled, @UpdatedBy)";
                    }

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UPIId", model.UPIId.Trim());
                        command.Parameters.AddWithValue("@PayeeName", model.PayeeName.Trim());
                        command.Parameters.AddWithValue("@IsEnabled", model.IsEnabled);
                        command.Parameters.AddWithValue("@UpdatedBy", currentUserId.HasValue ? (object)currentUserId.Value : DBNull.Value);

                        command.ExecuteNonQuery();
                    }
                }

                model.Message = "UPI settings saved successfully!";
                model.IsSuccess = true;
            }
            catch (Exception ex)
            {
                model.Message = $"Error saving settings: {ex.Message}";
                model.IsSuccess = false;
            }

            return View("Index", model);
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}
