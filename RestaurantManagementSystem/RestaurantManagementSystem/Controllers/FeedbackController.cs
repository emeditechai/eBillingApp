using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly string _connectionString;
        public FeedbackController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: /Feedback/Form
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Form()
        {
            var model = new GuestFeedback
            {
                VisitDate = DateTime.Today,
                OverallRating = 5
            };
            // Load restaurant header details for display on the form
            try
            {
                using var con = new SqlConnection(_connectionString);
                con.Open();
                using var cmd = new SqlCommand("SELECT TOP 1 RestaurantName, StreetAddress, City, State, Pincode, Email, Website FROM RestaurantSettings ORDER BY Id DESC", con);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    ViewBag.Restaurant = new
                    {
                        Name = reader["RestaurantName"] as string,
                        Address = string.Join(", ", new[]
                        {
                            reader["StreetAddress"] as string,
                            reader["City"] as string,
                            reader["State"] as string,
                            reader["Pincode"] as string
                        }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        Email = reader["Email"] as string,
                        Website = reader["Website"] as string
                    };
                }
            }
            catch { /* non-blocking */ }
            return View(model);
        }

        // POST: /Feedback/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Submit(GuestFeedback model)
        {
            if (model.OverallRating < 1 || model.OverallRating > 5)
            {
                ModelState.AddModelError("OverallRating", "Overall rating must be between 1 and 5.");
            }
            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            try
            {
                // Log received data for debugging
                Console.WriteLine($"=== Feedback Submission Debug ===");
                Console.WriteLine($"VisitDate: {model.VisitDate}");
                Console.WriteLine($"OverallRating: {model.OverallRating}");
                Console.WriteLine($"Location: {model.Location}");
                Console.WriteLine($"FirstVisit: {model.FirstVisit}");
                Console.WriteLine($"SurveyJson length: {model.SurveyJson?.Length ?? 0}");
                Console.WriteLine($"SurveyJson: {model.SurveyJson}");
                Console.WriteLine($"Tags: {model.Tags}");
                Console.WriteLine($"Comments length: {model.Comments?.Length ?? 0}");
                Console.WriteLine($"Guest Birth Date: {model.GuestBirthDate?.ToShortDateString() ?? "(none)"}");
                Console.WriteLine($"Anniversary Date: {model.AnniversaryDate?.ToShortDateString() ?? "(none)"}");

                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();

                // Discover available parameters on the SP to maintain backward compatibility
                var availableParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var pCmd = new SqlCommand("SELECT REPLACE(name,'@','') as name FROM sys.parameters WHERE object_id = OBJECT_ID('dbo.usp_SubmitGuestFeedback')", con))
                using (var pReader = await pCmd.ExecuteReaderAsync())
                {
                    while (await pReader.ReadAsync())
                    {
                        availableParams.Add(pReader.GetString(0));
                    }
                }
                Console.WriteLine($"SP has {availableParams.Count} parameters: {string.Join(", ", availableParams)}");

                using var cmd = new SqlCommand("usp_SubmitGuestFeedback", con)
                {
                    CommandType = CommandType.StoredProcedure
                };
                if (availableParams.Contains("VisitDate")) cmd.Parameters.AddWithValue("@VisitDate", model.VisitDate.Date);
                if (availableParams.Contains("OverallRating")) cmd.Parameters.AddWithValue("@OverallRating", model.OverallRating);
                if (availableParams.Contains("FoodRating")) cmd.Parameters.AddWithValue("@FoodRating", (object?)model.FoodRating ?? DBNull.Value);
                if (availableParams.Contains("ServiceRating")) cmd.Parameters.AddWithValue("@ServiceRating", (object?)model.ServiceRating ?? DBNull.Value);
                if (availableParams.Contains("CleanlinessRating")) cmd.Parameters.AddWithValue("@CleanlinessRating", (object?)model.CleanlinessRating ?? DBNull.Value);
                if (availableParams.Contains("StaffRating")) cmd.Parameters.AddWithValue("@StaffRating", (object?)model.StaffRating ?? DBNull.Value);
                // New detailed rating params (nullable)
                if (availableParams.Contains("AmbienceRating")) cmd.Parameters.AddWithValue("@AmbienceRating", (object?)model.AmbienceRating ?? DBNull.Value);
                if (availableParams.Contains("ValueRating")) cmd.Parameters.AddWithValue("@ValueRating", (object?)model.ValueRating ?? DBNull.Value);
                if (availableParams.Contains("SpeedRating")) cmd.Parameters.AddWithValue("@SpeedRating", (object?)model.SpeedRating ?? DBNull.Value);
                if (availableParams.Contains("Location")) cmd.Parameters.AddWithValue("@Location", (object?)model.Location ?? DBNull.Value);
                if (availableParams.Contains("IsFirstVisit")) cmd.Parameters.AddWithValue("@IsFirstVisit", (object?)model.FirstVisit ?? DBNull.Value);
                if (availableParams.Contains("SurveyJson")) 
                {
                    cmd.Parameters.AddWithValue("@SurveyJson", (object?)model.SurveyJson ?? DBNull.Value);
                    Console.WriteLine($"✓ SurveyJson parameter ADDED to SP call");
                }
                else
                {
                    Console.WriteLine($"✗ SurveyJson parameter NOT supported by SP");
                }
                if (availableParams.Contains("Tags")) cmd.Parameters.AddWithValue("@Tags", (object?)model.Tags ?? DBNull.Value);
                if (availableParams.Contains("Comments")) cmd.Parameters.AddWithValue("@Comments", (object?)model.Comments ?? DBNull.Value);
                if (availableParams.Contains("GuestName")) cmd.Parameters.AddWithValue("@GuestName", (object?)model.GuestName ?? DBNull.Value);
                if (availableParams.Contains("Email")) cmd.Parameters.AddWithValue("@Email", (object?)model.Email ?? DBNull.Value);
                if (availableParams.Contains("Phone")) cmd.Parameters.AddWithValue("@Phone", (object?)model.Phone ?? DBNull.Value);
                if (availableParams.Contains("GuestBirthDate")) cmd.Parameters.AddWithValue("@GuestBirthDate", (object?)model.GuestBirthDate ?? DBNull.Value);
                if (availableParams.Contains("AnniversaryDate")) cmd.Parameters.AddWithValue("@AnniversaryDate", (object?)model.AnniversaryDate ?? DBNull.Value);

                Console.WriteLine($"Executing SP with {cmd.Parameters.Count} parameters");
                var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Console.WriteLine($"✓ Feedback saved successfully with ID: {newId}");
                TempData["FeedbackSuccess"] = "Thank you! Your feedback has been submitted.";
                return RedirectToAction("ThankYou", new { id = newId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error submitting feedback: {ex.Message}");
                return View("Form", model);
            }
        }

        // GET: /Feedback/ThankYou
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ThankYou(int id)
        {
            ViewBag.FeedbackId = id;
            return View();
        }

        // GET: /Feedback/Summary
        [HttpGet]
        public async Task<IActionResult> Summary(DateTime? from, DateTime? to)
        {
            var summary = new GuestFeedbackSummary();
            var latest = new List<GuestFeedback>();
            try
            {
                using var con = new SqlConnection(_connectionString);
                await con.OpenAsync();
                using var cmd = new SqlCommand("usp_GetGuestFeedbackSummary", con)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@FromDate", (object?)from?.Date ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", (object?)to?.Date ?? DBNull.Value);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // helper local function to safely read numeric averages (float/decimal)
                    decimal ReadNumeric(string col)
                    {
                        try
                        {
                            var val = reader[col];
                            if (val == DBNull.Value) return 0m;
                            return Convert.ToDecimal(val);
                        }
                        catch { return 0m; }
                    }

                    summary.TotalFeedback = reader.ColumnExists("TotalFeedback") && !reader.IsDBNull(reader.GetOrdinal("TotalFeedback")) ? reader.GetInt32(reader.GetOrdinal("TotalFeedback")) : 0;
                    summary.AvgOverall = ReadNumeric("AvgOverall");
                    summary.AvgFood = ReadNumeric("AvgFood");
                    summary.AvgService = ReadNumeric("AvgService");
                    summary.AvgCleanliness = ReadNumeric("AvgCleanliness");
                    summary.AvgStaff = ReadNumeric("AvgStaff");
                    // New averages (optional columns)
                    summary.AvgAmbience = ReadNumeric("AvgAmbience");
                    summary.AvgValue = ReadNumeric("AvgValue");
                    summary.AvgSpeed = ReadNumeric("AvgSpeed");
                }
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        latest.Add(new GuestFeedback
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            VisitDate = reader.GetDateTime(reader.GetOrdinal("VisitDate")),
                            OverallRating = (byte)reader.GetByte(reader.GetOrdinal("OverallRating")),
                            FoodRating = reader.IsDBNull(reader.GetOrdinal("FoodRating")) ? null : (byte?)reader.GetByte(reader.GetOrdinal("FoodRating")),
                            ServiceRating = reader.IsDBNull(reader.GetOrdinal("ServiceRating")) ? null : (byte?)reader.GetByte(reader.GetOrdinal("ServiceRating")),
                            CleanlinessRating = reader.IsDBNull(reader.GetOrdinal("CleanlinessRating")) ? null : (byte?)reader.GetByte(reader.GetOrdinal("CleanlinessRating")),
                            StaffRating = reader.IsDBNull(reader.GetOrdinal("StaffRating")) ? null : (byte?)reader.GetByte(reader.GetOrdinal("StaffRating")),
                            AmbienceRating = reader.ColumnExists("AmbienceRating") && !reader.IsDBNull(reader.GetOrdinal("AmbienceRating")) ? (byte?)reader.GetByte(reader.GetOrdinal("AmbienceRating")) : null,
                            ValueRating = reader.ColumnExists("ValueRating") && !reader.IsDBNull(reader.GetOrdinal("ValueRating")) ? (byte?)reader.GetByte(reader.GetOrdinal("ValueRating")) : null,
                            SpeedRating = reader.ColumnExists("SpeedRating") && !reader.IsDBNull(reader.GetOrdinal("SpeedRating")) ? (byte?)reader.GetByte(reader.GetOrdinal("SpeedRating")) : null,
                            Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? null : reader.GetString(reader.GetOrdinal("Tags")),
                            Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? null : reader.GetString(reader.GetOrdinal("Comments")),
                            GuestName = reader.IsDBNull(reader.GetOrdinal("GuestName")) ? null : reader.GetString(reader.GetOrdinal("GuestName")),
                            GuestBirthDate = reader.ColumnExists("GuestBirthDate") && !reader.IsDBNull(reader.GetOrdinal("GuestBirthDate")) ? (DateTime?)reader.GetDateTime(reader.GetOrdinal("GuestBirthDate")) : null,
                            AnniversaryDate = reader.ColumnExists("AnniversaryDate") && !reader.IsDBNull(reader.GetOrdinal("AnniversaryDate")) ? (DateTime?)reader.GetDateTime(reader.GetOrdinal("AnniversaryDate")) : null,
                            // Optional extras
                            Location = reader.ColumnExists("Location") && !reader.IsDBNull(reader.GetOrdinal("Location")) ? reader.GetString(reader.GetOrdinal("Location")) : null,
                            FirstVisit = reader.ColumnExists("IsFirstVisit") && !reader.IsDBNull(reader.GetOrdinal("IsFirstVisit")) ? (bool?)reader.GetBoolean(reader.GetOrdinal("IsFirstVisit")) : null,
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                        });
                    }
                }
                else
                {
                    // Fallback for older DBs where the SP returns only a single result set (aggregates)
                    using var cmdLatest = new SqlCommand("SELECT TOP 50 Id, VisitDate, OverallRating, FoodRating, ServiceRating, CleanlinessRating, StaffRating, Tags, Comments, GuestName, GuestBirthDate, AnniversaryDate, CreatedAt FROM GuestFeedback ORDER BY CreatedAt DESC", con);
                    using var r2 = await cmdLatest.ExecuteReaderAsync();
                    while (await r2.ReadAsync())
                    {
                        latest.Add(new GuestFeedback
                        {
                            Id = r2.GetInt32(r2.GetOrdinal("Id")),
                            VisitDate = r2.GetDateTime(r2.GetOrdinal("VisitDate")),
                            OverallRating = (byte)r2.GetByte(r2.GetOrdinal("OverallRating")),
                            FoodRating = r2.IsDBNull(r2.GetOrdinal("FoodRating")) ? null : (byte?)r2.GetByte(r2.GetOrdinal("FoodRating")),
                            ServiceRating = r2.IsDBNull(r2.GetOrdinal("ServiceRating")) ? null : (byte?)r2.GetByte(r2.GetOrdinal("ServiceRating")),
                            CleanlinessRating = r2.IsDBNull(r2.GetOrdinal("CleanlinessRating")) ? null : (byte?)r2.GetByte(r2.GetOrdinal("CleanlinessRating")),
                            StaffRating = r2.IsDBNull(r2.GetOrdinal("StaffRating")) ? null : (byte?)r2.GetByte(r2.GetOrdinal("StaffRating")),
                            Tags = r2.IsDBNull(r2.GetOrdinal("Tags")) ? null : r2.GetString(r2.GetOrdinal("Tags")),
                            Comments = r2.IsDBNull(r2.GetOrdinal("Comments")) ? null : r2.GetString(r2.GetOrdinal("Comments")),
                            GuestName = r2.IsDBNull(r2.GetOrdinal("GuestName")) ? null : r2.GetString(r2.GetOrdinal("GuestName")),
                            GuestBirthDate = r2.ColumnExists("GuestBirthDate") && !r2.IsDBNull(r2.GetOrdinal("GuestBirthDate")) ? (DateTime?)r2.GetDateTime(r2.GetOrdinal("GuestBirthDate")) : null,
                            AnniversaryDate = r2.ColumnExists("AnniversaryDate") && !r2.IsDBNull(r2.GetOrdinal("AnniversaryDate")) ? (DateTime?)r2.GetDateTime(r2.GetOrdinal("AnniversaryDate")) : null,
                            Location = r2.ColumnExists("Location") && !r2.IsDBNull(r2.GetOrdinal("Location")) ? r2.GetString(r2.GetOrdinal("Location")) : null,
                            FirstVisit = r2.ColumnExists("IsFirstVisit") && !r2.IsDBNull(r2.GetOrdinal("IsFirstVisit")) ? (bool?)r2.GetBoolean(r2.GetOrdinal("IsFirstVisit")) : null,
                            CreatedAt = r2.GetDateTime(r2.GetOrdinal("CreatedAt"))
                        });
                    }
                }
                // If aggregates indicate data but the latest list is empty (e.g., due to SP filter differences), load a best-effort latest list.
                if (latest.Count == 0 && summary.TotalFeedback > 0)
                {
                    using var cmdLatest2 = new SqlCommand("SELECT TOP 50 Id, VisitDate, OverallRating, FoodRating, ServiceRating, CleanlinessRating, StaffRating, AmbienceRating, ValueRating, SpeedRating, Tags, Comments, GuestName, GuestBirthDate, AnniversaryDate, Location, IsFirstVisit, CreatedAt FROM GuestFeedback ORDER BY CreatedAt DESC", con);
                    using var r3 = await cmdLatest2.ExecuteReaderAsync();
                    while (await r3.ReadAsync())
                    {
                        latest.Add(new GuestFeedback
                        {
                            Id = r3.GetInt32(r3.GetOrdinal("Id")),
                            VisitDate = r3.GetDateTime(r3.GetOrdinal("VisitDate")),
                            OverallRating = (byte)r3.GetByte(r3.GetOrdinal("OverallRating")),
                            FoodRating = r3.IsDBNull(r3.GetOrdinal("FoodRating")) ? null : (byte?)r3.GetByte(r3.GetOrdinal("FoodRating")),
                            ServiceRating = r3.IsDBNull(r3.GetOrdinal("ServiceRating")) ? null : (byte?)r3.GetByte(r3.GetOrdinal("ServiceRating")),
                            CleanlinessRating = r3.IsDBNull(r3.GetOrdinal("CleanlinessRating")) ? null : (byte?)r3.GetByte(r3.GetOrdinal("CleanlinessRating")),
                            StaffRating = r3.IsDBNull(r3.GetOrdinal("StaffRating")) ? null : (byte?)r3.GetByte(r3.GetOrdinal("StaffRating")),
                            AmbienceRating = r3.ColumnExists("AmbienceRating") && !r3.IsDBNull(r3.GetOrdinal("AmbienceRating")) ? (byte?)r3.GetByte(r3.GetOrdinal("AmbienceRating")) : null,
                            ValueRating = r3.ColumnExists("ValueRating") && !r3.IsDBNull(r3.GetOrdinal("ValueRating")) ? (byte?)r3.GetByte(r3.GetOrdinal("ValueRating")) : null,
                            SpeedRating = r3.ColumnExists("SpeedRating") && !r3.IsDBNull(r3.GetOrdinal("SpeedRating")) ? (byte?)r3.GetByte(r3.GetOrdinal("SpeedRating")) : null,
                            Tags = r3.IsDBNull(r3.GetOrdinal("Tags")) ? null : r3.GetString(r3.GetOrdinal("Tags")),
                            Comments = r3.IsDBNull(r3.GetOrdinal("Comments")) ? null : r3.GetString(r3.GetOrdinal("Comments")),
                            GuestName = r3.IsDBNull(r3.GetOrdinal("GuestName")) ? null : r3.GetString(r3.GetOrdinal("GuestName")),
                            GuestBirthDate = r3.ColumnExists("GuestBirthDate") && !r3.IsDBNull(r3.GetOrdinal("GuestBirthDate")) ? (DateTime?)r3.GetDateTime(r3.GetOrdinal("GuestBirthDate")) : null,
                            AnniversaryDate = r3.ColumnExists("AnniversaryDate") && !r3.IsDBNull(r3.GetOrdinal("AnniversaryDate")) ? (DateTime?)r3.GetDateTime(r3.GetOrdinal("AnniversaryDate")) : null,
                            Location = r3.ColumnExists("Location") && !r3.IsDBNull(r3.GetOrdinal("Location")) ? r3.GetString(r3.GetOrdinal("Location")) : null,
                            FirstVisit = r3.ColumnExists("IsFirstVisit") && !r3.IsDBNull(r3.GetOrdinal("IsFirstVisit")) ? (bool?)r3.GetBoolean(r3.GetOrdinal("IsFirstVisit")) : null,
                            CreatedAt = r3.GetDateTime(r3.GetOrdinal("CreatedAt"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["FeedbackError"] = $"Error loading feedback summary: {ex.Message}";
            }
            ViewBag.Summary = summary;
            return View(latest);
        }
    }
}