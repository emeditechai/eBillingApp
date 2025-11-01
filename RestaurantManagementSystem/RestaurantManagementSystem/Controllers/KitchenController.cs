using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.ViewModels;

namespace RestaurantManagementSystem.Controllers
{
    public class KitchenController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public KitchenController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Kitchen/Dashboard
        public IActionResult Dashboard(int? stationId)
        {
            var viewModel = new KitchenDashboardViewModel();
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get all kitchen stations
                viewModel.Stations = GetKitchenStations(connection);
                
                // Set the selected station
                string selectedStationName = null;
                if (stationId.HasValue && stationId.Value > 0)
                {
                    viewModel.SelectedStationId = stationId.Value;
                    selectedStationName = viewModel.Stations.FirstOrDefault(s => s.Id == stationId.Value)?.Name;
                    viewModel.SelectedStationName = selectedStationName ?? "All Stations";
                }
                else
                {
                    viewModel.SelectedStationName = "All Stations";
                }
                
                // Get tickets by status and station
                viewModel.NewTickets = GetTicketsByStatus(connection, 0, stationId, selectedStationName);
                viewModel.InProgressTickets = GetTicketsByStatus(connection, 1, stationId, selectedStationName);
                viewModel.ReadyTickets = GetTicketsByStatus(connection, 2, stationId, selectedStationName);
                // Delivered tickets (today only)
                var deliveredAll = GetTicketsByStatus(connection, 3, stationId, selectedStationName);
                var today = DateTime.Today;
                // Normalize CompletedAt to local date and also accept UTC-stored dates
                viewModel.DeliveredTickets = deliveredAll
                    .Where(t => t.CompletedAt.HasValue && (
                        t.CompletedAt.Value.Date == today ||
                        t.CompletedAt.Value.ToLocalTime().Date == today ||
                        t.CompletedAt.Value.ToUniversalTime().Date == today
                    ))
                    .OrderByDescending(t => t.CompletedAt)
                    .ToList();
                
                // Get dashboard statistics
                viewModel.Stats = GetKitchenStats(connection, stationId);

                // Safe alignment: if filtering by a specific station and stats came back empty
                // but we do have tickets in the lists (due to fallback filtering), override counts
                if (stationId.HasValue && stationId.Value > 0)
                {
                    var sum = (viewModel.NewTickets?.Count ?? 0) + (viewModel.InProgressTickets?.Count ?? 0) + (viewModel.ReadyTickets?.Count ?? 0);
                    if (sum > 0 && (viewModel.Stats.TotalTicketsCount == 0))
                    {
                        viewModel.Stats.NewTicketsCount = viewModel.NewTickets?.Count ?? 0;
                        viewModel.Stats.InProgressTicketsCount = viewModel.InProgressTickets?.Count ?? 0;
                        viewModel.Stats.ReadyTicketsCount = viewModel.ReadyTickets?.Count ?? 0;
                        // Keep Pending/Ready Items and Avg prep as-is (we don't recalc here)
                    }
                }
            }
            
            return View(viewModel);
        }
        
        // GET: Kitchen/Tickets
        public IActionResult Tickets(KitchenStationFilterViewModel filter)
        {
            var viewModel = new KitchenTicketsViewModel
            {
                Filter = filter ?? new KitchenStationFilterViewModel()
            };
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get all kitchen stations for the filter
                viewModel.Filter.Stations = GetKitchenStations(connection);
                
                // Get tickets based on filter
                viewModel.Tickets = GetFilteredTickets(connection, filter);
                
                // Get dashboard statistics
                viewModel.Stats = GetKitchenStats(connection, filter.StationId);
            }
            
            return View(viewModel);
        }

        // GET: Kitchen/ExportCsv
        public IActionResult ExportCsv(KitchenStationFilterViewModel filter)
        {
            try
            {
                var tickets = new List<KitchenTicket>();
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    tickets = GetFilteredTickets(connection, filter);
                }

                var csv = "TicketNumber,OrderNumber,TableName,StationName,Status,CreatedAt,WaitMinutes\n";
                foreach (var t in tickets)
                {
                    var line = $"{t.TicketNumber},\"{t.OrderNumber}\",\"{(t.TableName ?? "").Replace("\"", "\"\"")}\",\"{(t.StationName ?? "").Replace("\"", "\"\"")}\",\"{t.StatusDisplay}\",{t.CreatedAt:O},{t.MinutesSinceCreated}\n";
                    csv += line;
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                return File(bytes, "text/csv", "kitchen-tickets.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error exporting CSV: " + ex.Message;
                return RedirectToAction("Tickets");
            }
        }

        // GET: Kitchen/Print
        public IActionResult Print(KitchenStationFilterViewModel filter)
        {
            try
            {
                var viewModel = new KitchenTicketsViewModel { Filter = filter ?? new KitchenStationFilterViewModel() };
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    viewModel.Filter.Stations = GetKitchenStations(connection);
                    viewModel.Tickets = GetFilteredTickets(connection, filter);
                    viewModel.Stats = GetKitchenStats(connection, filter?.StationId);
                }

                return View("Print", viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error preparing print view: " + ex.Message;
                return RedirectToAction("Tickets");
            }
        }
        
        // GET: Kitchen/TicketDetails/{id}
        public IActionResult TicketDetails(int id)
        {
            var viewModel = new KitchenTicketDetailsViewModel();
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get ticket details
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetKitchenTicketDetails", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@TicketId", id);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            viewModel.Ticket = new KitchenTicket
                            {
                                Id = (int)reader["Id"],
                                TicketNumber = reader["TicketNumber"].ToString(),
                                OrderId = (int)reader["OrderId"],
                                OrderNumber = reader["OrderNumber"].ToString(),
                                KitchenStationId = reader["KitchenStationId"] as int?,
                                StationName = reader["StationName"].ToString(),
                                TableName = reader["TableName"].ToString(),
                                Status = (int)reader["Status"],
                                CreatedAt = (DateTime)reader["CreatedAt"],
                                CompletedAt = reader["CompletedAt"] as DateTime?,
                                MinutesSinceCreated = (int)reader["MinutesSinceCreated"]
                            };
                            
                            // Safely access OrderNotes column, if it exists
                            try 
                            {
                                // Safely handle OrderNotes column which may not exist in all database schemas
                            try
                            {
                                viewModel.OrderNotes = reader["OrderNotes"].ToString();
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // OrderNotes column not found - set to empty string
                                viewModel.OrderNotes = string.Empty;
                            }
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // OrderNotes column does not exist in this schema
                                viewModel.OrderNotes = string.Empty;
                            }
                        }
                        
                        reader.NextResult();
                        
                        while (reader.Read())
                        {
                            var item = new KitchenTicketItem
                            {
                                Id = (int)reader["Id"],
                                KitchenTicketId = (int)reader["KitchenTicketId"],
                                OrderItemId = (int)reader["OrderItemId"],
                                MenuItemName = reader["MenuItemName"].ToString(),
                                Quantity = (int)reader["Quantity"],
                                SpecialInstructions = reader["SpecialInstructions"].ToString(),
                                Status = (int)reader["Status"],
                                StartTime = reader["StartTime"] as DateTime?,
                                CompletionTime = reader["CompletionTime"] as DateTime?,
                                Notes = reader["Notes"].ToString(),
                                MinutesCooking = (int)reader["MinutesCooking"],
                                KitchenStationId = reader["KitchenStationId"] as int?,
                                StationName = reader["StationName"].ToString(),
                                PrepTime = (int)reader["PrepTime"]
                            };
                            
                            viewModel.Items.Add(item);
                        }
                        
                        // Read modifiers for each item
                        reader.NextResult();
                        
                        while (reader.Read())
                        {
                            int itemId = (int)reader["KitchenTicketItemId"];
                            string modifierText = reader["ModifierText"].ToString();
                            
                            var item = viewModel.Items.FirstOrDefault(i => i.Id == itemId);
                            if (item != null)
                            {
                                item.Modifiers.Add(modifierText);
                            }
                        }
                        
                        // If some items are missing SpecialInstructions/Notes in the ticket row (older DB/migration differences),
                        // try to fetch them from the original OrderItems row as a safe fallback so kitchen can always see instructions.
                        foreach (var itm in viewModel.Items)
                        {
                            if (string.IsNullOrWhiteSpace(itm.Notes) && string.IsNullOrWhiteSpace(itm.SpecialInstructions))
                            {
                                try
                                {
                                    using (var noteCmd = new SqlCommand("SELECT SpecialInstructions FROM OrderItems WHERE Id = @OrderItemId", connection))
                                    {
                                        noteCmd.Parameters.AddWithValue("@OrderItemId", itm.OrderItemId);
                                        var noteObj = noteCmd.ExecuteScalar();
                                        if (noteObj != null && noteObj != DBNull.Value)
                                        {
                                            var noteText = noteObj.ToString();
                                            if (!string.IsNullOrWhiteSpace(noteText))
                                            {
                                                // populate SpecialInstructions so the view shows it (we prefer Notes but fall back to SpecialInstructions)
                                                itm.SpecialInstructions = noteText;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // Ignore failures here - this is a best-effort fallback.
                                }
                            }
                        }
                    }
                }
                
                // Check if ticket can be updated (not delivered or cancelled)
                viewModel.CanUpdateStatus = viewModel.Ticket.Status < 3;
            }
            
            return View(viewModel);
        }
        
        // POST: Kitchen/UpdateTicketStatus
        [HttpPostAttribute]
        public IActionResult UpdateTicketStatus(KitchenStatusUpdateModel model)
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("UpdateKitchenTicketStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@TicketId", model.TicketId);
                    command.Parameters.AddWithValue("@Status", model.Status);
                    
                    command.ExecuteNonQuery();
                }
            }
            
            return RedirectToAction("TicketDetails", new { id = model.TicketId });
        }
        
        // POST: Kitchen/UpdateItemStatus
        [HttpPostAttribute]
        public IActionResult UpdateItemStatus(KitchenItemStatusUpdateModel model, int ticketId)
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("UpdateKitchenTicketItemStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ItemId", model.ItemId);
                    command.Parameters.AddWithValue("@Status", model.Status);
                    
                    command.ExecuteNonQuery();
                }
            }
            
            return RedirectToAction("TicketDetails", new { id = ticketId });
        }
        
        // GET: Kitchen/Stations
        public IActionResult Stations()
        {
            var stations = new List<KitchenStation>();
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                stations = GetKitchenStations(connection);
            }
            
            return View(stations);
        }
        
        // GET: Kitchen/StationForm
        public IActionResult StationForm(int? id)
        {
            var viewModel = new KitchenStationViewModel();
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                if (id.HasValue && id.Value > 0)
                {
                    // Edit existing station
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetKitchenStationById", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@StationId", id.Value);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                viewModel.Id = (int)reader["Id"];
                                viewModel.Name = reader["Name"].ToString();
                                viewModel.Description = reader["Description"].ToString();
                                viewModel.IsActive = (bool)reader["IsActive"];
                            }
                            
                            reader.NextResult();
                            
                            // Get assigned menu items
                            while (reader.Read())
                            {
                                int menuItemId = (int)reader["MenuItemId"];
                                viewModel.AssignedMenuItemIds.Add(menuItemId);
                            }
                        }
                    }
                }
                
                // Get all menu items for assignment
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("sp_GetAllMenuItems", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var menuItem = new MenuItemOption
                            {
                                Id = (int)reader["Id"],
                                Name = reader["Name"].ToString(),
                                Category = reader["CategoryName"].ToString(),
                                IsAssigned = viewModel.AssignedMenuItemIds.Contains((int)reader["Id"]),
                                IsPrimary = true // Default primary for assigned items
                            };
                            
                            viewModel.AvailableMenuItems.Add(menuItem);
                        }
                    }
                }
            }
            
            return View(viewModel);
        }
        
        // POST: Kitchen/SaveStation
        [HttpPostAttribute]
        public IActionResult SaveStation(KitchenStationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get all menu items again to repopulate the form
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand("sp_GetAllMenuItems", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var menuItem = new MenuItemOption
                                {
                                    Id = (int)reader["Id"],
                                    Name = reader["Name"].ToString(),
                                    Category = reader["CategoryName"].ToString(),
                                    IsAssigned = model.AssignedMenuItemIds?.Contains((int)reader["Id"]) ?? false,
                                    IsPrimary = true
                                };
                                
                                model.AvailableMenuItems.Add(menuItem);
                            }
                        }
                    }
                }
                
                return View("StationForm", model);
            }
            
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int stationId;
                        
                        if (model.Id > 0)
                        {
                            // Update existing station
                            using (var command = new Microsoft.Data.SqlClient.SqlCommand("UpdateKitchenStation", connection, transaction))
                            {
                                command.CommandType = CommandType.StoredProcedure;
                                command.Parameters.AddWithValue("@StationId", model.Id);
                                command.Parameters.AddWithValue("@Name", model.Name);
                                command.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("@IsActive", model.IsActive);
                                
                                command.ExecuteNonQuery();
                                stationId = model.Id;
                            }
                            
                            // Delete existing menu item assignments
                            using (var command = new Microsoft.Data.SqlClient.SqlCommand("DeleteKitchenStationMenuItems", connection, transaction))
                            {
                                command.CommandType = CommandType.StoredProcedure;
                                command.Parameters.AddWithValue("@StationId", stationId);
                                
                                command.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Create new station
                            using (var command = new Microsoft.Data.SqlClient.SqlCommand("CreateKitchenStation", connection, transaction))
                            {
                                command.CommandType = CommandType.StoredProcedure;
                                command.Parameters.AddWithValue("@Name", model.Name);
                                command.Parameters.AddWithValue("@Description", model.Description ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("@IsActive", model.IsActive);
                                
                                var stationIdParam = new Microsoft.Data.SqlClient.SqlParameter("@StationId", SqlDbType.Int)
                                {
                                    Direction = ParameterDirection.Output
                                };
                                command.Parameters.Add(stationIdParam);
                                
                                command.ExecuteNonQuery();
                                stationId = (int)stationIdParam.Value;
                            }
                        }
                        
                        // Assign menu items to station
                        if (model.AssignedMenuItemIds != null && model.AssignedMenuItemIds.Any())
                        {
                            foreach (var menuItemId in model.AssignedMenuItemIds)
                            {
                                using (var command = new Microsoft.Data.SqlClient.SqlCommand("AssignMenuItemToKitchenStation", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;
                                    command.Parameters.AddWithValue("@StationId", stationId);
                                    command.Parameters.AddWithValue("@MenuItemId", menuItemId);
                                    command.Parameters.AddWithValue("@IsPrimary", true); // Default to primary assignment
                                    
                                    command.ExecuteNonQuery();
                                }
                            }
                        }
                        
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            
            return RedirectToAction("Stations");
        }

        // POST: Kitchen/DeleteStation/{id}
        [HttpPostAttribute]
        public IActionResult DeleteStation(int id)
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("DeleteKitchenStation", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@StationId", id);
                    
                    command.ExecuteNonQuery();
                }
            }
            
            return RedirectToAction("Stations");
        }
        
        // GET: Kitchen/MarkAllReady/{stationId?}
        public IActionResult MarkAllReady(int? stationId)
        {
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("MarkAllTicketsReady", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    
                    if (stationId.HasValue && stationId.Value > 0)
                    {
                        command.Parameters.AddWithValue("@StationId", stationId.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("@StationId", DBNull.Value);
                    }
                    
                    command.ExecuteNonQuery();
                }
            }
            
            return RedirectToAction("Dashboard", new { stationId });
        }
        
        // Private helper methods
        private List<KitchenStation> GetKitchenStations(Microsoft.Data.SqlClient.SqlConnection connection)
        {
            var stations = new List<KitchenStation>();
            
            using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetAllKitchenStations", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        stations.Add(new KitchenStation
                        {
                            Id = (int)reader["Id"],
                            Name = reader["Name"].ToString(),
                            Description = reader["Description"].ToString(),
                            IsActive = (bool)reader["IsActive"],
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            UpdatedAt = (DateTime)reader["UpdatedAt"]
                        });
                    }
                }
            }
            
            // Deduplicate stations by Name (trimmed, case-insensitive) to avoid showing duplicate rows
            // Keep the station with the most recent UpdatedAt if duplicates exist.
            var deduped = stations
                .GroupBy(s => (s.Name ?? string.Empty).Trim().ToLowerInvariant())
                .Select(g => g.OrderByDescending(s => s.UpdatedAt).First())
                .ToList();

            // Exclude BAR station from Kitchen views (BOT tickets are handled in the BOT dashboard)
            deduped = deduped
                .Where(s => !string.Equals((s.Name ?? string.Empty).Trim(), "BAR", StringComparison.OrdinalIgnoreCase)
                          && !string.Equals((s.Name ?? string.Empty).Trim(), "Bar", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return deduped;
        }
        
        private List<KitchenTicket> GetTicketsByStatus(Microsoft.Data.SqlClient.SqlConnection connection, int status, int? stationId, string stationNameFallback = null)
        {
            var tickets = new List<KitchenTicket>();

            void ReadIntoList(Microsoft.Data.SqlClient.SqlCommand cmd, List<KitchenTicket> list)
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader["TableName"].ToString();
                        if (string.IsNullOrWhiteSpace(tableName))
                        {
                            tableName = GetTableNameForOrder(connection, (int)reader["OrderId"]);
                        }
                        list.Add(new KitchenTicket
                        {
                            Id = (int)reader["Id"],
                            TicketNumber = reader["TicketNumber"].ToString(),
                            OrderId = (int)reader["OrderId"],
                            OrderNumber = reader["OrderNumber"].ToString(),
                            KitchenStationId = reader["KitchenStationId"] as int?,
                            StationName = reader["StationName"].ToString(),
                            TableName = tableName,
                            Status = (int)reader["Status"],
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            CompletedAt = reader["CompletedAt"] as DateTime?,
                            MinutesSinceCreated = (int)reader["MinutesSinceCreated"]
                        });
                    }
                }
            }

            // When filtering by station: fetch ALL tickets of this status, then filter by item-level station assignment
            // because tickets may not have a top-level StationId but items do
            if (stationId.HasValue && stationId.Value > 0)
            {
                // Get all tickets for this status
                using (var cmdAll = new Microsoft.Data.SqlClient.SqlCommand("GetKitchenTicketsByStatus", connection))
                {
                    cmdAll.CommandType = CommandType.StoredProcedure;
                    cmdAll.Parameters.AddWithValue("@Status", status);
                    cmdAll.Parameters.AddWithValue("@StationId", DBNull.Value);
                    ReadIntoList(cmdAll, tickets);
                }

                // Filter to tickets that have at least one item assigned to this station
                var ticketIdsForStation = GetTicketIdsByItemStation(connection, stationId.Value, stationNameFallback);
                if (ticketIdsForStation.Count > 0)
                {
                    tickets = tickets.Where(t => ticketIdsForStation.Contains(t.Id)).ToList();
                }
                else
                {
                    // No items found for station, return empty
                    tickets = new List<KitchenTicket>();
                }
            }
            else
            {
                // No station filter: use stored proc as-is
                using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetKitchenTicketsByStatus", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Status", status);
                    command.Parameters.AddWithValue("@StationId", DBNull.Value);
                    ReadIntoList(command, tickets);
                }
            }

            // Exclude BOT/BAR tickets from Kitchen dashboard
            tickets = (tickets ?? new List<KitchenTicket>())
                .Where(t => !(t?.TicketNumber?.StartsWith("BOT-", StringComparison.OrdinalIgnoreCase) ?? false)
                            && !string.Equals((t?.StationName ?? string.Empty).Trim(), "BAR", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return tickets;
        }

        // Best-effort fallback helper: find tickets that have at least one item for a specific station
        private HashSet<int> GetTicketIdsByItemStation(Microsoft.Data.SqlClient.SqlConnection connection, int stationId, string stationNameKey)
        {
            var set = new HashSet<int>();
            try
            {
                var sql = @"SELECT DISTINCT KitchenTicketId
                            FROM KitchenTicketItems
                            WHERE (KitchenStationId = @StationId)
                               OR (StationName IS NOT NULL AND LTRIM(RTRIM(LOWER(StationName))) = @StationNameKey)";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@StationId", stationId);
                    cmd.Parameters.AddWithValue("@StationNameKey", stationNameKey);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            set.Add(Convert.ToInt32(reader[0]));
                        }
                    }
                }
            }
            catch
            {
                // Ignore if table/columns differ in schema
            }
            return set;
        }
        
        private List<KitchenTicket> GetFilteredTickets(Microsoft.Data.SqlClient.SqlConnection connection, KitchenStationFilterViewModel filter)
        {
            var tickets = new List<KitchenTicket>();
            
            using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetFilteredKitchenTickets", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                
                if (filter.StationId.HasValue && filter.StationId.Value > 0)
                {
                    command.Parameters.AddWithValue("@StationId", filter.StationId.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@StationId", DBNull.Value);
                }
                
                if (filter.Status.HasValue)
                {
                    command.Parameters.AddWithValue("@Status", filter.Status.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@Status", DBNull.Value);
                }
                
                if (filter.DateFrom.HasValue)
                {
                    command.Parameters.AddWithValue("@DateFrom", filter.DateFrom.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@DateFrom", DBNull.Value);
                }
                
                if (filter.DateTo.HasValue)
                {
                    command.Parameters.AddWithValue("@DateTo", filter.DateTo.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@DateTo", DBNull.Value);
                }
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string tableName = reader["TableName"].ToString();
                        if (string.IsNullOrWhiteSpace(tableName))
                        {
                            tableName = GetTableNameForOrder(connection, (int)reader["OrderId"]);
                        }
                        var kt = new KitchenTicket
                        {
                            Id = (int)reader["Id"],
                            TicketNumber = reader["TicketNumber"].ToString(),
                            OrderId = (int)reader["OrderId"],
                            OrderNumber = reader["OrderNumber"].ToString(),
                            KitchenStationId = reader["KitchenStationId"] as int?,
                            StationName = reader["StationName"].ToString(),
                            TableName = tableName,
                            Status = (int)reader["Status"],
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            CompletedAt = reader["CompletedAt"] as DateTime?,
                            MinutesSinceCreated = (int)reader["MinutesSinceCreated"]
                        };

                        // Exclude BOT/BAR tickets
                        bool isBarTicket = (kt.TicketNumber?.StartsWith("BOT-", StringComparison.OrdinalIgnoreCase) ?? false)
                                           || string.Equals((kt.StationName ?? string.Empty).Trim(), "BAR", StringComparison.OrdinalIgnoreCase);
                        if (!isBarTicket)
                        {
                            tickets.Add(kt);
                        }
                    }
                }
            }
            
            return tickets;
        }

        // Attempts to retrieve a table name for an order if it was not populated in the KitchenTickets row
        private string GetTableNameForOrder(Microsoft.Data.SqlClient.SqlConnection connection, int orderId)
        {
            try
            {
                // Unified query: only return a table display value for dine‑in orders (OrderType = 0)
                // Tries multiple possible column naming conventions gracefully.
                var primarySql = @"SELECT TOP 1 
                        CASE 
                            WHEN o.OrderType = 0 THEN COALESCE(t.TableName, t.TableNumber, CONCAT('Table ', t.Id))
                            ELSE NULL 
                        END AS TableDisplay
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    WHERE o.Id = @OrderId";
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(primarySql, connection))
                {
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        var display = result.ToString();
                        if (!string.IsNullOrWhiteSpace(display)) return display;
                    }
                }

                // Attempt to aggregate merged tables (OrderTables) if present
                var mergedSql = @"SELECT STRING_AGG(t.TableName, ' + ') WITHIN GROUP (ORDER BY t.TableName)
                                   FROM OrderTables ot
                                   INNER JOIN Tables t ON ot.TableId = t.Id
                                   WHERE ot.OrderId = @OrderId";
                using (var cmdMerged = new Microsoft.Data.SqlClient.SqlCommand(mergedSql, connection))
                {
                    cmdMerged.Parameters.AddWithValue("@OrderId", orderId);
                    var merged = cmdMerged.ExecuteScalar();
                    if (merged != null && merged != DBNull.Value)
                    {
                        var mergedDisplay = merged.ToString();
                        if (!string.IsNullOrWhiteSpace(mergedDisplay)) return mergedDisplay;
                    }
                }

                // Secondary attempt: Some schemas might store a display text directly on TableTurnovers
                var turnoverSql = @"SELECT TOP 1 
                        CASE WHEN o.OrderType = 0 THEN 
                            COALESCE(tt.TableName, tt.TableNumber, CONCAT('Table ', tt.Id)) ELSE NULL END AS TableDisplay
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    WHERE o.Id = @OrderId";
                using (var cmd2 = new Microsoft.Data.SqlClient.SqlCommand(turnoverSql, connection))
                {
                    cmd2.Parameters.AddWithValue("@OrderId", orderId);
                    var result2 = cmd2.ExecuteScalar();
                    if (result2 != null && result2 != DBNull.Value)
                    {
                        var display2 = result2.ToString();
                        if (!string.IsNullOrWhiteSpace(display2)) return display2;
                    }
                }

                // Final attempt: fallback to any existing KitchenTickets row that already captured a table name for the same order
                var ktFallbackSql = @"SELECT TOP 1 TableName FROM KitchenTickets WHERE OrderId = @OrderId AND TableName IS NOT NULL AND LTRIM(RTRIM(TableName)) <> ''";
                using (var cmd3 = new Microsoft.Data.SqlClient.SqlCommand(ktFallbackSql, connection))
                {
                    cmd3.Parameters.AddWithValue("@OrderId", orderId);
                    var result3 = cmd3.ExecuteScalar();
                    if (result3 != null && result3 != DBNull.Value)
                    {
                        var display3 = result3.ToString();
                        if (!string.IsNullOrWhiteSpace(display3)) return display3;
                    }
                }
            }
            catch
            {
                // Swallow and return null – dashboard should not break if schema differs
            }
            System.Diagnostics.Debug.WriteLine($"[Kitchen] Table lookup failed for order {orderId}");
            return null;
        }
        
        private KitchenDashboardStats GetKitchenStats(Microsoft.Data.SqlClient.SqlConnection connection, int? stationId)
        {
            var stats = new KitchenDashboardStats();
            
            using (var command = new Microsoft.Data.SqlClient.SqlCommand("GetKitchenDashboardStats", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                
                if (stationId.HasValue && stationId.Value > 0)
                {
                    command.Parameters.AddWithValue("@StationId", stationId.Value);
                }
                else
                {
                    command.Parameters.AddWithValue("@StationId", DBNull.Value);
                }
                
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        stats.NewTicketsCount = reader["NewTicketsCount"] != DBNull.Value ? Convert.ToInt32(reader["NewTicketsCount"]) : 0;
                        stats.InProgressTicketsCount = reader["InProgressTicketsCount"] != DBNull.Value ? Convert.ToInt32(reader["InProgressTicketsCount"]) : 0;
                        stats.ReadyTicketsCount = reader["ReadyTicketsCount"] != DBNull.Value ? Convert.ToInt32(reader["ReadyTicketsCount"]) : 0;
                        stats.PendingItemsCount = reader["PendingItemsCount"] != DBNull.Value ? Convert.ToInt32(reader["PendingItemsCount"]) : 0;
                        stats.ReadyItemsCount = reader["ReadyItemsCount"] != DBNull.Value ? Convert.ToInt32(reader["ReadyItemsCount"]) : 0;
                        stats.AvgPrepTimeMinutes = reader["AvgPrepTimeMinutes"] != DBNull.Value ? Convert.ToDouble(reader["AvgPrepTimeMinutes"]) : 0.0;
                    }
                }
            }
            
            return stats;
        }
    }
}
