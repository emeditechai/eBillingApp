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
    public class ReservationController : Controller
    {
        private readonly IConfiguration _config;

        public ReservationController(IConfiguration configuration)
        {
            _config = configuration;
        }

        #region Reservations

        // GET: Reservations Dashboard
        public IActionResult Dashboard()
        {
            // Get today's date
            DateTime today = DateTime.Today;

            // Get reservations for today
            var todaysReservations = GetReservationsByDate(today);
            ViewBag.TodaysReservations = todaysReservations;

            // Get tomorrow's reservations
            var tomorrowsReservations = GetReservationsByDate(today.AddDays(1));
            ViewBag.TomorrowsReservations = tomorrowsReservations;

            // Get active waitlist
            var waitlist = GetActiveWaitlist();
            ViewBag.Waitlist = waitlist;

            // Get all tables with their status
            var tables = GetAllTables();
            ViewBag.Tables = tables;

            // Calculate statistics
            ViewBag.TotalTables = tables.Count;
            ViewBag.AvailableTables = tables.Count(t => t.Status == TableStatus.Available);
            ViewBag.OccupiedTables = tables.Count(t => t.Status == TableStatus.Occupied);
            ViewBag.ReservedTables = tables.Count(t => t.Status == TableStatus.Reserved);
            ViewBag.WaitlistCount = waitlist.Count;
            ViewBag.TodaysReservationCount = todaysReservations.Count;
            ViewBag.PendingArrivals = todaysReservations.Count(r => r.Status == ReservationStatus.Confirmed);
            ViewBag.SeatedGuests = todaysReservations.Count(r => r.Status == ReservationStatus.Seated);

            return View();
        }

        // GET: Reservation List
        public IActionResult List(DateTime? date = null)
        {
            try
            {
                // If no date is provided, use today's date
                date ??= DateTime.Today;

                // Get reservations for the selected date
                var reservations = GetReservationsByDate(date.Value);
                
                // Store the selected date in ViewBag for the view
                ViewBag.SelectedDate = date.Value;

                return View(reservations);
            }
            catch (Exception ex)
            {
                // Log the error
                
                
                // Add error message to TempData
                TempData["ErrorMessage"] = $"Error loading reservations: {ex.Message}";
                
                // Return an empty list to avoid null reference exceptions
                return View(new List<Reservation>());
            }
        }

        // GET: Create Reservation Form
        public IActionResult Create()
        {
            Reservation model = new Reservation 
            { 
                ReservationDate = DateTime.Today,
                ReservationTime = DateTime.Now.AddHours(1)
            };
            
            // Get available tables for the dropdown
            ViewBag.Tables = GetAvailableTables(model.ReservationDateTime, model.PartySize);
            
            return View("ReservationForm", model);
        }

        // GET: Edit Reservation Form
        public IActionResult Edit(int id)
        {
            var model = GetReservationById(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction("List");
            }

            // Get available tables for the dropdown, including the currently assigned table
            ViewBag.Tables = GetAvailableTables(model.ReservationDateTime, model.PartySize, model.TableId);

            return View("ReservationForm", model);
        }

        // POST: Save Reservation
        [HttpPostAttribute]
        public IActionResult SaveReservation(Reservation model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Tables = GetAvailableTables(model.ReservationDateTime, model.PartySize, model.TableId);
                return View("ReservationForm", model);
            }

            string resultMessage = "";
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                
                // Check if updating and Id exists
                if (model.Id > 0)
                {
                    using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM Reservations WHERE Id = @Id", con))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", model.Id);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            TempData["ErrorMessage"] = "Reservation update failed. Id not found.";
                            return RedirectToAction("List");
                        }
                    }
                }

                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("usp_UpsertReservation", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", model.Id == 0 ? 0 : model.Id);
                    cmd.Parameters.AddWithValue("@GuestName", model.GuestName);
                    cmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                    cmd.Parameters.AddWithValue("@EmailAddress", model.EmailAddress ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PartySize", model.PartySize);
                    cmd.Parameters.AddWithValue("@ReservationDate", model.ReservationDate.Date);
                    cmd.Parameters.AddWithValue("@ReservationTime", model.ReservationTime);
                    cmd.Parameters.AddWithValue("@SpecialRequests", model.SpecialRequests ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Notes", model.Notes ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TableId", model.TableId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", (int)model.Status);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            resultMessage = reader["Message"].ToString();
                        }
                    }
                }

                // If a table is assigned, update the table status
                if (model.TableId.HasValue && model.Status == ReservationStatus.Confirmed)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET Status = @Status WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)TableStatus.Reserved);
                        cmd.Parameters.AddWithValue("@Id", model.TableId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            TempData["ResultMessage"] = resultMessage;
            return RedirectToAction("List", new { date = model.ReservationDate.ToString("yyyy-MM-dd") });
        }

        // POST: Change Reservation Status
        [HttpPostAttribute]
        public IActionResult ChangeStatus(int id, ReservationStatus status)
        {
            var reservation = GetReservationById(id);
            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction("List");
            }

            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Reservations SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Status", (int)status);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }

                // Update the table status based on reservation status
                if (reservation.TableId.HasValue)
                {
                    TableStatus tableStatus = status switch
                    {
                        ReservationStatus.Seated => TableStatus.Occupied,
                        ReservationStatus.Completed => TableStatus.Dirty,
                        ReservationStatus.Cancelled => TableStatus.Available,
                        ReservationStatus.NoShow => TableStatus.Available,
                        _ => TableStatus.Reserved
                    };

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET Status = @Status WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)tableStatus);
                        cmd.Parameters.AddWithValue("@Id", reservation.TableId.Value);
                        cmd.ExecuteNonQuery();
                    }

                    // If the reservation is marked as seated, update the LastOccupiedAt timestamp
                    if (status == ReservationStatus.Seated)
                    {
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET LastOccupiedAt = @LastOccupiedAt WHERE Id = @Id", con))
                        {
                            cmd.Parameters.AddWithValue("@LastOccupiedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("@Id", reservation.TableId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // If the reservation is marked as NoShow, update the NoShow flag
                if (status == ReservationStatus.NoShow)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Reservations SET NoShow = 1 WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            TempData["ResultMessage"] = $"Reservation status updated to {status}";
            return RedirectToAction("List", new { date = reservation.ReservationDate.ToString("yyyy-MM-dd") });
        }

        // GET: Delete Reservation Confirmation
        public IActionResult DeleteConfirm(int id)
        {
            var model = GetReservationById(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction("List");
            }

            return View(model);
        }

        // POST: Delete Reservation
        [HttpPostAttribute]
        public IActionResult Delete(int id)
        {
            var reservation = GetReservationById(id);
            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction("List");
            }

            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                
                // Free up the table if the reservation had one assigned
                if (reservation.TableId.HasValue && reservation.Status == ReservationStatus.Confirmed)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET Status = @Status WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)TableStatus.Available);
                        cmd.Parameters.AddWithValue("@Id", reservation.TableId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
                
                // Delete the reservation
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("DELETE FROM Reservations WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["ResultMessage"] = "Reservation successfully deleted.";
            return RedirectToAction("List", new { date = reservation.ReservationDate.ToString("yyyy-MM-dd") });
        }

        #endregion

        #region Waitlist

        // GET: Waitlist Management
        public IActionResult Waitlist()
        {
            var waitlist = GetActiveWaitlist();
            
            // Check if an error occurred while loading waitlist data
            if (TempData.ContainsKey("ErrorMessage"))
            {
                ViewBag.ErrorMessage = TempData["ErrorMessage"];
                // Continue with empty waitlist if error occurred
                if (waitlist.Count == 0)
                {
                    waitlist = new List<WaitlistEntry>();
                }
            }
            
            try
            {
                // Get available tables for potentially seating waitlisted guests
                ViewBag.AvailableTables = GetAllTables().Where(t => t.Status == TableStatus.Available).ToList();
            }
            catch (Exception ex)
            {
                
                ViewBag.AvailableTables = new List<Table>();
                ViewBag.ErrorMessage = ViewBag.ErrorMessage ?? "Could not load available tables. Some functionality may be limited.";
            }
            
            return View(waitlist);
        }

        // GET: Add to Waitlist Form
        public IActionResult AddToWaitlist()
        {
            WaitlistEntry model = new WaitlistEntry();
            return View("WaitlistForm", model);
        }

        // POST: Save Waitlist Entry
        [HttpPostAttribute]
        public IActionResult SaveWaitlist(WaitlistEntry model)
        {
            if (!ModelState.IsValid)
            {
                return View("WaitlistForm", model);
            }

            string resultMessage = "";
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("usp_UpsertWaitlist", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", model.Id == 0 ? 0 : model.Id);
                    cmd.Parameters.AddWithValue("@GuestName", model.GuestName);
                    cmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                    cmd.Parameters.AddWithValue("@PartySize", model.PartySize);
                    cmd.Parameters.AddWithValue("@QuotedWaitTime", model.QuotedWaitTime);
                    cmd.Parameters.AddWithValue("@NotifyWhenReady", model.NotifyWhenReady);
                    cmd.Parameters.AddWithValue("@Notes", model.Notes ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", (int)model.Status);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            resultMessage = reader["Message"].ToString();
                        }
                    }
                }
            }

            TempData["ResultMessage"] = resultMessage;
            return RedirectToAction("Waitlist");
        }

        // POST: Update Waitlist Status
        [HttpPostAttribute]
        public IActionResult UpdateWaitlistStatus(int id, WaitlistStatus status)
        {
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Waitlist SET Status = @Status WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Status", (int)status);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }

                // If notifying the guest, update the NotifiedAt timestamp
                if (status == WaitlistStatus.Notified)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Waitlist SET NotifiedAt = @NotifiedAt WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@NotifiedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                // If seating the guest, update the SeatedAt timestamp
                if (status == WaitlistStatus.Seated)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Waitlist SET SeatedAt = @SeatedAt WHERE Id = @Id", con))
                    {
                        cmd.Parameters.AddWithValue("@SeatedAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            TempData["ResultMessage"] = $"Waitlist status updated to {status}";
            return RedirectToAction("Waitlist");
        }

        // POST: Assign Table to Waitlist Entry
        [HttpPostAttribute]
        public IActionResult AssignTableToWaitlist(int waitlistId, int tableId)
        {
            var waitlistEntry = GetWaitlistEntryById(waitlistId);
            var table = GetTableById(tableId);

            if (waitlistEntry == null || table == null)
            {
                TempData["ErrorMessage"] = "Waitlist entry or table not found.";
                return RedirectToAction("Waitlist");
            }

            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    try
                    {
                        // Update waitlist entry
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Waitlist SET Status = @Status, TableId = @TableId, SeatedAt = @SeatedAt WHERE Id = @Id", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Status", (int)WaitlistStatus.Seated);
                            cmd.Parameters.AddWithValue("@TableId", tableId);
                            cmd.Parameters.AddWithValue("@SeatedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("@Id", waitlistId);
                            cmd.ExecuteNonQuery();
                        }

                        // Update table status
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET Status = @Status, LastOccupiedAt = @LastOccupiedAt WHERE Id = @Id", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Status", (int)TableStatus.Occupied);
                            cmd.Parameters.AddWithValue("@LastOccupiedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("@Id", tableId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        TempData["ErrorMessage"] = "Error assigning table. Please try again.";
                        return RedirectToAction("Waitlist");
                    }
                }
            }

            TempData["ResultMessage"] = $"Table {table.TableNumber} assigned to {waitlistEntry.GuestName}";
            return RedirectToAction("Waitlist");
        }

        // POST: Remove from Waitlist
        [HttpPostAttribute]
        public IActionResult RemoveFromWaitlist(int id)
        {
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("DELETE FROM Waitlist WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["ResultMessage"] = "Entry removed from waitlist.";
            return RedirectToAction("Waitlist");
        }

        #endregion

        #region Tables Management

        // GET: Tables List
        public IActionResult Tables()
        {
            try
            {
                var tables = GetAllTables();

                // Rebuild merged table relationships directly from active orders to ensure symmetry (each table lists others in its merge group)
                try
                {
                    var tableLookup = tables.ToDictionary(t => t.TableNumber, t => t, StringComparer.OrdinalIgnoreCase);

                    // Query active order -> table memberships (only orders with >1 table are merges)
                    using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
                    {
                        con.Open();
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT o.Id AS OrderId, t.TableNumber
                                FROM Orders o
                                INNER JOIN OrderTables ot ON o.Id = ot.OrderId
                                INNER JOIN Tables t ON ot.TableId = t.Id
                                WHERE o.Status IN (0,1,2)
                                ORDER BY o.Id", con))
                        {
                            var orderTables = new Dictionary<int, List<string>>();
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var orderId = reader.GetInt32(0);
                                    var tblNum = reader.IsDBNull(1) ? null : reader.GetString(1);
                                    if (string.IsNullOrEmpty(tblNum)) continue;
                                    if (!orderTables.TryGetValue(orderId, out var list))
                                    {
                                        list = new List<string>();
                                        orderTables[orderId] = list;
                                    }
                                    if (!list.Contains(tblNum, StringComparer.OrdinalIgnoreCase))
                                        list.Add(tblNum);
                                }
                            }

                            foreach (var kvp in orderTables)
                            {
                                if (kvp.Value.Count <= 1) continue; // not a merge
                                var group = kvp.Value;
                                foreach (var tblNum in group)
                                {
                                    if (tableLookup.TryGetValue(tblNum, out var tbl))
                                    {
                                        tbl.IsPartOfMergedOrder = true;
                                        if (tbl.Status == TableStatus.Available)
                                            tbl.Status = TableStatus.Occupied; // show occupied when merged into active order

                                        var others = group.Where(n => !string.Equals(n, tbl.TableNumber, StringComparison.OrdinalIgnoreCase));
                                        var display = string.Join(" + ", others);
                                        tbl.DisplayMergedWith = string.IsNullOrWhiteSpace(display) ? null : display;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception mergeEx)
                {
                    Console.WriteLine($"Merged table relationship build failed: {mergeEx.Message}");
                }

                return View(tables.OrderBy(t => t.TableNumber));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error loading tables: {ex.Message}";
                return View(new List<Table>());
            }
        }

        // GET: Create/Edit Table Form
        public IActionResult TableForm(int? id)
        {
            Table model = new Table();

            if (id.HasValue)
            {
                model = GetTableById(id.Value);
                if (model == null)
                {
                    TempData["ErrorMessage"] = "Table not found.";
                    return RedirectToAction("Tables");
                }
            }

            return View(model);
        }

        // POST: Save Table
        [HttpPostAttribute]
        public IActionResult SaveTable(Table model)
        {
            if (!ModelState.IsValid)
            {
                return View("TableForm", model);
            }

            string resultMessage = "";
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                
                // Check if updating and Id exists
                if (model.Id > 0)
                {
                    using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM Tables WHERE Id = @Id", con))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", model.Id);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            TempData["ErrorMessage"] = "Table update failed. Id not found.";
                            return RedirectToAction("Tables");
                        }
                    }
                }

                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("usp_UpsertTable", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Id", model.Id == 0 ? 0 : model.Id);
                    cmd.Parameters.AddWithValue("@TableNumber", model.TableNumber);
                    cmd.Parameters.AddWithValue("@Capacity", model.Capacity);
                    cmd.Parameters.AddWithValue("@Section", model.Section ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", (int)model.Status);
                    cmd.Parameters.AddWithValue("@MinPartySize", model.MinPartySize);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            resultMessage = reader["Message"].ToString();
                        }
                    }
                }
            }

            TempData["ResultMessage"] = resultMessage;
            return RedirectToAction("Tables");
        }

        // POST: Update Table Status
        [HttpPostAttribute]
        public IActionResult UpdateTableStatus(int id, TableStatus status)
        {
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("UPDATE Tables SET Status = @Status WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Status", (int)status);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            TempData["ResultMessage"] = $"Table status updated to {status}";
            return RedirectToAction("Tables");
        }

        #endregion

        #region Helper Methods

        private List<Reservation> GetReservationsByDate(DateTime date)
        {
            var reservations = new List<Reservation>();
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                try
                {
                    con.Open();
                    
                    // First check if the table exists
                    using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(
                        @"IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Reservations')
                          SELECT 1 ELSE SELECT 0", con))
                    {
                        int tableExists = (int)checkCmd.ExecuteScalar();
                        if (tableExists == 0)
                        {
                            
                            return reservations; // Return empty list
                        }
                    }
                    
                    // Check if required columns exist
                    using (var checkColumnsCmd = new Microsoft.Data.SqlClient.SqlCommand(
                        @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                          WHERE TABLE_NAME = 'Reservations' 
                          AND COLUMN_NAME IN ('Id', 'GuestName', 'PhoneNumber', 'EmailAddress', 
                               'PartySize', 'ReservationDate', 'ReservationTime', 'Status')", con))
                    {
                        int columnCount = (int)checkColumnsCmd.ExecuteScalar();
                        if (columnCount < 8) // We need at least these 8 columns
                        {
                            
                            return reservations; // Return empty list
                        }
                    }
                    
                    // Revised query to get reservations with more resilient column access
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                        @"SELECT r.*, t.TableNumber
                        FROM Reservations r
                        LEFT JOIN Tables t ON r.TableId = t.Id
                        WHERE CONVERT(date, r.ReservationDate) = @Date
                        ORDER BY r.ReservationTime", con))
                    {
                        cmd.Parameters.AddWithValue("@Date", date.Date);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    Reservation reservation = new Reservation
                                    {
                                        Id = GetIntSafe(reader, "Id"),
                                        GuestName = GetStringSafe(reader, "GuestName"),
                                        PhoneNumber = GetStringSafe(reader, "PhoneNumber"),
                                        EmailAddress = GetNullableStringSafe(reader, "EmailAddress"),
                                        PartySize = GetIntSafe(reader, "PartySize"),
                                        ReservationDate = GetDateTimeSafe(reader, "ReservationDate"),
                                        ReservationTime = GetDateTimeSafe(reader, "ReservationTime"),
                                        SpecialRequests = GetNullableStringSafe(reader, "SpecialRequests"),
                                        Notes = GetNullableStringSafe(reader, "Notes"),
                                        Status = (ReservationStatus)GetIntSafe(reader, "Status")
                                    };
                                    
                                    // Store the table number (from joined Tables table)
                                    if (HasColumn(reader, "TableNumber"))
                                    {
                                        string tableNumber = GetNullableStringSafe(reader, "TableNumber");
                                        reservation.TableNumber = tableNumber;
                                    }

                                    // Optional columns - only add if they exist
                                    if (HasColumn(reader, "TableId"))
                                        reservation.TableId = GetNullableIntSafe(reader, "TableId");
                                        
                                    if (HasColumn(reader, "CreatedAt"))
                                        reservation.CreatedAt = GetDateTimeSafe(reader, "CreatedAt");
                                        
                                    if (HasColumn(reader, "UpdatedAt"))
                                        reservation.UpdatedAt = GetDateTimeSafe(reader, "UpdatedAt");
                                        
                                    if (HasColumn(reader, "ReminderSent"))
                                        reservation.ReminderSent = GetBooleanSafe(reader, "ReminderSent");
                                        
                                    if (HasColumn(reader, "NoShow"))
                                        reservation.NoShow = GetBooleanSafe(reader, "NoShow");
                                    
                                    reservations.Add(reservation);
                                }
                                catch (Exception ex)
                                {
                                    
                                    // Skip this row but continue processing others
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    
                    throw; // Rethrow to be caught in the List action
                }
            }
            return reservations;
        }
        
        // Safe helper methods for data access
        private bool HasColumn(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        
        private int GetIntSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }
        
        private int? GetNullableIntSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : (int?)reader.GetInt32(ordinal);
        }
        
        private string GetStringSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }
        
        private string? GetNullableStringSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        
        private DateTime GetDateTimeSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? DateTime.Now : reader.GetDateTime(ordinal);
        }
        
        private bool GetBooleanSafe(Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? false : reader.GetBoolean(ordinal);
        }

        private Reservation GetReservationById(int id)
        {
            Reservation reservation = null;
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"SELECT r.Id, r.GuestName, r.PhoneNumber, r.EmailAddress, r.PartySize, 
                    r.ReservationDate, r.ReservationTime, r.SpecialRequests, r.Notes, 
                    r.TableId, r.Status, r.CreatedAt, r.UpdatedAt, r.ReminderSent, r.NoShow,
                    t.TableNumber
                    FROM Reservations r
                    LEFT JOIN Tables t ON r.TableId = t.Id
                    WHERE r.Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reservation = new Reservation
                            {
                                Id = reader.GetInt32(0),
                                GuestName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                EmailAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                ReservationDate = reader.GetDateTime(5),
                                ReservationTime = reader.GetDateTime(6),
                                SpecialRequests = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
                                TableId = reader.IsDBNull(9) ? null : (int?)reader.GetInt32(9),
                                Status = (ReservationStatus)reader.GetInt32(10),
                                CreatedAt = reader.GetDateTime(11),
                                UpdatedAt = reader.GetDateTime(12),
                                ReminderSent = reader.GetBoolean(13),
                                NoShow = reader.GetBoolean(14)
                            };
                        }
                    }
                }
            }
            return reservation;
        }

        private List<WaitlistEntry> GetActiveWaitlist()
        {
            var waitlist = new List<WaitlistEntry>();
            try
            {
                using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    // Add timeout to connection open
                    var connectionTimeout = 60; // seconds
                    var connectionTask = Task.Run(() => con.Open());
                    if (!connectionTask.Wait(TimeSpan.FromSeconds(connectionTimeout)))
                    {
                        throw new TimeoutException($"Connection timeout after {connectionTimeout} seconds");
                    }
                    
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                        @"SELECT w.Id, w.GuestName, w.PhoneNumber, w.PartySize, w.AddedAt, 
                        w.QuotedWaitTime, w.NotifyWhenReady, w.Notes, w.Status, w.NotifiedAt, 
                        w.SeatedAt, w.TableId,
                        t.TableNumber
                        FROM Waitlist w
                        LEFT JOIN Tables t ON w.TableId = t.Id
                        WHERE w.Status IN (0, 1) -- Only Waiting and Notified statuses
                        ORDER BY w.AddedAt", con))
                    {
                        // Set command timeout to 30 seconds
                        cmd.CommandTimeout = 30;
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                waitlist.Add(new WaitlistEntry
                                {
                                    Id = reader.GetInt32(0),
                                    GuestName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    PhoneNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    PartySize = reader.GetInt32(3),
                                    AddedAt = reader.GetDateTime(4),
                                    QuotedWaitTime = reader.GetInt32(5),
                                    NotifyWhenReady = reader.GetBoolean(6),
                                    Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Status = (WaitlistStatus)reader.GetInt32(8),
                                    NotifiedAt = reader.IsDBNull(9) ? null : (DateTime?)reader.GetDateTime(9),
                                    SeatedAt = reader.IsDBNull(10) ? null : (DateTime?)reader.GetDateTime(10),
                                    TableId = reader.IsDBNull(11) ? null : (int?)reader.GetInt32(11)
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                // Log the SQL error
                
                TempData["ErrorMessage"] = "Unable to load waitlist data. Please try again later.";
            }
            catch (TimeoutException ex)
            {
                // Handle timeout specifically
                
                TempData["ErrorMessage"] = "Database connection timeout. Please try again later.";
            }
            catch (Exception ex)
            {
                // Log any other errors
                
                TempData["ErrorMessage"] = "An error occurred while loading the waitlist.";
            }
            
            return waitlist;
        }

        /// <summary>
        /// Helper method to execute database operations with proper timeout handling
        /// </summary>
        private T ExecuteWithTimeout<T>(Func<T> dbOperation, string operationName, T defaultValue = default)
        {
            try
            {
                // Execute the database operation with a timeout
                var task = Task.Run(dbOperation);
                if (!task.Wait(TimeSpan.FromSeconds(60)))
                {
                    throw new TimeoutException($"Operation timed out after 60 seconds: {operationName}");
                }
                return task.Result;
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Count == 1)
            {
                // Unwrap the first inner exception
                var innerEx = ex.InnerExceptions[0];
                if (innerEx is SqlException sqlEx)
                {
                    
                    TempData["ErrorMessage"] = $"Database error: {sqlEx.Message}";
                }
                else
                {
                    
                    TempData["ErrorMessage"] = $"Error: {innerEx.Message}";
                }
                return defaultValue;
            }
            catch (TimeoutException ex)
            {
                
                TempData["ErrorMessage"] = "The operation timed out. Please try again later.";
                return defaultValue;
            }
            catch (Exception ex)
            {
                
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return defaultValue;
            }
        }
        
        private WaitlistEntry GetWaitlistEntryById(int id)
        {
            WaitlistEntry entry = null;
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"SELECT w.Id, w.GuestName, w.PhoneNumber, w.PartySize, w.AddedAt, 
                    w.QuotedWaitTime, w.NotifyWhenReady, w.Notes, w.Status, w.NotifiedAt, 
                    w.SeatedAt, w.TableId,
                    t.TableNumber
                    FROM Waitlist w
                    LEFT JOIN Tables t ON w.TableId = t.Id
                    WHERE w.Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            entry = new WaitlistEntry
                            {
                                Id = reader.GetInt32(0),
                                GuestName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                PartySize = reader.GetInt32(3),
                                AddedAt = reader.GetDateTime(4),
                                QuotedWaitTime = reader.GetInt32(5),
                                NotifyWhenReady = reader.GetBoolean(6),
                                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Status = (WaitlistStatus)reader.GetInt32(8),
                                NotifiedAt = reader.IsDBNull(9) ? null : (DateTime?)reader.GetDateTime(9),
                                SeatedAt = reader.IsDBNull(10) ? null : (DateTime?)reader.GetDateTime(10),
                                TableId = reader.IsDBNull(11) ? null : (int?)reader.GetInt32(11)
                            };
                        }
                    }
                }
            }
            return entry;
        }

        private List<Table> GetAllTables()
        {
            var tables = new List<Table>();
            try
            {
                using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    con.Open();
                    
                    // Query with merged table support
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT 
                            t.Id, 
                            t.TableNumber, 
                            t.Capacity, 
                            t.Section, 
                            t.IsAvailable,
                            CASE 
                                WHEN ot.TableId IS NOT NULL THEN 2  -- Occupied if part of active order
                                ELSE t.Status 
                            END as Status,
                            t.MinPartySize, 
                            t.LastOccupiedAt, 
                            t.IsActive,
                            ISNULL(merged.MergedTableNames, '') as MergedTableNames,
                            CASE WHEN ot.TableId IS NOT NULL THEN 1 ELSE 0 END as IsPartOfMergedOrder
                        FROM Tables t
                        LEFT JOIN (
                            SELECT DISTINCT ot.TableId
                            FROM OrderTables ot
                            INNER JOIN Orders o ON ot.OrderId = o.Id AND o.Status IN (0, 1, 2)
                        ) ot ON t.Id = ot.TableId
                        LEFT JOIN (
                            SELECT 
                                ot2.TableId,
                                STRING_AGG(CAST(t2.TableNumber AS VARCHAR(50)), ' + ') WITHIN GROUP (ORDER BY t2.TableNumber) AS MergedTableNames
                            FROM OrderTables ot2
                            INNER JOIN Tables t2 ON ot2.TableId = t2.Id
                            INNER JOIN Orders o2 ON ot2.OrderId = o2.Id AND o2.Status IN (0, 1, 2)
                            GROUP BY ot2.TableId
                        ) merged ON t.Id = merged.TableId
                        WHERE t.IsActive = 1
                        ORDER BY t.TableNumber", con))
                    {
                        cmd.CommandTimeout = 30;
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                tables.Add(new Table
                                {
                                    Id = reader.GetInt32("Id"),
                                    TableNumber = reader.IsDBNull("TableNumber") ? string.Empty : reader.GetString("TableNumber"),
                                    Capacity = reader.GetInt32("Capacity"),
                                    Section = reader.IsDBNull("Section") ? null : reader.GetString("Section"),
                                    IsAvailable = reader.GetBoolean("IsAvailable"),
                                    Status = (TableStatus)reader.GetInt32("Status"),
                                    MinPartySize = reader.GetInt32("MinPartySize"),
                                    LastOccupiedAt = reader.IsDBNull("LastOccupiedAt") ? null : (DateTime?)reader.GetDateTime("LastOccupiedAt"),
                                    IsActive = reader.GetBoolean("IsActive"),
                                    MergedTableNames = reader.IsDBNull("MergedTableNames") ? null : reader.GetString("MergedTableNames"),
                                    IsPartOfMergedOrder = reader.GetInt32("IsPartOfMergedOrder") == 1
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any other errors
                
                // We'll handle the error at the action level
            }
            
            return tables;
        }

        private Table GetTableById(int id)
        {
            Table table = null;
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"SELECT Id, TableNumber, Capacity, Section, IsAvailable, Status, 
                    MinPartySize, LastOccupiedAt, IsActive 
                    FROM Tables 
                    WHERE Id = @Id", con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            table = new Table
                            {
                                Id = reader.GetInt32(0),
                                TableNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Capacity = reader.GetInt32(2),
                                Section = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsAvailable = reader.GetBoolean(4),
                                Status = (TableStatus)reader.GetInt32(5),
                                MinPartySize = reader.GetInt32(6),
                                LastOccupiedAt = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                                IsActive = reader.GetBoolean(8)
                            };
                        }
                    }
                }
            }
            return table;
        }

        private List<Table> GetAvailableTables(DateTime reservationDateTime, int partySize, int? currentTableId = null)
        {
            var tables = new List<Table>();
            using (var con = new Microsoft.Data.SqlClient.SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                con.Open();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    @"SELECT t.Id, t.TableNumber, t.Capacity, t.Section, t.IsAvailable, t.Status, 
                    t.MinPartySize, t.LastOccupiedAt, t.IsActive 
                    FROM Tables t
                    WHERE t.IsActive = 1
                    AND t.Capacity >= @PartySize
                    AND (t.Status = 0 -- Available
                         OR t.Id = @CurrentTableId)
                    AND NOT EXISTS (
                        SELECT 1 FROM Reservations r
                        WHERE r.TableId = t.Id
                        AND CONVERT(date, r.ReservationDate) = CONVERT(date, @ReservationDate)
                        AND r.Status IN (1, 2) -- Confirmed or Seated
                        AND r.Id <> ISNULL(@CurrentReservationId, 0) -- Exclude current reservation
                        AND DATEADD(HOUR, -1, @ReservationTime) < r.ReservationTime
                        AND DATEADD(HOUR, 1, @ReservationTime) > r.ReservationTime
                    )
                    ORDER BY ABS(t.Capacity - @PartySize), t.TableNumber", con))
                {
                    cmd.Parameters.AddWithValue("@PartySize", partySize);
                    cmd.Parameters.AddWithValue("@ReservationDate", reservationDateTime.Date);
                    cmd.Parameters.AddWithValue("@ReservationTime", reservationDateTime);
                    cmd.Parameters.AddWithValue("@CurrentTableId", currentTableId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CurrentReservationId", currentTableId != null ? (object)currentTableId : DBNull.Value);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tables.Add(new Table
                            {
                                Id = reader.GetInt32(0),
                                TableNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Capacity = reader.GetInt32(2),
                                Section = reader.IsDBNull(3) ? null : reader.GetString(3),
                                IsAvailable = reader.GetBoolean(4),
                                Status = (TableStatus)reader.GetInt32(5),
                                MinPartySize = reader.GetInt32(6),
                                LastOccupiedAt = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                                IsActive = reader.GetBoolean(8)
                            });
                        }
                    }
                }
            }
            
            // Apply smart recommendation scoring
            return ApplySmartTableScoring(tables, partySize, reservationDateTime);
        }
        
        /// <summary>
        /// Smart Table Recommendation Algorithm
        /// Scores tables based on multiple factors to recommend the best match
        /// </summary>
        private List<Table> ApplySmartTableScoring(List<Table> tables, int partySize, DateTime reservationDateTime)
        {
            var scoredTables = new List<(Table table, double score, string reason)>();
            
            foreach (var table in tables)
            {
                double score = 0;
                var reasons = new List<string>();
                
                // 1. CAPACITY MATCH (40 points) - Most important factor
                int capacityDiff = table.Capacity - partySize;
                if (capacityDiff == 0)
                {
                    score += 40;
                    reasons.Add("Perfect fit");
                }
                else if (capacityDiff == 1)
                {
                    score += 35;
                    reasons.Add("Excellent match");
                }
                else if (capacityDiff == 2)
                {
                    score += 30;
                    reasons.Add("Good match");
                }
                else if (capacityDiff <= 4)
                {
                    score += 20;
                    reasons.Add("Suitable");
                }
                else
                {
                    score += 10;
                    if (capacityDiff > 6)
                        reasons.Add("Oversized");
                }
                
                // 2. TIME SLOT OPTIMIZATION (25 points)
                int hour = reservationDateTime.Hour;
                bool isPeakHour = (hour >= 12 && hour <= 14) || (hour >= 18 && hour <= 21);
                
                if (isPeakHour)
                {
                    if (capacityDiff <= 1)
                    {
                        score += 25;
                        reasons.Add("Peak hour optimal");
                    }
                    else if (capacityDiff <= 3)
                    {
                        score += 15;
                    }
                    else
                    {
                        score += 5;
                    }
                }
                else
                {
                    score += 20;
                    if (capacityDiff > 4)
                        reasons.Add("Off-peak flexible");
                }
                
                // 3. SECTION PREFERENCE (15 points)
                if (!string.IsNullOrEmpty(table.Section))
                {
                    if ((table.Section.Contains("Window", StringComparison.OrdinalIgnoreCase) || 
                         table.Section.Contains("Patio", StringComparison.OrdinalIgnoreCase)) &&
                        partySize == 2 && hour >= 18)
                    {
                        score += 15;
                        reasons.Add("Romantic setting");
                    }
                    else if ((table.Section.Contains("Private", StringComparison.OrdinalIgnoreCase) ||
                             table.Section.Contains("Back", StringComparison.OrdinalIgnoreCase)) &&
                            partySize >= 6)
                    {
                        score += 15;
                        reasons.Add("Group-friendly");
                    }
                    else
                    {
                        score += 10;
                    }
                }
                else
                {
                    score += 5;
                }
                
                // 4. AVAILABILITY & FRESHNESS (10 points)
                if (table.Status == TableStatus.Available)
                {
                    score += 10;
                    
                    if (table.LastOccupiedAt.HasValue)
                    {
                        var minutesSinceOccupied = (DateTime.Now - table.LastOccupiedAt.Value).TotalMinutes;
                        if (minutesSinceOccupied > 30)
                        {
                            score += 5;
                            reasons.Add("Ready to use");
                        }
                    }
                }
                
                // 5. LOAD BALANCING (10 points)
                score += 5;
                
                scoredTables.Add((table, score, string.Join(", ", reasons)));
            }
            
            // Sort by score descending
            return scoredTables
                .OrderByDescending(x => x.score)
                .Select(x => x.table)
                .ToList();
        }

        #endregion
    }
}
