using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Services;
using RestaurantManagementSystem.ViewModels;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    public class SettingsController : Controller
    {
        private readonly RestaurantDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SettingsService _settingsService;

        public SettingsController(RestaurantDbContext dbContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
            _settingsService = new SettingsService(dbContext, _configuration.GetConnectionString("DefaultConnection"));
            
            // Ensure the RestaurantSettings table exists when the controller is first initialized
            _settingsService.EnsureSettingsTableExistsAsync().GetAwaiter().GetResult();
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            try
            {
                // Try to ensure the settings table exists
                bool tableCreated = await _settingsService.EnsureSettingsTableExistsAsync();
                
                // If we just created the table, show success message
                if (tableCreated)
                {
                    TempData["SuccessMessage"] = "Restaurant Settings table created successfully.";
                }
                
                var settings = await _settingsService.GetSettingsAsync();
                var viewModel = MapToViewModel(settings);
                return View(viewModel);
            }
            catch (SqlException sqlEx) when (sqlEx.Message.Contains("Invalid object name"))
            {
                // This is a specific case when the table doesn't exist
                TempData["ErrorMessage"] = "The Restaurant Settings table does not exist in the database.";
                return View("SettingsSetup");
            }
            catch (Exception ex)
            {
                // General exception handling
                TempData["ErrorMessage"] = $"Error loading settings: {ex.Message}";
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        // GET: Settings/Edit
        public async Task<IActionResult> Edit()
        {
            try
            {
                // Try to ensure the settings table exists
                await _settingsService.EnsureSettingsTableExistsAsync();
                
                var settings = await _settingsService.GetSettingsAsync();
                var viewModel = MapToViewModel(settings);
                return View(viewModel);
            }
            catch (SqlException sqlEx) when (sqlEx.Message.Contains("Invalid object name"))
            {
                // This is a specific case when the table doesn't exist
                TempData["ErrorMessage"] = "The Restaurant Settings table does not exist in the database.";
                return View("SettingsSetup");
            }
            catch (Exception ex)
            {
                // General exception handling
                TempData["ErrorMessage"] = $"Error loading settings: {ex.Message}";
                return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }
        }

        // POST: Settings/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RestaurantSettingsViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                // Handle logo file upload if a new logo was provided
                if (viewModel.LogoFile != null && viewModel.LogoFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "restaurant");
                    
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Generate unique filename
                    string uniqueFileName = $"logo_{DateTime.Now.ToString("yyyyMMddHHmmss")}{Path.GetExtension(viewModel.LogoFile.FileName)}";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    // Save the file
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await viewModel.LogoFile.CopyToAsync(fileStream);
                    }
                    
                    // Update the logo path
                    viewModel.LogoPath = $"/images/restaurant/{uniqueFileName}";
                }

                // Map view model to domain model
                var settings = MapToModel(viewModel);
                
                // Update settings
                await _settingsService.UpdateSettingsAsync(settings);
                
                TempData["SuccessMessage"] = "Restaurant settings updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating settings: {ex.Message}");
                return View(viewModel);
            }
        }

        // Helper methods
        private RestaurantSettingsViewModel MapToViewModel(RestaurantSettings model)
        {
            return new RestaurantSettingsViewModel
            {
                Id = model.Id,
                RestaurantName = model.RestaurantName,
                StreetAddress = model.StreetAddress,
                City = model.City,
                State = model.State,
                Pincode = model.Pincode,
                Country = model.Country,
                GSTCode = model.GSTCode,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                Website = model.Website,
                LogoPath = model.LogoPath,
                CurrencySymbol = model.CurrencySymbol,
                DefaultGSTPercentage = model.DefaultGSTPercentage,
                TakeAwayGSTPercentage = model.TakeAwayGSTPercentage,
                BarGSTPerc = model.BarGSTPerc,
                IsDefaultGSTRequired = model.IsDefaultGSTRequired,
                	IsTakeAwayGSTRequired = model.IsTakeAwayGSTRequired,
                	IsTakeawayIncludedGSTReq = model.IsTakeawayIncludedGSTReq,
                IsKOTBillPrintRequired = model.IsKOTBillPrintRequired,
                IsDiscountApprovalRequired = model.IsDiscountApprovalRequired,
                IsCardPaymentApprovalRequired = model.IsCardPaymentApprovalRequired,
                IsReqAutoSentbillEmail = model.IsReqAutoSentbillEmail,
                BillFormat = model.BillFormat,
                	FssaiNo = model.FssaiNo,
                CreatedAt = model.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
                UpdatedAt = model.UpdatedAt.ToString("dd MMM yyyy, hh:mm tt")
            };
        }

        private RestaurantSettings MapToModel(RestaurantSettingsViewModel viewModel)
        {
            return new RestaurantSettings
            {
                Id = viewModel.Id,
                RestaurantName = viewModel.RestaurantName,
                StreetAddress = viewModel.StreetAddress,
                City = viewModel.City,
                State = viewModel.State,
                Pincode = viewModel.Pincode,
                Country = viewModel.Country,
                GSTCode = viewModel.GSTCode,
                PhoneNumber = viewModel.PhoneNumber,
                Email = viewModel.Email,
                Website = viewModel.Website,
                LogoPath = viewModel.LogoPath ?? "", // Use existing path if no new logo was uploaded
                CurrencySymbol = viewModel.CurrencySymbol,
                DefaultGSTPercentage = viewModel.DefaultGSTPercentage,
                TakeAwayGSTPercentage = viewModel.TakeAwayGSTPercentage,
                BarGSTPerc = viewModel.BarGSTPerc,
                IsDefaultGSTRequired = viewModel.IsDefaultGSTRequired,
                IsTakeAwayGSTRequired = viewModel.IsTakeAwayGSTRequired,
                IsTakeawayIncludedGSTReq = viewModel.IsTakeawayIncludedGSTReq,
                IsKOTBillPrintRequired = viewModel.IsKOTBillPrintRequired,
                IsDiscountApprovalRequired = viewModel.IsDiscountApprovalRequired,
                IsCardPaymentApprovalRequired = viewModel.IsCardPaymentApprovalRequired,
                IsReqAutoSentbillEmail = viewModel.IsReqAutoSentbillEmail,
                BillFormat = viewModel.BillFormat,
                FssaiNo = viewModel.FssaiNo,
                UpdatedAt = DateTime.Now
            };
        }
    }
}