using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    public class TableServiceController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        
        public TableServiceController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }
        
        // Dashboard for Table Service
        public IActionResult Dashboard()
        {
            var model = GetTableServiceDashboardViewModel();
            return View(model);
        }
        
        // View for Seating a Guest (from reservation or waitlist)
        public IActionResult SeatGuest(int? reservationId = null, int? waitlistId = null)
        {
            var model = new SeatGuestViewModel
            {
                ReservationId = reservationId,
                WaitlistId = waitlistId,
                AvailableTables = GetAvailableTables(),
                Servers = GetAvailableServers()
            };
            
            // If coming from reservation, pre-populate data
            if (reservationId.HasValue)
            {
                var reservation = GetReservationById(reservationId.Value);
                if (reservation != null)
                {
                    model.GuestName = reservation.CustomerName;
                    model.PartySize = reservation.PartySize;
                    model.Notes = reservation.SpecialRequests;
                }
            }
            
            // If coming from waitlist, pre-populate data
            if (waitlistId.HasValue)
            {
                var waitlist = GetWaitlistEntryById(waitlistId.Value);
                if (waitlist != null)
                {
                    model.GuestName = waitlist.CustomerName;
                    model.PartySize = waitlist.PartySize;
                    model.Notes = waitlist.SpecialRequests;
                }
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult SeatGuest(SeatGuestViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // First, start table turnover
                    var turnoverResult = StartTableTurnover(
                        model.TableId,
                        model.ReservationId,
                        model.WaitlistId,
                        model.GuestName,
                        model.PartySize,
                        model.Notes,
                        model.TargetTurnTime
                    );
                    
                    // If turnover started successfully, assign cashier
                    if (turnoverResult.Success)
                    {
                        var assignResult = AssignServerToTable(model.TableId, model.ServerId, GetCurrentUserId());
                        
                        if (assignResult.Success)
                        {
                            TempData["SuccessMessage"] = "Guest seated and cashier assigned successfully.";
                            return RedirectToAction(nameof(Dashboard));
                        }
                        else
                        {
                            ModelState.AddModelError("", $"Error assigning cashier: {assignResult.Message}");
                        }
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Error seating guest: {turnoverResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            // If we reach here, there was an error - repopulate dropdowns
            model.AvailableTables = GetAvailableTables();
            model.Servers = GetAvailableServers();
            return View(model);
        }
        
        // View Active Tables
        public IActionResult ActiveTables()
        {
            var model = GetActiveTables();
            return View(model);
        }
        
        // Update Table Status
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult UpdateTableStatus(int turnoverId, int newStatus)
        {
            try
            {
                var result = UpdateOrderStatusForAllMergedTables(turnoverId, newStatus);
                
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = $"Error updating status: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
            }
            
            return RedirectToAction(nameof(ActiveTables));
        }
        
        // Clean up stale table turnovers and reset table statuses
        public IActionResult CleanupStaleTurnovers()
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    int cleanedTurnovers = 0;
                    int resetTables = 0;
                    
                    // First, mark old turnovers as departed if they don't have active orders
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE TableTurnovers 
                        SET Status = 5, -- Departed
                            DepartedAt = GETDATE()
                        WHERE Status < 5 -- Not already departed
                        AND DATEDIFF(MINUTE, SeatedAt, GETDATE()) > 480 -- More than 8 hours old
                        AND NOT EXISTS (
                            SELECT 1 FROM Orders o 
                            WHERE o.TableTurnoverId = TableTurnovers.Id 
                            AND o.Status < 3 -- Active orders
                        )", connection))
                    {
                        cleanedTurnovers = command.ExecuteNonQuery();
                    }
                    
                    // Reset table status to Available for tables without active turnovers
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Tables 
                        SET Status = 0, -- Available
                            LastOccupiedAt = GETDATE(),
                            IsAvailable = 1
                        WHERE Id NOT IN (
                            SELECT DISTINCT tt.TableId 
                            FROM TableTurnovers tt 
                            WHERE tt.Status < 5 -- Active turnovers
                        )
                        AND Status != 0", connection))
                    {
                        resetTables = command.ExecuteNonQuery();
                    }
                    
                    TempData["SuccessMessage"] = $"Cleanup completed: {cleanedTurnovers} stale turnovers departed, {resetTables} tables reset to available.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error during cleanup: {ex.Message}";
            }
            
            return RedirectToAction(nameof(Dashboard));
        }
        
        // Force reset all tables to available status (aggressive cleanup)
        public IActionResult ForceResetAllTables()
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    int departedTurnovers = 0;
                    int resetTables = 0;
                    
                    // Mark ALL active turnovers as departed
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE TableTurnovers 
                        SET Status = 5, -- Departed
                            DepartedAt = GETDATE()
                        WHERE Status < 5", connection))
                    {
                        departedTurnovers = command.ExecuteNonQuery();
                    }
                    
                    // Reset ALL tables to Available status
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Tables 
                        SET Status = 0, -- Available
                            LastOccupiedAt = GETDATE(),
                            IsAvailable = 1
                        WHERE IsActive = 1", connection))
                    {
                        resetTables = command.ExecuteNonQuery();
                    }
                    
                    // Clear any cashier assignments
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE ServerAssignments 
                        SET IsActive = 0,
                            LastModifiedAt = GETDATE()
                        WHERE IsActive = 1", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    
                    TempData["SuccessMessage"] = $"Force reset completed: {departedTurnovers} turnovers departed, {resetTables} tables reset to available. All cashier assignments cleared.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error during force reset: {ex.Message}";
            }
            
            return RedirectToAction(nameof(Dashboard));
        }
        
        // Helper Methods
        private TableServiceDashboardViewModel GetTableServiceDashboardViewModel()
        {
            var model = new TableServiceDashboardViewModel
            {
                AvailableTables = 0,
                OccupiedTables = 0,
                DirtyTables = 0,
                ReservationCount = 0,
                WaitlistCount = 0,
                CurrentTurnovers = new List<ActiveTableViewModel>(),
                UnoccupiedTables = new List<TableViewModel>()
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get table counts including merged table logic
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT
                        SUM(CASE WHEN (ot.TableId IS NULL AND t.Status = 0) THEN 1 ELSE 0 END) AS AvailableCount,
                        SUM(CASE WHEN (ot.TableId IS NOT NULL OR t.Status = 2) THEN 1 ELSE 0 END) AS OccupiedCount,
                        SUM(CASE WHEN (ot.TableId IS NULL AND t.Status = 3) THEN 1 ELSE 0 END) AS DirtyCount
                    FROM Tables t
                    LEFT JOIN (
                        SELECT DISTINCT ot.TableId
                        FROM OrderTables ot
                        INNER JOIN Orders o ON ot.OrderId = o.Id AND o.Status IN (0, 1, 2)
                    ) ot ON t.Id = ot.TableId
                    WHERE t.IsActive = 1", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.AvailableTables = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            model.OccupiedTables = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            model.DirtyTables = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        }
                    }
                }
                
                // Get today's pending reservation count
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Reservations
                    WHERE CAST(ReservationDate AS DATE) = CAST(GETDATE() AS DATE)
                    AND Status IN (0, 1, 6)", connection)) // Pending, Confirmed, Waitlisted
                {
                    model.ReservationCount = (int)command.ExecuteScalar();
                }
                
                // Get active waitlist count
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Waitlist
                    WHERE Status = 0", connection)) // 0 = Waiting
                {
                    model.WaitlistCount = (int)command.ExecuteScalar();
                }
                
                // Get current active turnovers
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT TOP 5
                        t.Id,
                        tb.Id AS TableId,
                        tb.TableNumber,
                        t.GuestName,
                        t.PartySize,
                        t.SeatedAt,
                        t.Status,
                        (u.FirstName + ' ' + ISNULL(u.LastName,'')) AS ServerName,
                        DATEDIFF(MINUTE, t.SeatedAt, GETDATE()) AS MinutesSinceSeated
                    FROM TableTurnovers t
                    INNER JOIN Tables tb ON t.TableId = tb.Id
                    LEFT JOIN ServerAssignments sa ON tb.Id = sa.TableId AND sa.IsActive = 1
                    LEFT JOIN Users u ON sa.ServerId = u.Id
                    WHERE t.Status < 5 -- Not departed
                    ORDER BY t.SeatedAt DESC", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.CurrentTurnovers.Add(new ActiveTableViewModel
                            {
                                TurnoverId = reader.GetInt32(0),
                                TableId = reader.GetInt32(1),
                                TableName = reader.GetString(2),
                                GuestName = reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                SeatedAt = reader.GetDateTime(5),
                                Status = reader.GetInt32(6),
                                ServerName = reader.IsDBNull(7) ? "Unassigned" : reader.GetString(7),
                                Duration = reader.GetInt32(8)
                            });
                        }
                    }
                }
                
                // Get ALL tables with their current status (available, reserved, occupied, dirty)
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT t.Id, t.TableName, t.Capacity, t.Status,
                        CASE 
                            WHEN ot.TableId IS NOT NULL OR t.Status = 2 THEN 2 -- Occupied
                            WHEN t.Status = 3 THEN 3 -- Dirty
                            WHEN t.Status = 1 THEN 1 -- Reserved (can take orders)
                            WHEN t.Status = 0 THEN 0 -- Available
                            ELSE t.Status
                        END AS CurrentStatus,
                        CASE 
                            WHEN ot.TableId IS NOT NULL OR t.Status = 2 THEN 'Occupied'
                            WHEN t.Status = 3 THEN 'Dirty'
                            WHEN t.Status = 1 THEN 'Reserved'
                            WHEN t.Status = 0 THEN 'Available'
                            ELSE 'Unknown'
                        END AS StatusDisplay,
                        ISNULL(t.Section, 'Main') AS Section,
                        nr.ReservationId,
                        nr.ReservationAt
                    FROM Tables t
                    LEFT JOIN (
                        SELECT DISTINCT ot.TableId
                        FROM OrderTables ot
                        INNER JOIN Orders o ON ot.OrderId = o.Id AND o.Status IN (0, 1, 2)
                    ) ot ON t.Id = ot.TableId
                    OUTER APPLY (
                        SELECT TOP 1
                            r.Id AS ReservationId,
                                                        DATEADD(SECOND,
                                                                DATEDIFF(SECOND, 0, CAST(r.ReservationTime AS time)),
                                                                CAST(r.ReservationDate AS datetime)
                                                        ) AS ReservationAt
                        FROM Reservations r
                        WHERE r.TableId = t.Id
                          AND r.Status IN (0, 1, 6) -- Pending/Confirmed/Waitlisted
                                                    AND DATEADD(SECOND,
                                                                DATEDIFF(SECOND, 0, CAST(r.ReservationTime AS time)),
                                                                CAST(r.ReservationDate AS datetime)
                                                            ) >= DATEADD(DAY, DATEDIFF(DAY, 0, GETDATE()), 0)
                                                ORDER BY DATEADD(SECOND,
                                                                DATEDIFF(SECOND, 0, CAST(r.ReservationTime AS time)),
                                                                CAST(r.ReservationDate AS datetime)
                                                            ) ASC
                    ) nr
                    WHERE t.IsActive = 1
                    ORDER BY t.Section, t.TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.UnoccupiedTables.Add(new TableViewModel
                            {
                                Id = reader.GetInt32(0),
                                TableName = reader.GetString(1),
                                Capacity = reader.GetInt32(2),
                                Status = reader.GetInt32(4), // CurrentStatus
                                StatusDisplay = reader.GetString(5),
                                Section = reader.GetString(6),
                                NextReservationId = reader.IsDBNull(7) ? null : (int?)reader.GetInt32(7),
                                NextReservationAt = reader.IsDBNull(8) ? null : (DateTime?)reader.GetDateTime(8)
                            });
                        }
                    }
                }
                
                // ===== REAL-TIME ANALYTICS & PERFORMANCE METRICS =====
                
                // 1. Average Table Turnover Time (completed turnovers today)
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT AVG(DATEDIFF(MINUTE, SeatedAt, DepartedAt))
                    FROM TableTurnovers
                    WHERE Status = 5 -- Departed
                    AND CAST(SeatedAt AS DATE) = CAST(GETDATE() AS DATE)
                    AND DepartedAt IS NOT NULL", connection))
                {
                    var result = command.ExecuteScalar();
                    model.AverageTurnoverTime = result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
                
                // 2. Today's Revenue and Revenue per Table
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        ISNULL(SUM(o.TotalAmount), 0) AS TodayRevenue,
                        COUNT(DISTINCT CASE WHEN tt.TableId IS NOT NULL THEN tt.TableId END) AS TablesUsed
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                    AND o.Status != 4", connection)) // Exclude cancelled orders
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TodayRevenue = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                            var tablesUsed = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            model.AverageRevenuePerTable = tablesUsed > 0 ? model.TodayRevenue / tablesUsed : 0;
                        }
                    }
                }
                
                // 3. Peak Hours Data (hourly order count and revenue for today)
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        DATEPART(HOUR, o.CreatedAt) AS Hour,
                        COUNT(*) AS OrderCount,
                        ISNULL(SUM(o.TotalAmount), 0) AS Revenue
                    FROM Orders o
                    WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                    AND o.Status != 4 -- Exclude cancelled
                    GROUP BY DATEPART(HOUR, o.CreatedAt)
                    ORDER BY OrderCount DESC", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        var peakHoursList = new List<PeakHourData>();
                        while (reader.Read())
                        {
                            peakHoursList.Add(new PeakHourData
                            {
                                Hour = reader.GetInt32(0),
                                OrderCount = reader.GetInt32(1),
                                Revenue = reader.GetDecimal(2)
                            });
                        }
                        
                        // Mark top 3 hours as peak hours
                        for (int i = 0; i < Math.Min(3, peakHoursList.Count); i++)
                        {
                            peakHoursList[i].IsPeakHour = true;
                        }
                        
                        model.PeakHours = peakHoursList.OrderBy(p => p.Hour).ToList();
                    }
                }
                
                // 4. Server Performance Stats (active tables, today's orders, today's revenue)
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        u.Id AS ServerId,
                        (u.FirstName + ' ' + ISNULL(u.LastName, '')) AS ServerName,
                        COUNT(DISTINCT sa.TableId) AS ActiveTables,
                        COUNT(DISTINCT o.Id) AS TotalOrders,
                        ISNULL(SUM(o.TotalAmount), 0) AS TotalRevenue
                    FROM Users u
                    LEFT JOIN ServerAssignments sa ON u.Id = sa.ServerId AND sa.IsActive = 1
                    LEFT JOIN TableTurnovers tt ON sa.TableId = tt.TableId AND tt.Status < 5
                    LEFT JOIN Orders o ON tt.Id = o.TableTurnoverId 
                        AND CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                        AND o.Status != 4
                    WHERE u.IsActive = 1
                    AND EXISTS (
                        SELECT 1 FROM UserRoles ur
                        INNER JOIN Roles r ON ur.RoleId = r.Id
                        WHERE ur.UserId = u.Id AND r.Name IN ('Server', 'Waiter', 'Staff')
                    )
                    GROUP BY u.Id, u.FirstName, u.LastName
                    HAVING COUNT(DISTINCT sa.TableId) > 0 OR COUNT(DISTINCT o.Id) > 0
                    ORDER BY ActiveTables DESC, TotalRevenue DESC", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.ServerPerformance.Add(new ServerPerformanceData
                            {
                                ServerId = reader.GetInt32(0),
                                ServerName = reader.GetString(1),
                                ActiveTables = reader.GetInt32(2),
                                TotalOrders = reader.GetInt32(3),
                                TotalRevenue = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }
            
            return model;
        }
        
        private List<SelectListItem> GetAvailableTables()
        {
            var tables = new List<SelectListItem>();
            string diagnostics = null;

            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Return currently available tables OR today's reserved tables (Pending/Confirmed)
                    string sql = @"
                        SELECT DISTINCT t.Id, t.TableName, t.Capacity, t.Status
                        FROM Tables t
                        LEFT JOIN OrderTables ot ON t.Id = ot.TableId
                        LEFT JOIN Orders o ON ot.OrderId = o.Id AND o.Status IN (0, 1, 2)
                        WHERE t.Status = 0 AND ot.TableId IS NULL -- Available and not assigned to active order

                        UNION

                        SELECT DISTINCT t2.Id, t2.TableName, t2.Capacity, t2.Status
                        FROM Tables t2
                        INNER JOIN Reservations r ON r.TableId = t2.Id
                        WHERE CAST(r.ReservationTime AS DATE) = CAST(GETDATE() AS DATE)
                          AND r.Status IN (0, 1) -- Pending or Confirmed
                          AND t2.IsActive = 1

                        ORDER BY 2";

                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                    {
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            int count = 0;
                            while (reader.Read())
                            {
                                tables.Add(new SelectListItem
                                {
                                    Value = reader.GetInt32(0).ToString(),
                                    Text = $"{reader.GetString(1)} (Seats {reader.GetInt32(2)})"
                                });
                                count++;
                            }
                            diagnostics = $"GetAvailableTables returned {count} rows";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics = (diagnostics == null ? "" : diagnostics + ";") + "Error: " + ex.Message;
            }

            if (!tables.Any())
            {
                tables.Add(new SelectListItem { Value = "", Text = "-- No tables available --" });
            }

            if (!string.IsNullOrEmpty(diagnostics))
            {
                try { TempData["TableLoadDiagnostics"] = diagnostics; } catch { }
            }

            return tables;
        }
        
    private List<SelectListItem> GetAvailableServers()
    {
        var servers = new List<SelectListItem>();
        string diagnostics = null;

        try
        {
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                int serverRoleId = ResolveServerRoleId(connection);
                string selectName = "COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, ' ', ISNULL(u.LastName,'')))), ''), u.Username, u.Email, 'User ' + CAST(u.Id AS NVARCHAR(10))) AS FullName";

                // 1) Try Users.Role
                try
                {
                    if (ColumnExists(connection, "Users", "Role"))
                    {
                        string q = $@"SELECT Id, {selectName} FROM Users WHERE IsActive = 1 AND Role = @r ORDER BY 2";
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(q, connection))
                        {
                            cmd.Parameters.AddWithValue("@r", serverRoleId);
                            using (var rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read()) servers.Add(new SelectListItem { Value = rdr.GetInt32(0).ToString(), Text = rdr.GetString(1) });
                            }
                        }
                        if (servers.Any()) diagnostics = "Used Users.Role";
                    }
                }
                catch (Exception e1) { diagnostics = (diagnostics ?? "") + ";Users.Role failed: " + e1.Message; }

                // 2) Try Users.RoleId
                if (!servers.Any())
                {
                    try
                    {
                        if (ColumnExists(connection, "Users", "RoleId"))
                        {
                            string q = $@"SELECT Id, {selectName} FROM Users WHERE IsActive = 1 AND RoleId = @r ORDER BY 2";
                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(q, connection))
                            {
                                cmd.Parameters.AddWithValue("@r", serverRoleId);
                                using (var rdr = cmd.ExecuteReader())
                                {
                                    while (rdr.Read()) servers.Add(new SelectListItem { Value = rdr.GetInt32(0).ToString(), Text = rdr.GetString(1) });
                                }
                            }
                            if (servers.Any()) diagnostics = (diagnostics ?? "") + ";Used Users.RoleId";
                        }
                    }
                    catch (Exception e2) { diagnostics = (diagnostics ?? "") + ";Users.RoleId failed: " + e2.Message; }
                }

                // 3) Try UserRoles + Roles (detect Roles id column)
                if (!servers.Any())
                {
                    try
                    {
                        if (TableExists(connection, "UserRoles") && TableExists(connection, "Roles"))
                        {
                            string rolesIdCol = ColumnExists(connection, "Roles", "RoleId") ? "RoleId" : "Id";
                            string q = $@"SELECT DISTINCT u.Id, {selectName} FROM Users u INNER JOIN UserRoles ur ON u.Id = ur.UserId INNER JOIN Roles r ON ur.RoleId = r.{rolesIdCol} WHERE u.IsActive = 1 AND r.{rolesIdCol} = @r ORDER BY 2";
                            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(q, connection))
                            {
                                cmd.Parameters.AddWithValue("@r", serverRoleId);
                                using (var rdr = cmd.ExecuteReader())
                                {
                                    while (rdr.Read()) servers.Add(new SelectListItem { Value = rdr.GetInt32(0).ToString(), Text = rdr.GetString(1) });
                                }
                            }
                            if (servers.Any()) diagnostics = (diagnostics ?? "") + ";Used UserRoles+Roles";
                        }
                    }
                    catch (Exception e3) { diagnostics = (diagnostics ?? "") + ";UserRoles+Roles failed: " + e3.Message; }
                }

                // 4) Simple users list fallback
                if (!servers.Any())
                {
                    try
                    {
                        string q = $@"SELECT Id, {selectName} FROM Users WHERE IsActive = 1 ORDER BY 2";
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(q, connection))
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read()) servers.Add(new SelectListItem { Value = rdr.GetInt32(0).ToString(), Text = rdr.GetString(1) });
                        }
                        if (servers.Any()) diagnostics = (diagnostics ?? "") + ";Used simple Users list";
                    }
                    catch (Exception e4) { diagnostics = (diagnostics ?? "") + ";Simple users query failed: " + e4.Message; }
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics = (diagnostics ?? "") + ";Outer exception: " + ex.Message;
        }

        if (!servers.Any())
        {
            servers.Add(new SelectListItem { Value = "", Text = "-- No servers available --" });
        }

        if (!string.IsNullOrEmpty(diagnostics))
        {
            try { TempData["ServerLoadDiagnostics"] = diagnostics; } catch { }
        }

        return servers;
    }

    // Determine an appropriate Server role id, if the Roles table exists; defaults to 2
    private int ResolveServerRoleId(Microsoft.Data.SqlClient.SqlConnection connection)
    {
        try
        {
            if (TableExists(connection, "Roles"))
            {
                // Try common server role names
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT TOP 1 RoleId FROM Roles WHERE RoleName IN ('Server','Waiter','Waitress','Server Staff') ORDER BY RoleId", connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
                // Fallback: smallest RoleId as a heuristic for staff
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT MIN(RoleId) FROM Roles", connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }
        }
        catch
        {
            // ignore and fallback
        }
        return 2; // default role id for Server if unknown
    }

    private bool ColumnExists(Microsoft.Data.SqlClient.SqlConnection connection, string tableName, string columnName)
    {
        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c", connection))
        {
            cmd.Parameters.AddWithValue("@t", "dbo." + tableName);
            cmd.Parameters.AddWithValue("@c", columnName);
            var result = (int)cmd.ExecuteScalar();
            return result > 0;
        }
    }

    private bool TableExists(Microsoft.Data.SqlClient.SqlConnection connection, string tableName)
    {
        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT COUNT(1) FROM sys.tables WHERE name = @t", connection))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            var result = (int)cmd.ExecuteScalar();
            return result > 0;
        }
    }

        private Reservation GetReservationById(int id)
        {
            Reservation reservation = null;
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, CustomerName, PhoneNumber, Email, PartySize, ReservationTime, SpecialRequests, Status
                    FROM Reservations
                    WHERE Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reservation = new Reservation
                            {
                                Id = reader.GetInt32(0),
                                CustomerName = reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                ReservationTime = reader.GetDateTime(5),
                                SpecialRequests = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Status = (ReservationStatus)reader.GetInt32(7)
                            };
                        }
                    }
                }
            }
            
            return reservation;
        }
        
        private WaitlistEntry GetWaitlistEntryById(int id)
        {
            WaitlistEntry waitlist = null;
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, CustomerName, PhoneNumber, PartySize, CheckInTime, EstimatedWaitMinutes, SpecialRequests, Status
                    FROM Waitlist
                    WHERE Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            waitlist = new WaitlistEntry
                            {
                                Id = reader.GetInt32(0),
                                CustomerName = reader.GetString(1),
                                PhoneNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                                PartySize = reader.GetInt32(3),
                                CheckInTime = reader.GetDateTime(4),
                                EstimatedWaitMinutes = reader.GetInt32(5),
                                SpecialRequests = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Status = (WaitlistStatus)reader.GetInt32(7)
                            };
                        }
                    }
                }
            }
            
            return waitlist;
        }
        
        private List<ActiveTableViewModel> GetActiveTables()
        {
            var activeTables = new List<ActiveTableViewModel>();
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT
                        o.Id AS OrderId,
                        t.Id AS TableId,
                        t.TableName,
                        o.CustomerName,
                        1 AS PartySize, -- Default party size, could be enhanced
                        o.CreatedAt AS SeatedAt,
                        o.CreatedAt AS StartedServiceAt,
                        o.CompletedAt,
                        o.Status,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        u.Id AS ServerId,
                        DATEDIFF(MINUTE, o.CreatedAt, GETDATE()) AS MinutesSinceSeated,
                        60 AS TargetTurnTimeMinutes, -- Default target time
                        merged.MergedTableNames,
                        merged.TableCount,
                        CASE WHEN merged.TableCount > 1 THEN 1 ELSE 0 END AS IsPartOfMergedOrder
                    FROM Orders o
                    INNER JOIN OrderTables ot ON o.Id = ot.OrderId
                    INNER JOIN Tables t ON ot.TableId = t.Id
                    LEFT JOIN ServerAssignments sa ON t.Id = sa.TableId AND sa.IsActive = 1
                    LEFT JOIN Users u ON sa.ServerId = u.Id
                    LEFT JOIN (
                        SELECT 
                            ot2.OrderId,
                            STRING_AGG(t2.TableName, ' + ') WITHIN GROUP (ORDER BY t2.TableName) AS MergedTableNames,
                            COUNT(*) AS TableCount
                        FROM OrderTables ot2
                        INNER JOIN Tables t2 ON ot2.TableId = t2.Id
                        GROUP BY ot2.OrderId
                    ) merged ON o.Id = merged.OrderId
                    WHERE o.Status IN (0, 1, 2) -- Active orders: New, In Progress, Ready
                    ORDER BY t.TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var tableName = reader.GetString(2);
                            var mergedTableNames = reader.IsDBNull(13) ? null : reader.GetString(13);
                            var tableCount = reader.IsDBNull(14) ? 1 : reader.GetInt32(14);
                            var isPartOfMerged = reader.GetInt32(15) == 1;
                            
                            activeTables.Add(new ActiveTableViewModel
                            {
                                TurnoverId = reader.GetInt32(0), // Using OrderId as TurnoverId
                                TableId = reader.GetInt32(1),
                                TableName = tableName, // Keep individual table name
                                GuestName = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                SeatedAt = reader.GetDateTime(5),
                                StartedServiceAt = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                CompletedAt = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                                Status = reader.GetInt32(8),
                                ServerName = reader.IsDBNull(9) ? "Unassigned" : reader.GetString(9),
                                ServerId = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                                Duration = reader.GetInt32(11),
                                TargetTurnTime = reader.GetInt32(12),
                                // Store merged table info for display
                                MergedTableNames = mergedTableNames,
                                IsPartOfMergedOrder = isPartOfMerged
                            });
                        }
                    }
                }
            }
            
            return activeTables;
        }
        
        private (bool Success, string Message) UpdateOrderStatusForAllMergedTables(int orderId, int newStatus)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // First, get the order and check if it's merged
                    string checkOrderSql = @"
                        SELECT o.Id, o.CustomerName, COUNT(ot.TableId) as TableCount,
                               STRING_AGG(t.TableName, ' + ') WITHIN GROUP (ORDER BY t.TableName) AS MergedTables
                        FROM Orders o
                        INNER JOIN OrderTables ot ON o.Id = ot.OrderId
                        INNER JOIN Tables t ON ot.TableId = t.Id
                        WHERE o.Id = @OrderId
                        GROUP BY o.Id, o.CustomerName";
                    
                    using (Microsoft.Data.SqlClient.SqlCommand checkCmd = new Microsoft.Data.SqlClient.SqlCommand(checkOrderSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = checkCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var tableCount = reader.GetInt32("TableCount");
                                var mergedTables = reader.GetString("MergedTables");
                                var customerName = reader.IsDBNull("CustomerName") ? "Unknown" : reader.GetString("CustomerName");
                                
                                reader.Close();
                                
                                // Update order status
                                string updateOrderSql = "UPDATE Orders SET Status = @NewStatus WHERE Id = @OrderId";
                                using (Microsoft.Data.SqlClient.SqlCommand updateCmd = new Microsoft.Data.SqlClient.SqlCommand(updateOrderSql, connection))
                                {
                                    updateCmd.Parameters.AddWithValue("@NewStatus", newStatus);
                                    updateCmd.Parameters.AddWithValue("@OrderId", orderId);
                                    updateCmd.ExecuteNonQuery();
                                }
                                
                                var statusText = newStatus switch
                                {
                                    0 => "Seated",
                                    1 => "In Service", 
                                    2 => "Check Requested",
                                    3 => "Paid",
                                    4 => "Completed",
                                    _ => "Updated"
                                };
                                
                                if (tableCount > 1)
                                {
                                    return (true, $"Status updated to '{statusText}' for all merged tables ({mergedTables}) - Customer: {customerName}");
                                }
                                else
                                {
                                    return (true, $"Status updated to '{statusText}' for table {mergedTables} - Customer: {customerName}");
                                }
                            }
                            else
                            {
                                return (false, "Order not found");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
        }
        
        private (bool Success, string Message) StartTableTurnover(int tableId, int? reservationId, int? waitlistId, string guestName, int partySize, string notes, int targetTurnTime)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_StartTableTurnover", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@TableId", tableId);
                        command.Parameters.AddWithValue("@ReservationId", reservationId ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@WaitlistId", waitlistId ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@GuestName", guestName);
                        command.Parameters.AddWithValue("@PartySize", partySize);
                        command.Parameters.AddWithValue("@Notes", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                        command.Parameters.AddWithValue("@TargetTurnTimeMinutes", targetTurnTime);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string message = reader.GetString(0);
                                
                                // If message doesn't contain "error" keyword, consider it success
                                bool success = !message.ToLower().Contains("error");
                                
                                return (success, message);
                            }
                        }
                    }
                }
                
                return (false, "Unknown error occurred.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
        
        private (bool Success, string Message) AssignServerToTable(int tableId, int serverId, int assignedById)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_AssignServerToTable", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@TableId", tableId);
                        command.Parameters.AddWithValue("@ServerId", serverId);
                        command.Parameters.AddWithValue("@AssignedById", assignedById);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string message = reader.GetString(0);
                                
                                // If message doesn't contain "error" keyword, consider it success
                                bool success = !message.ToLower().Contains("error") && 
                                               !message.ToLower().Contains("invalid") && 
                                               !message.ToLower().Contains("does not exist");
                                
                                return (success, message);
                            }
                        }
                    }
                }
                
                return (false, "Unknown error occurred.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
        
        private (bool Success, string Message) UpdateTableTurnoverStatus(int turnoverId, int newStatus)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_UpdateTableTurnoverStatus", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@TurnoverId", turnoverId);
                        command.Parameters.AddWithValue("@NewStatus", newStatus);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string message = reader.GetString(0);
                                
                                // If message doesn't contain "error" keyword, consider it success
                                bool success = !message.ToLower().Contains("error");
                                
                                return (success, message);
                            }
                        }
                    }
                }
                
                return (false, "Unknown error occurred.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
        }
        
        private int GetCurrentUserId()
        {
            // In a real application, get this from authentication
            // For now, hardcode to 1 (assuming ID 1 is an admin/host user)
            return 1;
        }
    }
}
