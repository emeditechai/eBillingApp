using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.ViewModels;
using System.Data;
using System.Linq;
using System.Security.Claims;

namespace RestaurantManagementSystem.Controllers
{
    /// <summary>
    /// BOT (Bar Order Ticket) Controller
    /// Manages bar/beverage orders using KitchenTickets table filtered by KitchenStation='BAR'
    /// </summary>
    public class BOTController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ILogger<BOTController> _logger;

        public BOTController(IConfiguration configuration, ILogger<BOTController> logger)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        /// <summary>
        /// BOT Dashboard - Shows BAR tickets organized by status (similar to Kitchen Dashboard)
        /// </summary>
        public IActionResult Dashboard()
        {
            var viewModel = new BOTDashboardViewModel();
            
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get tickets by status - filtered by KitchenStation='BAR'
                viewModel.NewTickets = GetBOTTicketsByStatus(connection, 0);
                viewModel.InProgressTickets = GetBOTTicketsByStatus(connection, 1);
                viewModel.ReadyTickets = GetBOTTicketsByStatus(connection, 2);
                
                // Delivered tickets (today only)
                var deliveredAll = GetBOTTicketsByStatus(connection, 3);
                var today = DateTime.Today;
                viewModel.DeliveredTickets = deliveredAll
                    .Where(t => t.CompletedAt.HasValue && (
                        t.CompletedAt.Value.Date == today ||
                        t.CompletedAt.Value.ToLocalTime().Date == today ||
                        t.CompletedAt.Value.ToUniversalTime().Date == today
                    ))
                    .OrderByDescending(t => t.CompletedAt)
                    .ToList();
                
                // Get dashboard statistics
                viewModel.Stats = GetBOTDashboardStats(connection);
            }
            
            return View(viewModel);
        }

        /// <summary>
        /// BAR Tickets - Clone of Kitchen/Tickets, filtered to KitchenStation='BAR'
        /// </summary>
        public IActionResult Tickets(BOTTicketsFilterViewModel filter)
        {
            var viewModel = new BOTTicketsViewModel
            {
                Filter = filter ?? new BOTTicketsFilterViewModel()
            };

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Load tickets for BAR with filters
                viewModel.Tickets = GetFilteredBOTTickets(connection, viewModel.Filter);

                // Compute stats for BAR (New/InProgress/Ready) within date filter scope
                viewModel.Stats = GetBOTTicketsStats(connection, viewModel.Filter);
            }

            return View(viewModel);
        }

        /// <summary>
        /// Export BAR Tickets to CSV (same columns as Kitchen export)
        /// </summary>
        public IActionResult ExportCsv(BOTTicketsFilterViewModel filter)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                var tickets = GetFilteredBOTTickets(connection, filter ?? new BOTTicketsFilterViewModel());

                var csv = "TicketNumber,OrderNumber,TableName,StationName,Status,CreatedAt,WaitMinutes\n";
                foreach (var t in tickets)
                {
                    var line = $"{t.TicketNumber},\"{t.OrderNumber}\",\"{(t.TableName ?? "").Replace("\"", "\"\"")}\",\"{(t.StationName ?? "").Replace("\"", "\"\"")}\",\"{t.StatusDisplay}\",{t.CreatedAt:O},{t.MinutesSinceCreated}\n";
                    csv += line;
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                return File(bytes, "text/csv", "bar-tickets.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting BAR tickets CSV");
                TempData["ErrorMessage"] = "Error exporting CSV: " + ex.Message;
                return RedirectToAction(nameof(Tickets));
            }
        }

        /// <summary>
        /// Printer-friendly BAR tickets list
        /// </summary>
        public IActionResult PrintTickets(BOTTicketsFilterViewModel filter)
        {
            try
            {
                var vm = new BOTTicketsViewModel { Filter = filter ?? new BOTTicketsFilterViewModel() };
                using var connection = new SqlConnection(_connectionString);
                connection.Open();
                vm.Tickets = GetFilteredBOTTickets(connection, vm.Filter);
                vm.Stats = GetBOTTicketsStats(connection, vm.Filter);
                return View("Tickets", vm); // Reuse the same view for print (user can use browser print)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing BAR tickets print view");
                TempData["ErrorMessage"] = "Error preparing print view: " + ex.Message;
                return RedirectToAction(nameof(Tickets));
            }
        }

        private List<KitchenTicket> GetFilteredBOTTickets(SqlConnection connection, BOTTicketsFilterViewModel filter)
        {
            var tickets = new List<KitchenTicket>();
            var sql = @"
                SELECT 
                    kt.Id,
                    kt.TicketNumber,
                    kt.OrderId,
                    o.OrderNumber,
                    kt.KitchenStationId,
                    ISNULL(kt.KitchenStation, 'BAR') AS StationName,
                    kt.Status,
                    kt.CreatedAt,
                    kt.CompletedAt,
                    DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated,
                    COALESCE(t.TableName, t.TableNumber, CONCAT('Table ', t.Id), '-') AS TableName
                FROM KitchenTickets kt
                INNER JOIN Orders o ON kt.OrderId = o.Id
                LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                LEFT JOIN Tables t ON tt.TableId = t.Id
                WHERE kt.KitchenStation = 'BAR' 
                  AND kt.TicketNumber LIKE 'BOT-%'
                  AND (@Status IS NULL OR kt.Status = @Status)
                  AND (@DateFrom IS NULL OR kt.CreatedAt >= @DateFrom)
                  AND (@DateTo IS NULL OR kt.CreatedAt < @DateTo)
                ORDER BY kt.CreatedAt DESC";

            using (var cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@Status", (object?)filter?.Status ?? DBNull.Value);

                // Normalize date range: inclusive from, inclusive to(end of day)
                var dateFrom = filter?.DateFrom?.Date;
                var dateToExclusive = filter?.DateTo?.Date.AddDays(1);
                cmd.Parameters.AddWithValue("@DateFrom", (object?)dateFrom ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateTo", (object?)dateToExclusive ?? DBNull.Value);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tickets.Add(new KitchenTicket
                    {
                        Id = reader.GetInt32(0),
                        TicketNumber = reader.GetString(1),
                        OrderId = reader.GetInt32(2),
                        OrderNumber = reader.GetString(3),
                        KitchenStationId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        StationName = reader.GetString(5),
                        Status = reader.GetInt32(6),
                        CreatedAt = reader.GetDateTime(7),
                        CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        MinutesSinceCreated = reader.GetInt32(9),
                        TableName = reader.GetString(10)
                    });
                }
            }

            return tickets;
        }

        private Models.BOTDashboardStats GetBOTTicketsStats(SqlConnection connection, BOTTicketsFilterViewModel filter)
        {
            var stats = new Models.BOTDashboardStats();
            var sql = @"
                SELECT 
                    SUM(CASE WHEN kt.Status = 0 THEN 1 ELSE 0 END) AS NewCount,
                    SUM(CASE WHEN kt.Status = 1 THEN 1 ELSE 0 END) AS InProgressCount,
                    SUM(CASE WHEN kt.Status = 2 THEN 1 ELSE 0 END) AS ReadyCount
                FROM KitchenTickets kt
                WHERE kt.KitchenStation = 'BAR'
                  AND kt.TicketNumber LIKE 'BOT-%'
                  AND (@DateFrom IS NULL OR kt.CreatedAt >= @DateFrom)
                  AND (@DateTo IS NULL OR kt.CreatedAt < @DateTo)";

            using (var cmd = new SqlCommand(sql, connection))
            {
                var dateFrom = filter?.DateFrom?.Date;
                var dateToExclusive = filter?.DateTo?.Date.AddDays(1);
                cmd.Parameters.AddWithValue("@DateFrom", (object?)dateFrom ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DateTo", (object?)dateToExclusive ?? DBNull.Value);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    stats.NewBOTsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    stats.InProgressBOTsCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    stats.ReadyBOTsCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    stats.AvgPrepTimeMinutes = 0; // not computed currently
                }
            }
            return stats;
        }

        /// <summary>
        /// Update BOT ticket status (POST)
        /// </summary>
        [HttpPost]
        public IActionResult UpdateTicketStatus(BOTStatusUpdateModel model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Update status and timestamps
                var sql = @"
                    UPDATE KitchenTickets 
                    SET Status = @Status";
                
                // Set CompletedAt when status is Delivered (3)
                if (model.Status == 3)
                {
                    sql += ", CompletedAt = GETDATE()";
                }
                
                sql += " WHERE Id = @TicketId AND KitchenStation = 'BAR'";
                
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@TicketId", model.TicketId);
                    command.Parameters.AddWithValue("@Status", model.Status);
                    
                    command.ExecuteNonQuery();
                }
            }
            
            return RedirectToAction("Dashboard");
        }

        /// <summary>
        /// Get BOT tickets by status from KitchenTickets table
        /// </summary>
        private List<KitchenTicket> GetBOTTicketsByStatus(SqlConnection connection, int status)
        {
            var tickets = new List<KitchenTicket>();
            
            var sql = @"
                SELECT 
                    kt.Id,
                    kt.TicketNumber,
                    kt.OrderId,
                    o.OrderNumber,
                    kt.KitchenStationId,
                    ISNULL(kt.KitchenStation, 'BAR') AS StationName,
                    kt.Status,
                    kt.CreatedAt,
                    kt.CompletedAt,
                    DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated,
                    COALESCE(t.TableName, t.TableNumber, CONCAT('Table ', t.Id), '-') AS TableName
                FROM KitchenTickets kt
                INNER JOIN Orders o ON kt.OrderId = o.Id
                LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                LEFT JOIN Tables t ON tt.TableId = t.Id
                WHERE kt.KitchenStation = 'BAR' 
                  AND kt.Status = @Status
                  AND kt.TicketNumber LIKE 'BOT-%'
                ORDER BY kt.CreatedAt ASC";
            
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Status", status);
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tickets.Add(new KitchenTicket
                        {
                            Id = reader.GetInt32(0),
                            TicketNumber = reader.GetString(1),
                            OrderId = reader.GetInt32(2),
                            OrderNumber = reader.GetString(3),
                            KitchenStationId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                            StationName = reader.GetString(5),
                            Status = reader.GetInt32(6),
                            CreatedAt = reader.GetDateTime(7),
                            CompletedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                            MinutesSinceCreated = reader.GetInt32(9),
                            TableName = reader.GetString(10)
                        });
                    }
                }
            }
            
            return tickets;
        }

        /// <summary>
        /// Get BOT dashboard statistics from KitchenTickets table
        /// </summary>
        private Models.BOTDashboardStats GetBOTDashboardStats(SqlConnection connection)
        {
            var stats = new Models.BOTDashboardStats();
            
            var sql = @"
                SELECT 
                    SUM(CASE WHEN kt.Status = 0 THEN 1 ELSE 0 END) AS NewCount,
                    SUM(CASE WHEN kt.Status = 1 THEN 1 ELSE 0 END) AS InProgressCount,
                    SUM(CASE WHEN kt.Status = 2 THEN 1 ELSE 0 END) AS ReadyCount,
                    0 AS AvgPrepTimeMinutes
                FROM KitchenTickets kt
                WHERE kt.KitchenStation = 'BAR'
                  AND kt.TicketNumber LIKE 'BOT-%'
                  AND kt.Status < 3";
            
            using (var command = new SqlCommand(sql, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        stats.NewBOTsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        stats.InProgressBOTsCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        stats.ReadyBOTsCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                        stats.AvgPrepTimeMinutes = 0; // No UpdatedAt column to compute
                    }
                }
            }
            
            // Get item counts per status for BAR
            var itemCountSql = @"
                SELECT kt.Status, COUNT(kti.Id) as ItemCount
                FROM KitchenTickets kt
                INNER JOIN KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
                WHERE kt.KitchenStation = 'BAR'
                  AND kt.TicketNumber LIKE 'BOT-%'
                  AND kt.Status IN (0, 1, 2)
                GROUP BY kt.Status";
            
            using (var itemCmd = new SqlCommand(itemCountSql, connection))
            {
                using (var itemReader = itemCmd.ExecuteReader())
                {
                    while (itemReader.Read())
                    {
                        int status = itemReader.GetInt32(0);
                        int itemCount = itemReader.GetInt32(1);
                        
                        switch (status)
                        {
                            case 0: // New
                                stats.NewItemsCount = itemCount;
                                break;
                            case 1: // In Progress
                                stats.InProgressItemsCount = itemCount;
                                break;
                            case 2: // Ready
                                stats.ReadyItemsCount = itemCount;
                                break;
                        }
                    }
                }
            }
            
            return stats;
        }

        /// <summary>
        /// Check BOT database setup status
        /// </summary>
        public IActionResult CheckSetup()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var setupStatus = new Dictionary<string, string>();

                // Check tables
                var tables = new[] { "BOT_Header", "BOT_Detail", "BOT_Audit", "BOT_Bills", "BOT_Payments" };
                foreach (var table in tables)
                {
                    using var cmd = new SqlCommand($@"
                        SELECT CASE WHEN OBJECT_ID('dbo.{table}', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END", conn);
                    setupStatus[$"Table: {table}"] = (string)cmd.ExecuteScalar();
                }

                // Check stored procedures
                var procedures = new[] { "GetNextBOTNumber", "GetBOTsByStatus", "GetBOTDetails", "UpdateBOTStatus", "VoidBOT", "GetBOTDashboardStats" };
                foreach (var proc in procedures)
                {
                    using var cmd = new SqlCommand($@"
                        SELECT CASE WHEN OBJECT_ID('dbo.{proc}', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END", conn);
                    setupStatus[$"Procedure: {proc}"] = (string)cmd.ExecuteScalar();
                }

                // Check IsAlcoholic column
                using (var cmd = new SqlCommand(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.MenuItems') AND name = 'IsAlcoholic'
                    ) THEN 'EXISTS' ELSE 'MISSING' END", conn))
                {
                    setupStatus["Column: MenuItems.IsAlcoholic"] = (string)cmd.ExecuteScalar();
                }

                // Check Bar group
                using (var cmd = new SqlCommand(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM menuitemgroup WHERE itemgroup = 'Bar' AND is_active = 1
                    ) THEN 'EXISTS' ELSE 'MISSING' END", conn))
                {
                    setupStatus["Menu Group: Bar"] = (string)cmd.ExecuteScalar();
                }

                return Json(new { success = true, setup = setupStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking BOT setup");
                return Json(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// BOT Details - Show specific BOT with all items
        /// </summary>
        public IActionResult Details(int id)
        {
            try
            {
                var bot = GetBOTDetails(id);
                if (bot == null)
                {
                    TempData["ErrorMessage"] = "BOT not found";
                    return RedirectToAction(nameof(Dashboard));
                }

                return View(bot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading BOT Details for BOT_ID: {BOT_ID}", id);
                TempData["ErrorMessage"] = "Failed to load BOT details: " + ex.Message;
                return RedirectToAction(nameof(Dashboard));
            }
        }

        /// <summary>
        /// Print BOT - Printer-friendly view
        /// </summary>
        public IActionResult Print(int id)
        {
            try
            {
                var bot = GetBOTDetails(id);
                if (bot == null)
                {
                    TempData["ErrorMessage"] = "BOT not found";
                    return RedirectToAction(nameof(Dashboard));
                }

                // Log print action
                LogAudit(bot.BOT_ID, bot.BOT_No, "PRINT", bot.Status, bot.Status, "BOT printed");

                return View(bot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing BOT for BOT_ID: {BOT_ID}", id);
                TempData["ErrorMessage"] = "Failed to print BOT: " + ex.Message;
                return RedirectToAction(nameof(Dashboard));
            }
        }

        /// <summary>
        /// Update BOT status
        /// </summary>
        [HttpPost]
        public IActionResult UpdateStatus(int botId, int newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                using var cmd = new SqlCommand("UpdateBOTStatus", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@BOT_ID", botId);
                cmd.Parameters.AddWithValue("@NewStatus", newStatus);
                cmd.Parameters.AddWithValue("@UpdatedBy", User.Identity?.Name ?? "System");

                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "BOT status updated successfully";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating BOT status for BOT_ID: {BOT_ID}", botId);
                TempData["ErrorMessage"] = "Failed to update BOT status: " + ex.Message;
                return RedirectToAction(nameof(Dashboard));
            }
        }

        /// <summary>
        /// Void BOT with reason
        /// </summary>
        [HttpPost]
        public IActionResult Void(int botId, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] = "Void reason is required";
                    return RedirectToAction(nameof(Details), new { id = botId });
                }

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                using var cmd = new SqlCommand("VoidBOT", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@BOT_ID", botId);
                cmd.Parameters.AddWithValue("@Reason", reason);
                cmd.Parameters.AddWithValue("@VoidedBy", User.Identity?.Name ?? "System");

                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "BOT voided successfully";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding BOT for BOT_ID: {BOT_ID}", botId);
                TempData["ErrorMessage"] = "Failed to void BOT: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id = botId });
            }
        }

        /// <summary>
        /// Get BOT item status via AJAX
        /// </summary>
        [HttpGet]
        public IActionResult GetBOTItemStatus(int botId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var items = new List<object>();
                using var cmd = new SqlCommand(@"
                    SELECT BOT_Detail_ID, Status, MinutesCooking
                    FROM BOT_Detail
                    WHERE BOT_ID = @BOT_ID", conn);

                cmd.Parameters.AddWithValue("@BOT_ID", botId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(new
                    {
                        itemId = reader.GetInt32(0),
                        status = reader.GetInt32(1),
                        minutesCooking = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                    });
                }

                return Json(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BOT item status for BOT_ID: {BOT_ID}", botId);
                return Json(new { error = ex.Message });
            }
        }

        #region Helper Methods

        /// <summary>
        /// Get dashboard statistics
        /// </summary>
        private Models.BOTDashboardStats GetDashboardStats()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Check if stored procedure exists
                using var checkCmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM sys.objects 
                    WHERE object_id = OBJECT_ID(N'[dbo].[GetBOTDashboardStats]') 
                    AND type IN (N'P', N'PC')", conn);
                
                var procedureExists = (int)checkCmd.ExecuteScalar();
                
                if (procedureExists == 0)
                {
                    _logger.LogWarning("GetBOTDashboardStats stored procedure does not exist");
                    return new Models.BOTDashboardStats(); // Return empty stats
                }

                using var cmd = new SqlCommand("GetBOTDashboardStats", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                
                // Add KitchenStation parameter to filter by BAR
                cmd.Parameters.AddWithValue("@KitchenStation", "BAR");

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Models.BOTDashboardStats
                    {
                        NewBOTsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        InProgressBOTsCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        ReadyBOTsCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        BilledTodayCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        TotalActiveBOTs = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        AvgPrepTimeMinutes = reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
                    };
                }

                return new Models.BOTDashboardStats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BOT dashboard stats");
                return new Models.BOTDashboardStats(); // Return empty stats on error
            }
        }

        /// <summary>
        /// Get BOTs filtered by status
        /// </summary>
        private List<BeverageOrderTicket> GetBOTsByStatus(int? statusFilter)
        {
            var bots = new List<BeverageOrderTicket>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Check if stored procedure exists
                using var checkCmd = new SqlCommand(@"
                    SELECT COUNT(*) 
                    FROM sys.objects 
                    WHERE object_id = OBJECT_ID(N'[dbo].[GetBOTsByStatus]') 
                    AND type IN (N'P', N'PC')", conn);
                
                var procedureExists = (int)checkCmd.ExecuteScalar();
                
                if (procedureExists == 0)
                {
                    _logger.LogWarning("GetBOTsByStatus stored procedure does not exist");
                    return bots; // Return empty list
                }

                using var cmd = new SqlCommand("GetBOTsByStatus", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@Status", statusFilter.HasValue ? (object)statusFilter.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@KitchenStation", "BAR"); // Filter by BAR kitchen station

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    bots.Add(new BeverageOrderTicket
                    {
                        BOT_ID = reader.GetInt32(0),
                        BOT_No = reader.GetString(1),
                        OrderId = reader.GetInt32(2),
                        OrderNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                        TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Status = reader.GetInt32(7),
                        SubtotalAmount = reader.GetDecimal(8),
                        TaxAmount = reader.GetDecimal(9),
                        TotalAmount = reader.GetDecimal(10),
                        CreatedAt = reader.GetDateTime(11),
                        MinutesSinceCreated = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BOTs by status");
            }

            return bots;
        }

        /// <summary>
        /// Get BOT details with items
        /// </summary>
        private BeverageOrderTicket? GetBOTDetails(int botId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            using var cmd = new SqlCommand("GetBOTDetails", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@BOT_ID", botId);

            using var reader = cmd.ExecuteReader();

            // Read header
            BeverageOrderTicket? bot = null;
            if (reader.Read())
            {
                bot = new BeverageOrderTicket
                {
                    BOT_ID = reader.GetInt32(0),
                    BOT_No = reader.GetString(1),
                    OrderId = reader.GetInt32(2),
                    OrderNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                    TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Status = reader.GetInt32(7),
                    SubtotalAmount = reader.GetDecimal(8),
                    TaxAmount = reader.GetDecimal(9),
                    TotalAmount = reader.GetDecimal(10),
                    CreatedAt = reader.GetDateTime(11),
                    UpdatedAt = reader.GetDateTime(12),
                    ServedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    BilledAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                    VoidedAt = reader.IsDBNull(15) ? null : reader.GetDateTime(15),
                    VoidReason = reader.IsDBNull(16) ? null : reader.GetString(16)
                };
            }

            if (bot == null)
                return null;

            // Read items
            if (reader.NextResult())
            {
                while (reader.Read())
                {
                    bot.Items.Add(new BeverageOrderTicketItem
                    {
                        BOT_Detail_ID = reader.GetInt32(0),
                        BOT_ID = reader.GetInt32(1),
                        OrderItemId = reader.GetInt32(2),
                        MenuItemId = reader.GetInt32(3),
                        MenuItemName = reader.GetString(4),
                        Quantity = reader.GetInt32(5),
                        UnitPrice = reader.GetDecimal(6),
                        Amount = reader.GetDecimal(7),
                        TaxRate = reader.GetDecimal(8),
                        TaxAmount = reader.GetDecimal(9),
                        IsAlcoholic = reader.GetBoolean(10),
                        SpecialInstructions = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Status = reader.GetInt32(12),
                        StartTime = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                        CompletionTime = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                        Notes = reader.IsDBNull(15) ? null : reader.GetString(15),
                        MinutesCooking = reader.IsDBNull(16) ? 0 : reader.GetInt32(16)
                    });
                }
            }

            return bot;
        }

        /// <summary>
        /// Log audit trail
        /// </summary>
        private void LogAudit(int botId, string botNo, string action, int? oldStatus, int? newStatus, string reason)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                using var cmd = new SqlCommand(@"
                    INSERT INTO BOT_Audit (BOT_ID, BOT_No, Action, OldStatus, NewStatus, UserName, Reason, Timestamp)
                    VALUES (@BOT_ID, @BOT_No, @Action, @OldStatus, @NewStatus, @UserName, @Reason, GETDATE())", conn);

                cmd.Parameters.AddWithValue("@BOT_ID", botId);
                cmd.Parameters.AddWithValue("@BOT_No", botNo);
                cmd.Parameters.AddWithValue("@Action", action);
                cmd.Parameters.AddWithValue("@OldStatus", oldStatus.HasValue ? (object)oldStatus.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@NewStatus", newStatus.HasValue ? (object)newStatus.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@UserName", User.Identity?.Name ?? "System");
                cmd.Parameters.AddWithValue("@Reason", string.IsNullOrWhiteSpace(reason) ? (object)DBNull.Value : reason);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging BOT audit for BOT_ID: {BOT_ID}, Action: {Action}", botId, action);
                // Don't throw - audit logging failure shouldn't break main flow
            }
        }

        #endregion

        #region Bar Order Create

        /// <summary>
        /// Create Bar Order - Clone of Order/Create for bar-specific workflow
        /// </summary>
        public IActionResult BarOrderCreate(int? tableId = null)
        {
            var model = new CreateOrderViewModel();
            
            if (tableId.HasValue)
            {
                model.SelectedTableId = tableId.Value;
                model.OrderType = 0; // 0 = Dine-In
            }
            
            // Get available tables
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get available tables
                using (SqlCommand command = new SqlCommand(@"
                    SELECT Id, TableName, Capacity, Status
                    FROM Tables
                    WHERE Status = 0
                    ORDER BY TableName", connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailableTables.Add(new TableViewModel
                            {
                                Id = reader.GetInt32(0),
                                TableName = reader.GetString(1),
                                Capacity = reader.GetInt32(2),
                                Status = reader.GetInt32(3),
                                StatusDisplay = "Available"
                            });
                        }
                    }
                }
                
                // Get occupied tables with turnover info
                using (SqlCommand command = new SqlCommand(@"
                    SELECT tt.Id, t.Id, t.TableName, tt.GuestName, tt.PartySize, tt.Status
                    FROM TableTurnovers tt
                    INNER JOIN Tables t ON tt.TableId = t.Id
                    WHERE tt.Status < 5 -- Not departed
                    ORDER BY t.TableName", connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.OccupiedTables.Add(new ActiveTableViewModel
                            {
                                TurnoverId = reader.GetInt32(0),
                                TableId = reader.GetInt32(1),
                                TableName = reader.GetString(2),
                                GuestName = reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                Status = reader.GetInt32(5)
                            });
                        }
                    }
                }
            }
            
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BarOrderCreate(CreateOrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                using (SqlCommand command = new SqlCommand("usp_CreateOrder", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;
                                    
                                    // If a table was selected from the TableService Dashboard
                                    if (model.SelectedTableId.HasValue)
                                    {
                                        // Need to seat guests at this table first
                                        int turnoverId = SeatGuestsAtTable(model.SelectedTableId.Value, "Walk-in", 2, connection, transaction);
                                        model.TableTurnoverId = turnoverId;
                                    }
                                    else if (model.TableTurnoverId.HasValue && model.TableTurnoverId < 0)
                                    {
                                        // User selected an available (unseated) table from dropdown (negative sentinel = -TableId)
                                        int availableTableId = Math.Abs(model.TableTurnoverId.Value);
                                        int turnoverId = SeatGuestsAtTable(availableTableId, model.CustomerName ?? "Walk-in", 2, connection, transaction);
                                        model.TableTurnoverId = turnoverId;
                                    }
                                    
                                    command.Parameters.AddWithValue("@TableTurnoverId", model.TableTurnoverId ?? (object)DBNull.Value);
                                    command.Parameters.AddWithValue("@OrderType", model.OrderType);
                                    command.Parameters.AddWithValue("@UserId", GetCurrentUserId());
                                    command.Parameters.AddWithValue("@OrderByUserId", GetCurrentUserId());
                                    command.Parameters.AddWithValue("@OrderByUserName", GetCurrentUserName());
                                    command.Parameters.AddWithValue("@CustomerName", string.IsNullOrEmpty(model.CustomerName) ? (object)DBNull.Value : model.CustomerName);
                                    command.Parameters.AddWithValue("@CustomerPhone", string.IsNullOrEmpty(model.CustomerPhone) ? (object)DBNull.Value : model.CustomerPhone);
                                    command.Parameters.AddWithValue("@SpecialInstructions", string.IsNullOrEmpty(model.SpecialInstructions) ? (object)DBNull.Value : model.SpecialInstructions);
                                    
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        int orderId = 0;
                                        string orderNumber = "";
                                        string message = "Failed to create order.";
                                        if (reader.Read())
                                        {
                                            orderId = reader.GetInt32(0);
                                            orderNumber = reader.GetString(1);
                                            message = reader.GetString(2);
                                        }
                                        reader.Close();
                                        
                                        if (orderId > 0)
                                        {
                                            // Set OrderKitchenType to "Bar" for orders created from Bar navigation
                                            try
                                            {
                                                using (var setKitchenTypeCmd = new SqlCommand(@"
                                                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                                                    BEGIN
                                                        UPDATE dbo.Orders SET OrderKitchenType = 'Bar' WHERE Id = @OrderId
                                                    END", connection, transaction))
                                                {
                                                    setKitchenTypeCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                    setKitchenTypeCmd.ExecuteNonQuery();
                                                }
                                            }
                                            catch { /* non-fatal if column doesn't exist */ }

                                            using (SqlCommand kitchenCommand = new SqlCommand("UpdateKitchenTicketsForOrder", connection, transaction))
                                            {
                                                kitchenCommand.CommandType = CommandType.StoredProcedure;
                                                kitchenCommand.Parameters.AddWithValue("@OrderId", orderId);
                                                kitchenCommand.ExecuteNonQuery();
                                            }

                                            // Add primary table to OrderTables
                                            int? primaryTableId = null;
                                            
                                            if (model.SelectedTableId.HasValue)
                                            {
                                                primaryTableId = model.SelectedTableId.Value;
                                            }
                                            else if (model.TableTurnoverId.HasValue)
                                            {
                                                // Get table ID from TableTurnover
                                                using (var getTableCmd = new SqlCommand("SELECT TableId FROM TableTurnovers WHERE Id = @TurnoverId", connection, transaction))
                                                {
                                                    getTableCmd.Parameters.AddWithValue("@TurnoverId", model.TableTurnoverId.Value);
                                                    var result = getTableCmd.ExecuteScalar();
                                                    if (result != null && result != DBNull.Value)
                                                    {
                                                        primaryTableId = (int)result;
                                                    }
                                                }
                                            }
                                            
                                            if (primaryTableId.HasValue)
                                            {
                                                using (var insertPrimary = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM OrderTables WHERE OrderId=@OrderId AND TableId=@TableId)
                                                    INSERT INTO OrderTables (OrderId, TableId, CreatedAt) VALUES (@OrderId, @TableId, GETDATE());", connection, transaction))
                                                {
                                                    insertPrimary.Parameters.AddWithValue("@OrderId", orderId);
                                                    insertPrimary.Parameters.AddWithValue("@TableId", primaryTableId.Value);
                                                    insertPrimary.ExecuteNonQuery();
                                                }
                                            }
                                            
                                            // Persist merged tables
                                            if (model.SelectedTableIds != null && model.SelectedTableIds.Count > 0)
                                            {
                                                foreach (var mergedTableId in model.SelectedTableIds.Distinct())
                                                {
                                                    if (model.SelectedTableId.HasValue && model.SelectedTableId.Value == mergedTableId)
                                                        continue;
                                                    using (var insertMerge = new SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM OrderTables WHERE OrderId=@OrderId AND TableId=@TableId)
                                                        INSERT INTO OrderTables (OrderId, TableId, CreatedAt) VALUES (@OrderId, @TableId, GETDATE());", connection, transaction))
                                                    {
                                                        insertMerge.Parameters.AddWithValue("@OrderId", orderId);
                                                        insertMerge.Parameters.AddWithValue("@TableId", mergedTableId);
                                                        insertMerge.ExecuteNonQuery();
                                                    }
                                                }
                                            }
                                            
                                            transaction.Commit();
                                            TempData["SuccessMessage"] = $"Bar Order {orderNumber} created successfully.";
                                            TempData["IsBarOrder"] = true; // Flag to indicate this came from Bar
                                            return RedirectToAction("Details", "Order", new { id = orderId, fromBar = true });
                                        }
                                        else
                                        {
                                            transaction.Rollback();
                                            ModelState.AddModelError("", message);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                                _logger.LogError(ex, "Error creating bar order");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                    _logger.LogError(ex, "Error creating bar order");
                }
            }
            
            // If we get here, something went wrong - repopulate the model
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (SqlCommand command = new SqlCommand(@"
                    SELECT Id, TableName, Capacity, Status
                    FROM Tables
                    WHERE Status = 0
                    ORDER BY TableName", connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailableTables.Add(new TableViewModel
                            {
                                Id = reader.GetInt32(0),
                                TableName = reader.GetString(1),
                                Capacity = reader.GetInt32(2),
                                Status = reader.GetInt32(3),
                                StatusDisplay = "Available"
                            });
                        }
                    }
                }
                
                using (SqlCommand command = new SqlCommand(@"
                    SELECT tt.Id, t.Id, t.TableName, tt.GuestName, tt.PartySize, tt.Status
                    FROM TableTurnovers tt
                    INNER JOIN Tables t ON tt.TableId = t.Id
                    WHERE tt.Status < 5
                    ORDER BY t.TableName", connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.OccupiedTables.Add(new ActiveTableViewModel
                            {
                                TurnoverId = reader.GetInt32(0),
                                TableId = reader.GetInt32(1),
                                TableName = reader.GetString(2),
                                GuestName = reader.GetString(3),
                                PartySize = reader.GetInt32(4),
                                Status = reader.GetInt32(5)
                            });
                        }
                    }
                }
            }
            
            return View(model);
        }

        /// <summary>
        /// Helper method to seat guests at a table
        /// </summary>
        private int SeatGuestsAtTable(int tableId, string guestName, int partySize, SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand cmd = new SqlCommand("usp_SeatGuests", connection, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@TableId", tableId);
                cmd.Parameters.AddWithValue("@GuestName", guestName);
                cmd.Parameters.AddWithValue("@PartySize", partySize);
                cmd.Parameters.AddWithValue("@UserId", GetCurrentUserId());

                var result = cmd.ExecuteScalar();
                if (result != null && int.TryParse(result.ToString(), out int turnoverId))
                {
                    return turnoverId;
                }
                throw new Exception("Failed to seat guests at table");
            }
        }

        /// <summary>
        /// Get current user ID
        /// </summary>
        private int GetCurrentUserId()
        {
            try
            {
                var claim = HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out int uid))
                {
                    return uid;
                }
            }
            catch
            {
                // Ignore and fall back to default below
            }

            return 1; // Legacy fallback
        }

        /// <summary>
        /// Get current user name
        /// </summary>
        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? "Admin";
        }

        private bool CurrentUserCanViewAllBarOrderData()
        {
            try
            {
                var roles = HttpContext?.User?.FindAll(ClaimTypes.Role)?.Select(claim => claim.Value) ?? Enumerable.Empty<string>();
                string[] privilegedRoles = ["Administrator", "FloorManager", "Floor Manager"];
                return roles.Any(role => privilegedRoles.Any(privileged => string.Equals(role, privileged, StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Bar Order Dashboard

        /// <summary>
        /// Bar Order Dashboard - Shows orders filtered by Bar menu item group
        /// </summary>
        public IActionResult BarOrderDashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var model = GetBarOrderDashboard(fromDate, toDate);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Bar Order Dashboard");
                TempData["ErrorMessage"] = "Failed to load Bar Order Dashboard: " + ex.Message;
                return View(new OrderDashboardViewModel());
            }
        }

        /// <summary>
        /// Get Bar Order Dashboard data - filtered by Bar menu item group
        /// </summary>
        private OrderDashboardViewModel GetBarOrderDashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var canViewAllRecords = CurrentUserCanViewAllBarOrderData();
            var currentUserId = GetCurrentUserId();
            var model = new OrderDashboardViewModel
            {
                ActiveOrders = new List<OrderSummary>(),
                CompletedOrders = new List<OrderSummary>(),
                CancelledOrders = new List<OrderSummary>()
            };

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // First, get the Bar menu item group ID
                int? barGroupId = null;
                using (var cmd = new SqlCommand("SELECT ID FROM menuitemgroup WHERE itemgroup = 'Bar' AND is_active = 1", connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        barGroupId = Convert.ToInt32(result);
                    }
                }

                // If no Bar group exists, return empty dashboard
                if (!barGroupId.HasValue)
                {
                    return model;
                }

                // Get order counts and total sales for today (orders with OrderKitchenType = 'Bar')
                var summarySql = @"
                    SELECT
                        SUM(CASE WHEN Status = 0 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS OpenCount,
                        SUM(CASE WHEN Status = 1 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS InProgressCount,
                        SUM(CASE WHEN Status = 2 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS ReadyCount,
                        SUM(CASE WHEN Status = 3 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS CompletedCount,
                        SUM(CASE WHEN Status = 3 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN TotalAmount ELSE 0 END) AS TotalSales,
                        SUM(CASE WHEN Status = 4 AND CAST(ISNULL(UpdatedAt, CreatedAt) AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS CancelledCount
                    FROM Orders
                    WHERE OrderKitchenType = 'Bar'";

                if (!canViewAllRecords)
                {
                    summarySql += " AND UserId = @UserId";
                }

                using (var command = new SqlCommand(summarySql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OpenOrdersCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            model.InProgressOrdersCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            model.ReadyOrdersCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                            model.CompletedOrdersCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                            model.TotalSales = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                            model.CancelledOrdersCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                        }
                    }
                }

                // Get active orders with OrderKitchenType = 'Bar'
                var activeOrderSql = @"
                    SELECT 
                        o.Id,
                        o.OrderNumber,
                        o.OrderType,
                        o.Status,
                        CASE 
                            WHEN o.OrderType = 0 THEN t.TableName 
                            ELSE NULL 
                        END AS TableName,
                        CASE 
                            WHEN o.OrderType = 0 THEN tt.GuestName 
                            ELSE o.CustomerName 
                        END AS GuestName,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        (SELECT COUNT(1) FROM OrderItems WHERE OrderId = o.Id) AS ItemCount,
                        o.TotalAmount,
                        o.CreatedAt,
                        DATEDIFF(MINUTE, o.CreatedAt, GETDATE()) AS DurationMinutes
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    LEFT JOIN Users u ON o.UserId = u.Id
                    WHERE o.Status < 3 AND o.OrderKitchenType = 'Bar'";

                if (!canViewAllRecords)
                {
                    activeOrderSql += " AND o.UserId = @UserId";
                }

                activeOrderSql += " ORDER BY o.CreatedAt DESC";

                using (var command = new SqlCommand(activeOrderSql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var orderType = reader.GetInt32(2);
                            string orderTypeDisplay = orderType switch
                            {
                                0 => "Dine-In",
                                1 => "Takeout",
                                2 => "Delivery",
                                3 => "Online",
                                _ => "Unknown"
                            };

                            var status = reader.GetInt32(3);
                            string statusDisplay = status switch
                            {
                                0 => "Open",
                                1 => "In Progress",
                                2 => "Ready",
                                3 => "Completed",
                                4 => "Cancelled",
                                _ => "Unknown"
                            };

                            var summary = new OrderSummary
                            {
                                Id = reader.GetInt32(0),
                                OrderNumber = reader.GetString(1),
                                OrderType = orderType,
                                OrderTypeDisplay = orderTypeDisplay,
                                Status = status,
                                StatusDisplay = statusDisplay,
                                TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                ItemCount = reader.GetInt32(7),
                                TotalAmount = reader.GetDecimal(8),
                                CreatedAt = reader.GetDateTime(9),
                                Duration = TimeSpan.FromMinutes(reader.GetInt32(10))
                            };

                            model.ActiveOrders.Add(summary);
                        }
                    }
                }

                // Get completed orders with OrderKitchenType = 'Bar' (filtered by date range if provided)
                string completedSql = @"
                    SELECT 
                        o.Id,
                        o.OrderNumber,
                        o.OrderType,
                        o.Status,
                        CASE 
                            WHEN o.OrderType = 0 THEN t.TableName 
                            ELSE NULL 
                        END AS TableName,
                        CASE 
                            WHEN o.OrderType = 0 THEN tt.GuestName 
                            ELSE o.CustomerName 
                        END AS GuestName,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        (SELECT COUNT(1) FROM OrderItems WHERE OrderId = o.Id) AS ItemCount,
                        o.TotalAmount,
                        o.CreatedAt,
                        o.CompletedAt,
                        DATEDIFF(MINUTE, o.CreatedAt, o.CompletedAt) AS DurationMinutes
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    LEFT JOIN Users u ON o.UserId = u.Id
                    WHERE o.Status = 3 AND o.OrderKitchenType = 'Bar'
                ";

                if (!canViewAllRecords)
                {
                    completedSql += " AND o.UserId = @UserId";
                }

                if (fromDate.HasValue && toDate.HasValue)
                {
                    completedSql += " AND CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate ORDER BY o.CompletedAt DESC";
                }
                else
                {
                    // default: today
                    completedSql += " AND CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE) ORDER BY o.CompletedAt DESC";
                }

                using (var command = new SqlCommand(completedSql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    if (fromDate.HasValue && toDate.HasValue)
                    {
                        command.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
                        command.Parameters.AddWithValue("@ToDate", toDate.Value.Date);
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var orderType = reader.GetInt32(2);
                            string orderTypeDisplay = orderType switch
                            {
                                0 => "Dine-In",
                                1 => "Takeout",
                                2 => "Delivery",
                                3 => "Online",
                                _ => "Unknown"
                            };

                            var completedSummary = new OrderSummary
                            {
                                Id = reader.GetInt32(0),
                                OrderNumber = reader.GetString(1),
                                OrderType = orderType,
                                OrderTypeDisplay = orderTypeDisplay,
                                Status = 3,
                                StatusDisplay = "Completed",
                                TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                ItemCount = reader.GetInt32(7),
                                TotalAmount = reader.GetDecimal(8),
                                CreatedAt = reader.GetDateTime(9),
                                Duration = TimeSpan.FromMinutes(reader.IsDBNull(11) ? 0 : reader.GetInt32(11))
                            };

                            model.CompletedOrders.Add(completedSummary);
                        }
                    }
                }
                
                // Get cancelled bar orders for today
                var cancelledSql = @"
                    SELECT 
                        o.Id,
                        o.OrderNumber,
                        o.OrderType,
                        o.Status,
                        CASE 
                            WHEN o.OrderType = 0 THEN t.TableName 
                            ELSE NULL 
                        END AS TableName,
                        CASE 
                            WHEN o.OrderType = 0 THEN tt.GuestName 
                            ELSE o.CustomerName 
                        END AS GuestName,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        (SELECT COUNT(1) FROM OrderItems WHERE OrderId = o.Id) AS ItemCount,
                        o.TotalAmount,
                        o.CreatedAt,
                        DATEDIFF(MINUTE, o.CreatedAt, ISNULL(o.UpdatedAt, GETDATE())) AS DurationMinutes
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    LEFT JOIN Users u ON o.UserId = u.Id
                    WHERE o.Status = 4 
                    AND o.OrderKitchenType = 'Bar'
                    AND CAST(ISNULL(o.UpdatedAt, o.CreatedAt) AS DATE) = CAST(GETDATE() AS DATE)
                ";

                if (!canViewAllRecords)
                {
                    cancelledSql += " AND o.UserId = @UserId";
                }

                cancelledSql += " ORDER BY ISNULL(o.UpdatedAt, o.CreatedAt) DESC";

                using (var command = new SqlCommand(cancelledSql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var orderType = reader.GetInt32(2);
                            string orderTypeDisplay = orderType switch
                            {
                                0 => "Dine-In",
                                1 => "Takeout",
                                2 => "Delivery",
                                3 => "Online",
                                _ => "Unknown"
                            };

                            var cancelledSummary = new OrderSummary
                            {
                                Id = reader.GetInt32(0),
                                OrderNumber = reader.GetString(1),
                                OrderType = orderType,
                                OrderTypeDisplay = orderTypeDisplay,
                                Status = 4,
                                StatusDisplay = "Cancelled",
                                TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                ItemCount = reader.GetInt32(7),
                                TotalAmount = reader.GetDecimal(8),
                                CreatedAt = reader.GetDateTime(9),
                                Duration = TimeSpan.FromMinutes(reader.IsDBNull(10) ? 0 : reader.GetInt32(10))
                            };

                            model.CancelledOrders.Add(cancelledSummary);
                        }
                    }
                }
            }

            return model;
        }

        /// <summary>
        /// Get BOT tickets from KitchenTickets table filtered by KitchenStation = 'BAR'
        /// </summary>
        private List<BeverageOrderTicket> GetBOTsFromKitchenTickets(int? statusFilter)
        {
            var bots = new List<BeverageOrderTicket>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Check if KitchenStation column exists
                bool hasKitchenStationColumn = false;
                using (var checkCmd = new SqlCommand(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.KitchenTickets') AND name = 'KitchenStation'
                    ) THEN 1 ELSE 0 END", conn))
                {
                    hasKitchenStationColumn = (int)checkCmd.ExecuteScalar() == 1;
                }

                string query = hasKitchenStationColumn
                    ? @"SELECT 
                            kt.Id AS BOT_ID,
                            kt.TicketNumber AS BOT_No,
                            kt.OrderId,
                            o.OrderNumber,
                            CASE WHEN o.OrderType = 0 THEN t.TableName ELSE NULL END AS TableName,
                            CASE WHEN o.OrderType = 0 THEN tt.GuestName ELSE o.CustomerName END AS GuestName,
                            CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                            kt.Status,
                            ISNULL(SUM(oi.Subtotal), 0) AS SubtotalAmount,
                            0 AS TaxAmount,
                            ISNULL(SUM(oi.Subtotal), 0) AS TotalAmount,
                            kt.CreatedAt,
                            DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated
                        FROM KitchenTickets kt
                        INNER JOIN Orders o ON kt.OrderId = o.Id
                        LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                        LEFT JOIN Tables t ON tt.TableId = t.Id
                        LEFT JOIN Users u ON o.UserId = u.Id
                        LEFT JOIN KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
                        LEFT JOIN OrderItems oi ON kti.OrderItemId = oi.Id
                        WHERE kt.KitchenStation = 'BAR' 
                          AND kt.TicketNumber LIKE 'BOT-%'
                          AND (@Status IS NULL OR kt.Status = @Status)
                        GROUP BY kt.Id, kt.TicketNumber, kt.OrderId, o.OrderNumber, o.OrderType, 
                                 t.TableName, tt.GuestName, o.CustomerName, u.FirstName, u.LastName, 
                                 kt.Status, kt.CreatedAt
                        ORDER BY kt.CreatedAt DESC"
                    : @"SELECT 
                            kt.Id AS BOT_ID,
                            kt.TicketNumber AS BOT_No,
                            kt.OrderId,
                            o.OrderNumber,
                            CASE WHEN o.OrderType = 0 THEN t.TableName ELSE NULL END AS TableName,
                            CASE WHEN o.OrderType = 0 THEN tt.GuestName ELSE o.CustomerName END AS GuestName,
                            CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                            kt.Status,
                            ISNULL(SUM(oi.Subtotal), 0) AS SubtotalAmount,
                            0 AS TaxAmount,
                            ISNULL(SUM(oi.Subtotal), 0) AS TotalAmount,
                            kt.CreatedAt,
                            DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated
                        FROM KitchenTickets kt
                        INNER JOIN Orders o ON kt.OrderId = o.Id
                        LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                        LEFT JOIN Tables t ON tt.TableId = t.Id
                        LEFT JOIN Users u ON o.UserId = u.Id
                        LEFT JOIN KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
                        LEFT JOIN OrderItems oi ON kti.OrderItemId = oi.Id
                        WHERE kt.TicketNumber LIKE 'BOT-%'
                          AND (@Status IS NULL OR kt.Status = @Status)
                        GROUP BY kt.Id, kt.TicketNumber, kt.OrderId, o.OrderNumber, o.OrderType, 
                                 t.TableName, tt.GuestName, o.CustomerName, u.FirstName, u.LastName, 
                                 kt.Status, kt.CreatedAt
                        ORDER BY kt.CreatedAt DESC";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Status", statusFilter.HasValue ? (object)statusFilter.Value : DBNull.Value);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    bots.Add(new BeverageOrderTicket
                    {
                        BOT_ID = reader.GetInt32(0),
                        BOT_No = reader.GetString(1),
                        OrderId = reader.GetInt32(2),
                        OrderNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                        TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Status = reader.GetInt32(7),
                        SubtotalAmount = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetValue(8)),
                        TaxAmount = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetValue(9)),
                        TotalAmount = reader.IsDBNull(10) ? 0 : Convert.ToDecimal(reader.GetValue(10)),
                        CreatedAt = reader.GetDateTime(11),
                        MinutesSinceCreated = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BOTs from KitchenTickets");
            }

            return bots;
        }

        /// <summary>
        /// Get dashboard statistics from KitchenTickets for BAR station
        /// </summary>
        private Models.BOTDashboardStats GetDashboardStatsFromKitchenTickets()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Check if KitchenStation column exists
                bool hasKitchenStationColumn = false;
                using (var checkCmd = new SqlCommand(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.KitchenTickets') AND name = 'KitchenStation'
                    ) THEN 1 ELSE 0 END", conn))
                {
                    hasKitchenStationColumn = (int)checkCmd.ExecuteScalar() == 1;
                }

                string statsQuery = hasKitchenStationColumn
                    ? @"SELECT 
                            SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewBOTsCount,
                            SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS InProgressBOTsCount,
                            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS ReadyBOTsCount,
                            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS BilledTodayCount,
                            COUNT(CASE WHEN Status < 4 THEN 1 END) AS TotalActiveBOTs,
                            0 AS AvgPrepTimeMinutes
                        FROM KitchenTickets
                        WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                          AND KitchenStation = 'BAR'
                          AND TicketNumber LIKE 'BOT-%'"
                    : @"SELECT 
                            SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewBOTsCount,
                            SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS InProgressBOTsCount,
                            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS ReadyBOTsCount,
                            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS BilledTodayCount,
                            COUNT(CASE WHEN Status < 4 THEN 1 END) AS TotalActiveBOTs,
                            0 AS AvgPrepTimeMinutes
                        FROM KitchenTickets
                        WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                          AND TicketNumber LIKE 'BOT-%'";

                using var cmd = new SqlCommand(statsQuery, conn);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new Models.BOTDashboardStats
                    {
                        NewBOTsCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        InProgressBOTsCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        ReadyBOTsCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                        BilledTodayCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        TotalActiveBOTs = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        AvgPrepTimeMinutes = 0 // Fixed: was reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
                    };
                }

                return new Models.BOTDashboardStats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BOT dashboard stats from KitchenTickets");
                return new Models.BOTDashboardStats();
            }
        }

        #endregion
    }
}

