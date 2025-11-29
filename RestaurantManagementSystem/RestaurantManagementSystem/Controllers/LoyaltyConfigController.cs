using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Models.Authorization;
using RestaurantManagementSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [RequirePermission("NAV_SETTINGS_LOYALTY", PermissionAction.View)]
    public class LoyaltyConfigController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<LoyaltyConfigController> _logger;

        public LoyaltyConfigController(
            IConfiguration configuration,
            ILogger<LoyaltyConfigController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string not found");
            _logger = logger;
        }

        // GET: LoyaltyConfig
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new LoyaltyConfigViewModel();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"SELECT Id, OutletType, EarnRate, RedemptionValue, 
                                  MinBillToEarn, MaxPointsPerBill, ExpiryDays, 
                                  EligiblePaymentModes, IsActive
                                  FROM LoyaltyConfig
                                  WHERE IsActive = 1
                                  ORDER BY OutletType";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var config = new LoyaltyConfigItem
                            {
                                Id = reader.GetInt32(0),
                                OutletType = reader.GetString(1),
                                EarnRate = reader.GetDecimal(2),
                                RedemptionValue = reader.GetDecimal(3),
                                MinBillToEarn = reader.GetDecimal(4),
                                MaxPointsPerBill = reader.GetDecimal(5),
                                ExpiryDays = reader.GetInt32(6),
                                EligiblePaymentModes = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                IsActive = reader.GetBoolean(8)
                            };

                            if (config.OutletType == "RESTAURANT")
                                viewModel.RestaurantConfig = config;
                            else if (config.OutletType == "BAR")
                                viewModel.BarConfig = config;
                        }
                    }
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loyalty configuration");
                TempData["ErrorMessage"] = "Error loading loyalty configuration";
                return View(new LoyaltyConfigViewModel());
            }
        }

        // POST: LoyaltyConfig/SaveConfiguration
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("NAV_SETTINGS_LOYALTY", PermissionAction.Edit)]
        public async Task<IActionResult> SaveConfiguration(LoyaltyConfigViewModel model)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Update Restaurant Config
                    if (model.RestaurantConfig != null)
                    {
                        await UpdateConfig(connection, model.RestaurantConfig);
                    }

                    // Update Bar Config
                    if (model.BarConfig != null)
                    {
                        await UpdateConfig(connection, model.BarConfig);
                    }
                }

                TempData["SuccessMessage"] = "Loyalty configuration saved successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving loyalty configuration");
                TempData["ErrorMessage"] = "Error saving loyalty configuration: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task UpdateConfig(SqlConnection connection, LoyaltyConfigItem config)
        {
            var query = @"UPDATE LoyaltyConfig 
                          SET EarnRate = @EarnRate,
                              RedemptionValue = @RedemptionValue,
                              MinBillToEarn = @MinBillToEarn,
                              MaxPointsPerBill = @MaxPointsPerBill,
                              ExpiryDays = @ExpiryDays,
                              EligiblePaymentModes = @EligiblePaymentModes,
                              LastModifiedDate = GETDATE()
                          WHERE OutletType = @OutletType";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@EarnRate", config.EarnRate);
                command.Parameters.AddWithValue("@RedemptionValue", config.RedemptionValue);
                command.Parameters.AddWithValue("@MinBillToEarn", config.MinBillToEarn);
                command.Parameters.AddWithValue("@MaxPointsPerBill", config.MaxPointsPerBill);
                command.Parameters.AddWithValue("@ExpiryDays", config.ExpiryDays);
                command.Parameters.AddWithValue("@EligiblePaymentModes", config.EligiblePaymentModes ?? string.Empty);
                command.Parameters.AddWithValue("@OutletType", config.OutletType);

                await command.ExecuteNonQueryAsync();
            }
        }

        // GET: LoyaltyConfig/SearchGuest
        [HttpGet]
        public async Task<IActionResult> SearchGuest(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return Json(new { success = false, message = "Search term is required" });
                }

                var guests = new List<GuestLoyaltyViewModel>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand("sp_GetGuestLoyaltyDetails", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@SearchTerm", searchTerm);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                guests.Add(new GuestLoyaltyViewModel
                                {
                                    CardNo = reader.GetString(0),
                                    GuestName = reader.GetString(1),
                                    Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    JoinDate = reader.GetDateTime(4),
                                    TotalPoints = reader.GetDecimal(5),
                                    Status = reader.GetString(6),
                                    DaysSinceJoined = reader.GetInt32(7)
                                });
                            }
                        }
                    }
                }

                return Json(new { success = true, guests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching guest");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: LoyaltyConfig/CalculatePoints
        [HttpPost]
        public async Task<IActionResult> CalculatePoints([FromBody] LoyaltyEarnRequest request)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand("sp_CalculatePointsToEarn", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@BillAmount", request.BillAmount);
                        command.Parameters.AddWithValue("@OutletType", request.OutletType);
                        command.Parameters.AddWithValue("@PaymentMethodId", request.PaymentMode); // Now expects ID

                        var pointsParam = new SqlParameter("@PointsEarned", System.Data.SqlDbType.Decimal)
                        {
                            Direction = System.Data.ParameterDirection.Output,
                            Precision = 10,
                            Scale = 2
                        };
                        command.Parameters.Add(pointsParam);

                        var eligibleParam = new SqlParameter("@IsEligible", System.Data.SqlDbType.Bit)
                        {
                            Direction = System.Data.ParameterDirection.Output
                        };
                        command.Parameters.Add(eligibleParam);

                        await command.ExecuteNonQueryAsync();

                        var pointsEarned = (decimal)pointsParam.Value;
                        var isEligible = (bool)eligibleParam.Value;

                        return Json(new
                        {
                            success = true,
                            isEligible,
                            pointsEarned,
                            message = isEligible
                                ? $"You will earn {pointsEarned} points on ₹{request.BillAmount:N2} ({request.OutletType})"
                                : "Bill does not qualify for loyalty points"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating points");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: LoyaltyConfig/GetPaymentMethods
        [HttpGet]
        public async Task<IActionResult> GetPaymentMethods()
        {
            try
            {
                var paymentMethods = new List<object>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"SELECT Id, Name, DisplayName, IsActive 
                                  FROM PaymentMethods 
                                  WHERE IsActive = 1
                                  ORDER BY DisplayName";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            paymentMethods.Add(new
                            {
                                id = reader.GetInt32(0),
                                name = reader.GetString(1),
                                displayName = reader.GetString(2)
                            });
                        }
                    }
                }

                return Json(new { success = true, paymentMethods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payment methods");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
