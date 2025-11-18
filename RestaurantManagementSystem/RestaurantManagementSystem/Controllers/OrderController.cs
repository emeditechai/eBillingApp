using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models.Authorization;
using System.Linq;
using System.Security.Claims;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize]
    public partial class OrderController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly RestaurantManagementSystem.Services.UrlEncryptionService _encryptionService;
        
        public OrderController(IConfiguration configuration, RestaurantManagementSystem.Services.UrlEncryptionService encryptionService)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
            _encryptionService = encryptionService;
        }
        
        // Order Dashboard
        [RequirePermission("NAV_ORDERS_DASH", PermissionAction.View)]
        public IActionResult Dashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = GetOrderDashboard(fromDate, toDate);
            return View(model);
        }
        
        // Create New Order
        [RequirePermission("NAV_ORDERS_CREATE", PermissionAction.Add)]
        public IActionResult Create(int? tableId = null)
        {
            var model = new CreateOrderViewModel();
            
            if (tableId.HasValue)
            {
                model.SelectedTableId = tableId.Value;
                model.OrderType = 0; // 0 = Dine-In
            }
            
            // Get available tables
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get available tables
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, TableName, Capacity, Status
                    FROM Tables
                    WHERE Status = 0
                    ORDER BY TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT tt.Id, t.Id, t.TableName, tt.GuestName, tt.PartySize, tt.Status
                    FROM TableTurnovers tt
                    INNER JOIN Tables t ON tt.TableId = t.Id
                    WHERE tt.Status < 5 -- Not departed
                    ORDER BY t.TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
            
            // If user selected an available table (negative sentinel) but form invalid, keep selection in UI
            if (model.TableTurnoverId.HasValue && model.TableTurnoverId < 0)
            {
                // nothing extra to do; dropdown will still show negative value; view logic maintains grouping
            }
            
            return View(model);
        }
        
    [HttpPostAttribute]
    [ValidateAntiForgeryTokenAttribute]
    [RequirePermission("NAV_ORDERS_CREATE", PermissionAction.Add)]
    public async Task<IActionResult> Create(CreateOrderViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_CreateOrder", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;
                                    
                                    // If a table was selected from the TableService Dashboard
                                    if (model.SelectedTableId.HasValue)
                                    {
                                        // Need to seat guests at this table first
                                        int turnoverId = SeatGuestsAtTable(model.SelectedTableId.Value, "Walk-in", 2, connection, transaction); // Default 2 guests for walk-ins
                                        model.TableTurnoverId = turnoverId;
                                    }
                                    else if (model.TableTurnoverId.HasValue && model.TableTurnoverId < 0)
                                    {
                                        // User selected an available (unseated) table from dropdown (negative sentinel = -TableId)
                                        int availableTableId = Math.Abs(model.TableTurnoverId.Value);
                                        int turnoverId = SeatGuestsAtTable(availableTableId, model.CustomerName ?? "Walk-in", 2, connection, transaction);
                                        model.TableTurnoverId = turnoverId; // Replace sentinel with real turnover id
                                    }
                                    
                                    command.Parameters.AddWithValue("@TableTurnoverId", model.TableTurnoverId ?? (object)DBNull.Value);
                                    command.Parameters.AddWithValue("@OrderType", model.OrderType);
                                    command.Parameters.AddWithValue("@UserId", GetCurrentUserId());
                                    // Pass through the authenticated user id and name for auditing who created the order
                                    command.Parameters.AddWithValue("@OrderByUserId", GetCurrentUserId());
                                    command.Parameters.AddWithValue("@OrderByUserName", GetCurrentUserName());
                                    command.Parameters.AddWithValue("@CustomerName", string.IsNullOrEmpty(model.CustomerName) ? (object)DBNull.Value : model.CustomerName);
                                    command.Parameters.AddWithValue("@CustomerPhone", string.IsNullOrEmpty(model.CustomerPhone) ? (object)DBNull.Value : model.CustomerPhone);
                                    command.Parameters.AddWithValue("@SpecialInstructions", string.IsNullOrEmpty(model.SpecialInstructions) ? (object)DBNull.Value : model.SpecialInstructions);
                                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                                            // Patch: Ensure Orders.CashierId is populated for Day Closing system amount calculations
                                            // Root cause of zero SystemAmount: orders were created with NULL CashierId so cash payments
                                            // couldn't be attributed to any cashier in UpdateCashierSystemAmountsAsync aggregation.
                                            try
                                            {
                                                using (var setCashierCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CashierId')
                                                    BEGIN
                                                        UPDATE dbo.Orders
                                                        SET CashierId = @CashierId
                                                        WHERE Id = @OrderId AND CashierId IS NULL;
                                                    END", connection, transaction))
                                                {
                                                    setCashierCmd.Parameters.AddWithValue("@CashierId", GetCurrentUserId());
                                                    setCashierCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                    setCashierCmd.ExecuteNonQuery();
                                                }
                                            }
                                            catch { /* Non-fatal: avoid blocking order creation if column missing */ }

                                            // Set OrderKitchenType to "Foods" for orders created from Orders navigation
                                            try
                                            {
                                                using (var setKitchenTypeCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                                                    BEGIN
                                                        UPDATE dbo.Orders SET OrderKitchenType = 'Foods' WHERE Id = @OrderId
                                                    END", connection, transaction))
                                                {
                                                    setKitchenTypeCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                    setKitchenTypeCmd.ExecuteNonQuery();
                                                }
                                            }
                                            catch { /* non-fatal if column doesn't exist */ }

                                            using (Microsoft.Data.SqlClient.SqlCommand kitchenCommand = new Microsoft.Data.SqlClient.SqlCommand("UpdateKitchenTicketsForOrder", connection, transaction))
                                            {
                                                kitchenCommand.CommandType = CommandType.StoredProcedure;
                                                kitchenCommand.Parameters.AddWithValue("@OrderId", orderId);
                                                kitchenCommand.ExecuteNonQuery();
                                            }

                                            // Add primary table to OrderTables (for both single and merged orders)
                                            int? primaryTableId = null;
                                            
                                            if (model.SelectedTableId.HasValue)
                                            {
                                                primaryTableId = model.SelectedTableId.Value;
                                            }
                                            else if (model.TableTurnoverId.HasValue)
                                            {
                                                // Get table ID from TableTurnover
                                                using (var getTableCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT TableId FROM TableTurnovers WHERE Id = @TurnoverId", connection, transaction))
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
                                                using (var insertPrimary = new Microsoft.Data.SqlClient.SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM OrderTables WHERE OrderId=@OrderId AND TableId=@TableId)
                                                    INSERT INTO OrderTables (OrderId, TableId, CreatedAt) VALUES (@OrderId, @TableId, GETDATE());", connection, transaction))
                                                {
                                                    insertPrimary.Parameters.AddWithValue("@OrderId", orderId);
                                                    insertPrimary.Parameters.AddWithValue("@TableId", primaryTableId.Value);
                                                    insertPrimary.ExecuteNonQuery();
                                                }
                                            }
                                            
                                            // Persist merged tables (additional tables beyond the primary)
                                            if (model.SelectedTableIds != null && model.SelectedTableIds.Count > 0)
                                            {
                                                foreach (var mergedTableId in model.SelectedTableIds.Distinct())
                                                {
                                                    // Skip if this table is already the primary selected table
                                                    if (model.SelectedTableId.HasValue && model.SelectedTableId.Value == mergedTableId)
                                                        continue;
                                                    using (var insertMerge = new Microsoft.Data.SqlClient.SqlCommand(@"IF NOT EXISTS (SELECT 1 FROM OrderTables WHERE OrderId=@OrderId AND TableId=@TableId)
                                                        INSERT INTO OrderTables (OrderId, TableId, CreatedAt) VALUES (@OrderId, @TableId, GETDATE());", connection, transaction))
                                                    {
                                                        insertMerge.Parameters.AddWithValue("@OrderId", orderId);
                                                        insertMerge.Parameters.AddWithValue("@TableId", mergedTableId);
                                                        insertMerge.ExecuteNonQuery();
                                                    }
                                                }
                                            }
                                            
                                            // Calculate and persist GST fields for the newly created order
                                            UpdateOrderFinancials(orderId, connection, transaction);
                                            
                                            transaction.Commit();
                                            
                                            // Log audit trail
                                            try
                                            {
                                                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                                                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                                                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                                                var orderTypeText = model.OrderType switch { 0 => "Dine In", 1 => "Takeout", 2 => "Delivery", 3 => "Online", _ => "Unknown" };
                                                var additionalInfo = $"Order Type: {orderTypeText}";
                                                if (model.OrderType == 0 && primaryTableId.HasValue)
                                                {
                                                    additionalInfo += $", Table ID: {primaryTableId.Value}";
                                                }
                                                
                                                await AuditTrailController.LogAuditAsync(_connectionString, orderId, orderNumber, "Create", "Order", 
                                                    orderId, null, null, $"Order created - {orderTypeText}", userId, userName, ipAddress, null, additionalInfo);
                                            }
                                            catch { /* Audit logging should not break the main flow */ }
                                            
                                            TempData["SuccessMessage"] = $"Order {orderNumber} created successfully.";
                                            TempData["IsBarOrder"] = false; // Explicitly mark as non-bar order (from Orders navigation)
                                            return RedirectToAction("Details", new { id = orderId });
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
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            // If we get here, something went wrong - repopulate the model
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get available tables
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, TableName, Capacity, Status
                    FROM Tables
                    WHERE Status = 0
                    ORDER BY TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT tt.Id, t.Id, t.TableName, tt.GuestName, tt.PartySize, tt.Status
                    FROM TableTurnovers tt
                    INNER JOIN Tables t ON tt.TableId = t.Id
                    WHERE tt.Status < 5 -- Not departed
                    ORDER BY t.TableName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
        
        // Order Details
        public IActionResult Details(int? id = null, bool? fromBar = null, string token = null)
        {
            // Support both encrypted token and plain parameters for backward compatibility
            int actualId = 0;
            bool actualFromBar = false;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    // Decrypt the token to get parameters
                    var parameters = _encryptionService.DecryptParameters(token);
                    
                    if (parameters.ContainsKey("id") && int.TryParse(parameters["id"], out int decryptedId))
                    {
                        actualId = decryptedId;
                    }
                    
                    if (parameters.ContainsKey("fromBar") && bool.TryParse(parameters["fromBar"], out bool decryptedFromBar))
                    {
                        actualFromBar = decryptedFromBar;
                    }
                }
                catch (Exception ex)
                {
                    // Log error if needed
                    TempData["ErrorMessage"] = "Invalid or expired order link. Please try again.";
                    return RedirectToAction("Dashboard", "Order");
                }
            }
            else if (id.HasValue)
            {
                // Use plain parameters for backward compatibility
                actualId = id.Value;
                actualFromBar = fromBar ?? false;
            }
            else
            {
                return BadRequest("Order ID or token is required");
            }

            var model = GetOrderDetails(actualId);
            if (model == null)
            {
                return NotFound();
            }

            // Determine BAR context: explicit parameter > TempData > DB detection (KitchenTickets BAR/BOT)
            bool isBarContext = false;
            if (actualFromBar)
            {
                isBarContext = true;
            }
            else if (TempData["IsBarOrder"] as bool? == true)
            {
                isBarContext = true;
            }
            else
            {
                // Fallback: detect if the order has any BAR/BOT tickets
                try
                {
                    isBarContext = IsBarOrder(actualId);
                }
                catch
                {
                    isBarContext = false; // default to non-bar if detection fails
                }
            }

            // Store bar order flag in ViewBag for the view
            ViewBag.IsBarOrder = isBarContext;
            
            // Populate Menu Item Groups and items (default group = 1)
            model.AvailableMenuItems = new List<MenuItem>();
            model.MenuItemGroups = new List<MenuItemGroup>();
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                // Load active groups
                using (var gcmd = new Microsoft.Data.SqlClient.SqlCommand(@"IF OBJECT_ID('dbo.menuitemgroup','U') IS NOT NULL
                    SELECT ID, itemgroup, is_active, CAST(GST_Perc AS decimal(12,2)) FROM dbo.menuitemgroup WHERE is_active = 1 ORDER BY itemgroup
                    ELSE SELECT CAST(NULL AS int) AS ID, CAST(NULL AS varchar(20)) AS itemgroup, CAST(1 AS bit) AS is_active, CAST(NULL AS decimal(12,2)) AS GST_Perc WHERE 1=0", connection))
                {
                    using (var gr = gcmd.ExecuteReader())
                    {
                        while (gr.Read())
                        {
                            model.MenuItemGroups.Add(new MenuItemGroup
                            {
                                ID = gr.GetInt32(0),
                                ItemGroup = gr.IsDBNull(1) ? string.Empty : gr.GetString(1),
                                IsActive = gr.IsDBNull(2) ? true : gr.GetBoolean(2),
                                GST_Perc = gr.IsDBNull(3) ? (decimal?)null : Convert.ToDecimal(gr[3])
                            });
                        }
                    }
                }

                // Determine selected group based on order source
                if (model.MenuItemGroups != null && model.MenuItemGroups.Count > 0)
                {
                    if (ViewBag.IsBarOrder)
                    {
                        // For bar orders, try to select "Bar" group first
                        var barGroup = model.MenuItemGroups.FirstOrDefault(g => g.ItemGroup.Equals("Bar", StringComparison.OrdinalIgnoreCase));
                        model.SelectedMenuItemGroupId = barGroup?.ID ?? model.MenuItemGroups.First().ID;
                    }
                    else
                    {
                        // For regular orders, try to select "Foods" group first, then fallback to group 1 or first active group
                        var foodGroup = model.MenuItemGroups.FirstOrDefault(g => g.ItemGroup.Equals("Foods", StringComparison.OrdinalIgnoreCase));
                        if (foodGroup != null)
                        {
                            model.SelectedMenuItemGroupId = foodGroup.ID;
                        }
                        else
                        {
                            model.SelectedMenuItemGroupId = model.MenuItemGroups.Any(g => g.ID == 1) ? 1 : model.MenuItemGroups.First().ID;
                        }
                    }
                }
                else
                {
                    model.SelectedMenuItemGroupId = 1; // safe default even if table missing
                }

                // Load available menu items filtered by group if column exists; else load all
                var sql = @"DECLARE @hasCol bit = 0;
                             IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MenuItems') AND name = 'menuitemgroupID')
                                SET @hasCol = 1;
                             IF (@hasCol = 1)
                             BEGIN
                                SELECT Id, PLUCode, Name, Description, Price
                                FROM dbo.MenuItems
                                WHERE IsAvailable = 1 AND (menuitemgroupID = @GroupId)
                                ORDER BY Name
                             END
                             ELSE
                             BEGIN
                                SELECT Id, PLUCode, Name, Description, Price
                                FROM dbo.MenuItems
                                WHERE IsAvailable = 1
                                ORDER BY Name
                             END";
                using (var icmd = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                {
                    icmd.Parameters.AddWithValue("@GroupId", model.SelectedMenuItemGroupId);
                    using (var reader = icmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailableMenuItems.Add(new MenuItem
                            {
                                Id = reader.GetInt32(0),
                                PLUCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Name = reader.GetString(2),
                                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Price = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }
            return View(model);
        }

        // Determine if an order should be treated as a Bar (BOT) order for navigation/context in Order Details
        private bool IsBarOrder(int orderId)
        {
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    conn.Open();
                    // 1) Prefer explicit flag on Orders if available
                    try
                    {
                        using (var orderFlagCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                            BEGIN
                                SELECT TOP 1 1 FROM dbo.Orders WHERE Id = @OrderId AND OrderKitchenType = 'Bar'
                            END
                            ELSE
                            BEGIN
                                SELECT CAST(NULL AS INT)
                            END", conn))
                        {
                            orderFlagCmd.Parameters.AddWithValue("@OrderId", orderId);
                            var flag = orderFlagCmd.ExecuteScalar();
                            if (flag != null && flag != DBNull.Value)
                                return true;
                        }
                    }
                    catch { /* ignore and fallback to tickets */ }
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT TOP 1 1 
                            FROM KitchenTickets 
                            WHERE OrderId = @OrderId 
                              AND (KitchenStation = 'BAR' OR TicketNumber LIKE 'BOT-%')", conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return true;
                    }
                }
            }
            catch
            {
                // ignore and return false below
            }
            return false;
        }

        [HttpGet]
        public JsonResult GetMenuItemsByGroup(int groupId)
        {
            var items = new List<object>();
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    var sql = @"DECLARE @hasCol bit = 0;
                                 IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MenuItems') AND name = 'menuitemgroupID')
                                    SET @hasCol = 1;
                                 IF (@hasCol = 1)
                                 BEGIN
                                    SELECT Id, PLUCode, Name, Price
                                    FROM dbo.MenuItems
                                    WHERE IsAvailable = 1 AND (menuitemgroupID = @GroupId)
                                    ORDER BY Name
                                 END
                                 ELSE
                                 BEGIN
                                    SELECT Id, PLUCode, Name, Price
                                    FROM dbo.MenuItems
                                    WHERE IsAvailable = 1
                                    ORDER BY Name
                                 END";
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@GroupId", groupId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                items.Add(new
                                {
                                    Id = r.GetInt32(0),
                                    PLUCode = r.IsDBNull(1) ? null : r.GetString(1),
                                    Name = r.GetString(2),
                                    Price = r.GetDecimal(3)
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return Json(items);
        }

        // KOT Bill print view
        public IActionResult KOTBill(int id)
        {
            var model = GetOrderDetails(id);
            if (model == null)
            {
                return NotFound();
            }
            // Only allow KOT print if there are kitchen tickets (items fired to kitchen)
            if (model.KitchenTickets == null || !model.KitchenTickets.Any())
            {
                TempData["ErrorMessage"] = "No items have been fired to kitchen for this order. KOT not available.";
                return RedirectToAction("Details", new { id = id });
            }
            return View("KOTBill", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult QuickAddMenuItem(int orderId, string menuItemNameOrId, int quantity)
        {
            if (quantity < 1) quantity = 1;
            int menuItemId = 0;
            // Try to parse as ID, otherwise resolve by name
            if (!int.TryParse(menuItemNameOrId, out menuItemId))
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 1 Id FROM MenuItems WHERE Name = @Name OR PLUCode = @Name", connection))
                    {
                        command.Parameters.AddWithValue("@Name", menuItemNameOrId);
                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            menuItemId = Convert.ToInt32(result);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Menu item not found.";
                            return RedirectToAction("Details", new { id = orderId });
                        }
                    }
                }
            }
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"INSERT INTO OrderItems (OrderId, MenuItemId, Quantity, UnitPrice, Subtotal, Status, CreatedAt) SELECT @OrderId, Id, @Quantity, Price, Price * @Quantity, 0, GETDATE() FROM MenuItems WHERE Id = @MenuItemId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    command.Parameters.AddWithValue("@MenuItemId", menuItemId);
                    command.Parameters.AddWithValue("@Quantity", quantity);
                    command.ExecuteNonQuery();
                }
            }
            TempData["SuccessMessage"] = "Menu item added to order.";
            return RedirectToAction("Details", new { id = orderId });
        }
        
        // Add Item to Order
        public IActionResult AddItem(int orderId, int? menuItemId = null)
        {
            var model = new AddOrderItemViewModel
            {
                OrderId = orderId
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT o.OrderNumber, ISNULL(t.TableName, 'N/A') AS TableNumber
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    WHERE o.Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderNumber = reader.GetString(0);
                            model.TableNumber = reader.GetString(1);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
                
                // Get available courses
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name
                    FROM CourseTypes
                    ORDER BY DisplayOrder", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailableCourses.Add(new SelectListItem
                            {
                                Value = reader.GetInt32(0).ToString(),
                                Text = reader.GetString(1)
                            });
                        }
                    }
                }
                
                // Get current order items for the order summary
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT oi.Id, oi.MenuItemId, oi.Quantity, oi.UnitPrice, oi.Subtotal, 
                           oi.SpecialInstructions, mi.Name
                    FROM OrderItems oi
                    INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                    WHERE oi.OrderId = @OrderId AND oi.Status < 5 -- Not cancelled
                    ORDER BY oi.CreatedAt DESC", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.CurrentOrderItems.Add(new OrderItemViewModel
                            {
                                Id = reader.GetInt32(0),
                                MenuItemId = reader.GetInt32(1),
                                Quantity = reader.GetInt32(2),
                                UnitPrice = reader.GetDecimal(3),
                                Subtotal = reader.GetDecimal(4),
                                SpecialInstructions = reader.IsDBNull(5) ? null : reader.GetString(5),
                                MenuItemName = reader.GetString(6),
                                TotalPrice = reader.GetDecimal(4) // Subtotal already includes quantity
                            });
                        }
                    }
                }
                
                // Calculate current order total
                model.CurrentOrderTotal = model.CurrentOrderItems.Sum(i => i.Subtotal);
                
                // If a specific menu item is selected, get its details and modifiers
                if (menuItemId.HasValue)
                {
                    model.MenuItemId = menuItemId.Value;
                    
                    // Get menu item details
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT Id, Name, Description, Price, CategoryId, ImagePath
                        FROM MenuItems
                        WHERE Id = @MenuItemId AND IsAvailable = 1", connection))
                    {
                        command.Parameters.AddWithValue("@MenuItemId", menuItemId.Value);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                model.MenuItem = new MenuItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    CategoryId = reader.GetInt32(4),
                                    ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5)
                                };
                                
                                // Set properties for the view
                                model.MenuItemName = model.MenuItem.Name;
                                model.MenuItemDescription = model.MenuItem.Description;
                                model.MenuItemPrice = model.MenuItem.Price;
                                model.MenuItemImagePath = model.MenuItem.ImagePath;
                            }
                            else
                            {
                                return NotFound();
                            }
                        }
                    }
                    
                    // Get available modifiers for the menu item
                    // Check if either table version exists
                    bool tableExists = false;
                    string modifiersTableName = "";
                    string modifiersQuery;
                    
                    try
                    {
                        using (Microsoft.Data.SqlClient.SqlConnection checkCon = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                        {
                            checkCon.Open();
                            
                            // Try with underscore first
                            using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CASE WHEN OBJECT_ID('MenuItem_Modifiers', 'U') IS NOT NULL THEN 1 ELSE 0 END", checkCon))
                            {
                                if (Convert.ToBoolean(cmd.ExecuteScalar()))
                                {
                                    tableExists = true;
                                    modifiersTableName = "MenuItem_Modifiers";
                                }
                            }
                            
                            // If not found, try without underscore
                            if (!tableExists)
                            {
                                using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CASE WHEN OBJECT_ID('MenuItemModifiers', 'U') IS NOT NULL THEN 1 ELSE 0 END", checkCon))
                                {
                                    if (Convert.ToBoolean(cmd.ExecuteScalar()))
                                    {
                                        tableExists = true;
                                        modifiersTableName = "MenuItemModifiers";
                                    }
                                }
                            }
                        }
                    
                        if (tableExists)
                        {
                            // Check if the table has PriceAdjustment and IsDefault columns
                            bool hasPriceAdjustment = ColumnExistsInTable(modifiersTableName, "PriceAdjustment");
                            bool hasIsDefault = ColumnExistsInTable(modifiersTableName, "IsDefault");
                            
                            // Build the query based on the available columns
                            if (hasPriceAdjustment && hasIsDefault)
                            {
                                modifiersQuery = $@"
                                    SELECT m.Id, m.Name, mm.PriceAdjustment AS Price, mm.IsDefault
                                    FROM Modifiers m
                                    INNER JOIN {modifiersTableName} mm ON m.Id = mm.ModifierId
                                    WHERE mm.MenuItemId = @MenuItemId
                                    ORDER BY m.Name";
                            }
                            else
                            {
                                modifiersQuery = $@"
                                    SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault
                                    FROM Modifiers m
                                    INNER JOIN {modifiersTableName} mm ON m.Id = mm.ModifierId
                                    WHERE mm.MenuItemId = @MenuItemId
                                    ORDER BY m.Name";
                            }
                        }
                        else
                        {
                            // If no table exists, just get modifiers without relationship
                            modifiersQuery = @"
                                SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault
                                FROM Modifiers m
                                ORDER BY m.Name";
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error if possible
                        
                        
                        // Fallback to a simple query that doesn't require the relationship table
                        modifiersQuery = @"
                            SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault
                            FROM Modifiers m
                            ORDER BY m.Name";
                    }
                        
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(modifiersQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MenuItemId", menuItemId.Value);

                        try
                        {
                            using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var modifier = new ModifierViewModel
                                    {
                                        Id = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Price = reader.GetDecimal(2),
                                        IsDefault = reader.GetBoolean(3),
                                        IsSelected = false, // Changed to false by default
                                        ModifierId = reader.GetInt32(0)
                                    };

                                    model.AvailableModifiers.Add(modifier);

                                    if (modifier.IsDefault)
                                    {
                                        model.SelectedModifiers.Add(modifier.Id);
                                    }
                                }
                            }
                        }
                        catch (SqlException)
                        {
                            // Fallback if relationship table still causes errors
                            using (Microsoft.Data.SqlClient.SqlCommand fallback = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault FROM Modifiers m ORDER BY m.Name", connection))
                            using (Microsoft.Data.SqlClient.SqlDataReader reader = fallback.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    model.AvailableModifiers.Add(new ModifierViewModel
                                    {
                                        Id = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Price = reader.GetDecimal(2),
                                        IsDefault = false,
                                        IsSelected = false,
                                        ModifierId = reader.GetInt32(0)
                                    });
                                }
                            }
                        }
                    }
                    
                    // Get allergens for the menu item (only if the relationship table exists)
                    string allergensTableName = GetMenuItemRelationshipTableName("Allergens");
                    if (TableExists(allergensTableName))
                    {
                        string allergensQuery = $@"
                            SELECT a.Name
                            FROM Allergens a
                            INNER JOIN {allergensTableName} ma ON a.Id = ma.AllergenId
                            WHERE ma.MenuItemId = @MenuItemId
                            ORDER BY a.Name";

                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(allergensQuery, connection))
                        {
                            command.Parameters.AddWithValue("@MenuItemId", menuItemId.Value);

                            using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    model.CommonAllergens.Add(reader.GetString(0));
                                }
                            }
                        }
                    }
                }
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> AddItem(AddOrderItemViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();

                        using (Microsoft.Data.SqlClient.SqlTransaction transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // Convert selected modifiers to comma-separated string
                                string modifierIds = model.SelectedModifiers != null && model.SelectedModifiers.Any()
                                    ? string.Join(",", model.SelectedModifiers)
                                    : null;

                                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_AddOrderItem", connection, transaction))
                                {
                                    command.CommandType = CommandType.StoredProcedure;

                                    command.Parameters.AddWithValue("@OrderId", model.OrderId);
                                    command.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                                    command.Parameters.AddWithValue("@Quantity", model.Quantity);
                                    command.Parameters.AddWithValue("@SpecialInstructions", string.IsNullOrEmpty(model.SpecialInstructions) ? (object)DBNull.Value : model.SpecialInstructions);
                                    command.Parameters.AddWithValue("@CourseId", model.CourseId.HasValue ? model.CourseId.Value : (object)DBNull.Value);
                                    command.Parameters.AddWithValue("@ModifierIds", modifierIds ?? (object)DBNull.Value);

                                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                                    {
                                        int orderItemId = 0;
                                        string message = "Failed to add item to order.";
                                        if (reader.Read())
                                        {
                                            orderItemId = reader.GetInt32(0);
                                            message = reader.GetString(1);
                                        }
                                        reader.Close();

                                        if (orderItemId > 0)
                                        {
                                            // Set/Update Orders.OrderKitchenType based on the added menu item's group (Bar/Foods), if the column exists
                                            using (var setTypeCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                DECLARE @kitchenType varchar(20) = NULL;
                                                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MenuItems') AND name = 'menuitemgroupID')
                                                BEGIN
                                                    SELECT @kitchenType = CASE WHEN LOWER(mg.itemgroup) = 'bar' THEN 'Bar' ELSE 'Foods' END
                                                    FROM dbo.MenuItems mi
                                                    LEFT JOIN dbo.menuitemgroup mg ON mi.menuitemgroupID = mg.ID
                                                    WHERE mi.Id = @MenuItemId;
                                                END
                                                ELSE
                                                BEGIN
                                                    SET @kitchenType = 'Foods';
                                                END

                                                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                                                BEGIN
                                                    IF (@kitchenType = 'Bar')
                                                    BEGIN
                                                        UPDATE o SET o.OrderKitchenType = 'Bar'
                                                        FROM dbo.Orders o
                                                        WHERE o.Id = @OrderId AND ISNULL(o.OrderKitchenType,'') <> 'Bar';
                                                    END
                                                    ELSE
                                                    BEGIN
                                                        UPDATE o SET o.OrderKitchenType = 'Foods'
                                                        FROM dbo.Orders o
                                                        WHERE o.Id = @OrderId AND ISNULL(o.OrderKitchenType,'') = '';
                                                    END
                                                END
                                            ", connection, transaction))
                                            {
                                                setTypeCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                setTypeCmd.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                                                setTypeCmd.ExecuteNonQuery();
                                            }

                                            // Create or update kitchen ticket after adding an item
                                            using (Microsoft.Data.SqlClient.SqlCommand kitchenCommand = new Microsoft.Data.SqlClient.SqlCommand("UpdateKitchenTicketsForOrder", connection, transaction))
                                            {
                                                kitchenCommand.CommandType = CommandType.StoredProcedure;
                                                kitchenCommand.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                kitchenCommand.ExecuteNonQuery();
                                            }

                                            // Recalculate and persist GST fields after item addition
                                            UpdateOrderFinancials(model.OrderId, connection, transaction);

                                            // All good, commit
                                            transaction.Commit();
                                            
                                            // Log audit trail
                                            try
                                            {
                                                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                                                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                                                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                                                
                                                // Get order number and menu item name
                                                string orderNumber = string.Empty, menuItemName = string.Empty;
                                                using (var auditCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT o.OrderNumber, mi.ItemName FROM Orders o LEFT JOIN MenuItems mi ON mi.Id = @MenuItemId WHERE o.Id = @OrderId", connection))
                                                {
                                                    auditCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                    auditCmd.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                                                    using (var auditReader = auditCmd.ExecuteReader())
                                                    {
                                                        if (auditReader.Read())
                                                        {
                                                            orderNumber = auditReader.IsDBNull(0) ? "" : auditReader.GetString(0);
                                                            menuItemName = auditReader.IsDBNull(1) ? "" : auditReader.GetString(1);
                                                        }
                                                    }
                                                }
                                                
                                                await AuditTrailController.LogAuditAsync(_connectionString, model.OrderId, orderNumber, "Add", "OrderItem",
                                                    null, null, $"{menuItemName} x{model.Quantity}", $"Added item to order", userId, userName, ipAddress, null, $"Quantity: {model.Quantity}");
                                            }
                                            catch { /* Audit logging should not break the main flow */ }
                                            
                                            TempData["SuccessMessage"] = "Item added to order successfully.";
                                            return RedirectToAction("Details", new { id = model.OrderId });
                                        }
                                        else
                                        {
                                            // Validation message from SP
                                            transaction.Rollback();
                                            ModelState.AddModelError("", message);
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Ensure rollback on any error
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            // If we get here, something went wrong - repopulate the model
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get available courses
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name
                    FROM CourseTypes
                    ORDER BY DisplayOrder", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailableCourses.Add(new SelectListItem
                            {
                                Value = reader.GetInt32(0).ToString(),
                                Text = reader.GetString(1)
                            });
                        }
                    }
                }
                
                // Get menu item details
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name, Description, Price, CategoryId
                    FROM MenuItems
                    WHERE Id = @MenuItemId AND IsAvailable = 1", connection))
                {
                    command.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.MenuItem = new MenuItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Price = reader.GetDecimal(3),
                                CategoryId = reader.GetInt32(4)
                            };
                        }
                    }
                }
                
                // Get available modifiers for the menu item (robust to table variations)
                string modifiersTableNamePost = string.Empty;
                bool modsTableExists = false;
                try
                {
                    using (Microsoft.Data.SqlClient.SqlConnection checkCon = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        checkCon.Open();
                        using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CASE WHEN OBJECT_ID('MenuItem_Modifiers', 'U') IS NOT NULL THEN 1 ELSE 0 END", checkCon))
                        {
                            if (Convert.ToBoolean(cmd.ExecuteScalar()))
                            {
                                modsTableExists = true;
                                modifiersTableNamePost = "MenuItem_Modifiers";
                            }
                        }
                        if (!modsTableExists)
                        {
                            using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT CASE WHEN OBJECT_ID('MenuItemModifiers', 'U') IS NOT NULL THEN 1 ELSE 0 END", checkCon))
                            {
                                if (Convert.ToBoolean(cmd.ExecuteScalar()))
                                {
                                    modsTableExists = true;
                                    modifiersTableNamePost = "MenuItemModifiers";
                                }
                            }
                        }
                    }
                }
                catch { modsTableExists = false; }

                string modifiersQueryPost;
                if (modsTableExists)
                {
                    bool hasPriceAdjustment = ColumnExistsInTable(modifiersTableNamePost, "PriceAdjustment");
                    bool hasIsDefault = ColumnExistsInTable(modifiersTableNamePost, "IsDefault");
                    modifiersQueryPost = (hasPriceAdjustment && hasIsDefault)
                        ? $@"SELECT m.Id, m.Name, mm.PriceAdjustment AS Price, mm.IsDefault
                             FROM Modifiers m
                             INNER JOIN {modifiersTableNamePost} mm ON m.Id = mm.ModifierId
                             WHERE mm.MenuItemId = @MenuItemId
                             ORDER BY m.Name"
                        : $@"SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault
                             FROM Modifiers m
                             INNER JOIN {modifiersTableNamePost} mm ON m.Id = mm.ModifierId
                             WHERE mm.MenuItemId = @MenuItemId
                             ORDER BY m.Name";
                }
                else
                {
                    modifiersQueryPost = @"SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault FROM Modifiers m ORDER BY m.Name";
                }

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(modifiersQueryPost, connection))
                {
                    command.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                    try
                    {
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                model.AvailableModifiers.Add(new ModifierViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    IsDefault = reader.GetBoolean(3),
                                    IsSelected = model.SelectedModifiers?.Contains(reader.GetInt32(0)) ?? false
                                });
                            }
                        }
                    }
                    catch (SqlException)
                    {
                        using (Microsoft.Data.SqlClient.SqlCommand fallback = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT m.Id, m.Name, 0 AS Price, 0 AS IsDefault FROM Modifiers m ORDER BY m.Name", connection))
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = fallback.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                model.AvailableModifiers.Add(new ModifierViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    IsDefault = false,
                                    IsSelected = model.SelectedModifiers?.Contains(reader.GetInt32(0)) ?? false
                                });
                            }
                        }
                    }
                }
            }
            return View(model);
        }
        
        // Fire Items to Kitchen
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public async Task<IActionResult> FireItems(FireOrderItemsViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        // Convert selected items to comma-separated string
                        string orderItemIds = null;
                        
                        if (!model.FireAll && model.SelectedItems != null && model.SelectedItems.Any())
                        {
                            orderItemIds = string.Join(",", model.SelectedItems);
                        }

                        // Check if KitchenTicketItems table exists with or without underscore
                        bool useUnderscoreVersion = false;
                        string kitchenTicketItemsTableName = "KitchenTicketItems";
                        
                        if (TableExists("Kitchen_TicketItems"))
                        {
                            kitchenTicketItemsTableName = "Kitchen_TicketItems";
                            useUnderscoreVersion = true;
                        }
                        
                        // First, let's create the kitchen ticket
                        // We'll use our own SQL instead of calling the stored procedure directly
                        // to handle table name differences
                        int kitchenTicketId = 0;
                        
                        try
                        {
                            // Start a transaction
                            using (Microsoft.Data.SqlClient.SqlTransaction transaction = connection.BeginTransaction())
                            {
                                try
                                {
                                    // Get items to process
                                    List<int> itemsToProcess = new List<int>();
                                    
                                    if (!model.FireAll && model.SelectedItems != null && model.SelectedItems.Any())
                                    {
                                        itemsToProcess = model.SelectedItems.ToList();
                                    }
                                    else
                                    {
                                        // Get all unfired items for this order
                                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT Id FROM OrderItems 
                                            WHERE OrderId = @OrderId AND Status = 0", connection, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                            using (var reader = cmd.ExecuteReader())
                                            {
                                                while (reader.Read())
                                                {
                                                    itemsToProcess.Add(reader.GetInt32(0));
                                                }
                                            }
                                        }
                                    }

                                    // Generate unique ticket number based on order type
                                    // BOT for bar orders, KOT for kitchen/food orders
                                    string ticketPrefix = model.IsBarOrder ? "BOT" : "KOT";
                                    string ticketNumberSql = $@"
                                        SELECT '{ticketPrefix}-' + CONVERT(NVARCHAR(8), GETDATE(), 112) + '-' + 
                                        RIGHT('0000' + CAST((SELECT ISNULL(MAX(CAST(RIGHT(TicketNumber, 4) AS INT)), 0) + 1 
                                                            FROM KitchenTickets 
                                                            WHERE LEFT(TicketNumber, 12) = '{ticketPrefix}-' + CONVERT(NVARCHAR(8), GETDATE(), 112)) AS NVARCHAR(4)), 4)
                                    ";
                                    
                                    string ticketNumber = null;
                                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(ticketNumberSql, connection, transaction))
                                    {
                                        ticketNumber = (string)cmd.ExecuteScalar();
                                    }
                                    
                                    // First check the structure of KitchenTickets table
                                    
                                    
                                    // Query to get the exact schema for the table
                                    string schemaQuery = @"
                                        SELECT c.name AS ColumnName 
                                        FROM sys.columns c
                                        JOIN sys.tables t ON c.object_id = t.object_id
                                        WHERE t.name = 'KitchenTickets' AND t.type = 'U'";
                                        
                                    List<string> kitchenTicketColumns = new List<string>();
                                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(schemaQuery, connection, transaction))
                                    {
                                        using (Microsoft.Data.SqlClient.SqlDataReader reader = cmd.ExecuteReader())
                                        {
                                            while (reader.Read())
                                            {
                                                string columnName = reader.GetString(0);
                                                kitchenTicketColumns.Add(columnName);
                                                
                                            }
                                        }
                                    }
                                    
                                    // Check if UpdatedAt column exists
                                    bool hasKitchenTicketUpdatedAtColumn = kitchenTicketColumns.Contains("UpdatedAt");
                                    
                                    
                                    // We need to get the order number first
                                    string orderNumber = null;
                                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                        SELECT OrderNumber FROM Orders WHERE Id = @OrderId
                                    ", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                        object result = cmd.ExecuteScalar();
                                        if (result != null)
                                        {
                                            orderNumber = result.ToString();
                                            
                                        }
                                        else
                                        {
                                            
                                            throw new Exception("Order number is required but could not be retrieved");
                                        }
                                    }
                                    
                                    // Now include the OrderNumber in our insert
                                    // Add KitchenStation column support for BAR vs KITCHEN
                                    bool hasKitchenStationColumn = ColumnExistsInTable("KitchenTickets", "KitchenStation");
                                    string kitchenStation = model.IsBarOrder ? "BAR" : "KITCHEN";
                                    
                                    string insertKitchenTicketSql = hasKitchenStationColumn
                                        ? @"INSERT INTO [KitchenTickets] (
                                                [TicketNumber],
                                                [OrderId],
                                                [OrderNumber],
                                                [KitchenStation],
                                                [Status],
                                                [CreatedAt]
                                            ) VALUES (
                                                @TicketNumber,
                                                @OrderId,
                                                @OrderNumber,
                                                @KitchenStation,
                                                0,
                                                GETDATE()
                                            );
                                            SELECT SCOPE_IDENTITY();"
                                        : @"INSERT INTO [KitchenTickets] (
                                                [TicketNumber],
                                                [OrderId],
                                                [OrderNumber],
                                                [Status],
                                                [CreatedAt]
                                            ) VALUES (
                                                @TicketNumber,
                                                @OrderId,
                                                @OrderNumber,
                                                0,
                                                GETDATE()
                                            );
                                            SELECT SCOPE_IDENTITY();";
                                    
                                    // Create kitchen ticket
                                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(insertKitchenTicketSql, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@TicketNumber", ticketNumber);
                                        cmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                        cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                                        if (hasKitchenStationColumn)
                                        {
                                            cmd.Parameters.AddWithValue("@KitchenStation", kitchenStation);
                                        }
                                        kitchenTicketId = Convert.ToInt32(cmd.ExecuteScalar());
                                    }
                                    
                                    // Update order items and add them to kitchen ticket items
                                    // Note: Only processing food items now (bar items already handled by BOT)
                                    foreach (int itemId in itemsToProcess)
                                    {
                                        // Check if OrderItems table has UpdatedAt column
                                        bool hasItemUpdatedAtColumn = ColumnExistsInTable("OrderItems", "UpdatedAt");
                                        
                                        // Build SQL based on column existence
                                        string updateItemSql = hasItemUpdatedAtColumn
                                            ? @"UPDATE [OrderItems]
                                                SET [Status] = 1,
                                                    [FireTime] = GETDATE(),
                                                    [UpdatedAt] = GETDATE()
                                                WHERE [Id] = @ItemId AND [OrderId] = @OrderId AND [Status] = 0;"
                                            : @"UPDATE [OrderItems]
                                                SET [Status] = 1,
                                                    [FireTime] = GETDATE()
                                                WHERE [Id] = @ItemId AND [OrderId] = @OrderId AND [Status] = 0;";
                                        
                                        using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(updateItemSql, connection, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                                            cmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                            cmd.ExecuteNonQuery();
                                        }
                                        
                                        // Get the menu item name
                                        string menuItemName = null;
                                        using (Microsoft.Data.SqlClient.SqlCommand menuItemCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT mi.Name
                                            FROM OrderItems oi
                                            INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                                            WHERE oi.Id = @ItemId
                                        ", connection, transaction))
                                        {
                                            menuItemCmd.Parameters.AddWithValue("@ItemId", itemId);
                                            object result = menuItemCmd.ExecuteScalar();
                                            if (result != null)
                                            {
                                                menuItemName = result.ToString();
                                                
                                            }
                                            else
                                            {
                                                // Use a default value if we can't find the name
                                                menuItemName = "Unknown Item";
                                                
                                            }
                                        }
                                        
                                        // Add to kitchen ticket items with the menu item name
                                        using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand($@"
                                            INSERT INTO [{kitchenTicketItemsTableName}] (
                                                [KitchenTicketId], 
                                                [OrderItemId], 
                                                [MenuItemName],
                                                [Status]
                                            ) VALUES (
                                                @KitchenTicketId, 
                                                @OrderItemId, 
                                                @MenuItemName,
                                                0
                                            );
                                        ", connection, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@KitchenTicketId", kitchenTicketId);
                                            cmd.Parameters.AddWithValue("@OrderItemId", itemId);
                                            cmd.Parameters.AddWithValue("@MenuItemName", menuItemName);
                                            cmd.ExecuteNonQuery();
                                            
                                        }
                                    }
                                    
                                    // Check if Orders table has UpdatedAt column
                                    bool hasUpdatedAtColumn = ColumnExistsInTable("Orders", "UpdatedAt");
                                    
                                    // Build the update SQL based on column existence
                                    string updateOrderSql = hasUpdatedAtColumn 
                                        ? @"UPDATE [Orders]
                                            SET [Status] = CASE WHEN [Status] = 0 THEN 1 ELSE [Status] END,
                                                [UpdatedAt] = GETDATE()
                                            WHERE [Id] = @OrderId;"
                                        : @"UPDATE [Orders]
                                            SET [Status] = CASE WHEN [Status] = 0 THEN 1 ELSE [Status] END
                                            WHERE [Id] = @OrderId;";
                                    
                                    // Update order status
                                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(updateOrderSql, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                        cmd.ExecuteNonQuery();
                                    }
                                    
                                    // Commit the transaction
                                    transaction.Commit();
                                    
                                    // Log audit trail
                                    try
                                    {
                                        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                                        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                                        var actionType = model.IsBarOrder ? "Fire to Bar" : "Fire to Kitchen";
                                        var ticketType = model.IsBarOrder ? "BOT" : "KOT";
                                        
                                        await AuditTrailController.LogAuditAsync(_connectionString, model.OrderId, orderNumber, "Fire", "Order",
                                            model.OrderId, "Status", "Pending", "Fired", userId, userName, ipAddress, null, 
                                            $"{ticketType} #{ticketNumber} created - {itemsToProcess.Count} item(s) fired");
                                    }
                                    catch { /* Audit logging should not break the main flow */ }
                                    
                                    // Build success message based on order type
                                    string successMsg = "";
                                    if (kitchenTicketId > 0)
                                    {
                                        if (model.IsBarOrder)
                                        {
                                            successMsg = $"Items sent to bar successfully. BOT #{ticketNumber} created.";
                                        }
                                        else
                                        {
                                            successMsg = $"Items fired to kitchen successfully. KOT #{ticketNumber} created.";
                                        }
                                    }
                                    else
                                    {
                                        successMsg = "Failed to create ticket.";
                                    }
                                    
                                    TempData["SuccessMessage"] = successMsg;
                                }
                                catch (Exception ex)
                                {
                                    // Rollback the transaction on error
                                    transaction.Rollback();
                                    TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                                    
                                    
                                    
                                    // If there's an inner exception, log it too
                                    if (ex.InnerException != null)
                                    {
                                        
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TempData["ErrorMessage"] = $"Failed to fire items to kitchen: {ex.Message}";
                            
                        }
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                }
            }
            
            return RedirectToAction("Details", new { id = model.OrderId });
        }
        
        // Cancel Entire Order
        public IActionResult CancelOrder(int id, string? returnUrl = null)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();

                    // First check if the order exists and its status
                    using (Microsoft.Data.SqlClient.SqlCommand checkCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT Status 
                        FROM Orders 
                        WHERE Id = @OrderId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("@OrderId", id);
                        var status = (int?)checkCommand.ExecuteScalar();

                        if (status == null)
                        {
                            TempData["ErrorMessage"] = "Order not found.";
                            return SafeRedirectTo(returnUrl, nameof(Dashboard));
                        }

                        if (status == 3) // If already completed
                        {
                            TempData["ErrorMessage"] = "Cannot cancel order that has already been completed.";
                            return SafeRedirectTo(returnUrl, nameof(Dashboard));
                        }
                        
                        if (status == 4) // If already cancelled
                        {
                            TempData["ErrorMessage"] = "This order has already been cancelled.";
                            return SafeRedirectTo(returnUrl, nameof(Dashboard));
                        }
                    }

                    // Begin transaction since we'll be updating multiple tables
                    using (Microsoft.Data.SqlClient.SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update order status to cancelled
                            using (Microsoft.Data.SqlClient.SqlCommand updateCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                UPDATE Orders 
                                SET Status = 4, -- 4 = Cancelled
                                    UpdatedAt = GETDATE()
                                WHERE Id = @OrderId", connection, transaction))
                            {
                                updateCommand.Parameters.AddWithValue("@OrderId", id);
                                updateCommand.ExecuteNonQuery();
                            }
                            
                            // Update all pending order items to cancelled
                            using (Microsoft.Data.SqlClient.SqlCommand updateItemsCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                UPDATE OrderItems 
                                SET Status = 5, -- 5 = Cancelled
                                    UpdatedAt = GETDATE() 
                                WHERE OrderId = @OrderId
                                AND Status = 0", connection, transaction)) // Only cancel pending items
                            {
                                updateItemsCommand.Parameters.AddWithValue("@OrderId", id);
                                updateItemsCommand.ExecuteNonQuery();
                            }

                            // Check if OrderItemModifiers table exists
                            using (Microsoft.Data.SqlClient.SqlCommand checkTableCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                SELECT CASE 
                                    WHEN OBJECT_ID('OrderItemModifiers', 'U') IS NOT NULL THEN 1
                                    WHEN OBJECT_ID('OrderItem_Modifiers', 'U') IS NOT NULL THEN 2
                                    ELSE 0
                                END", connection, transaction))
                            {
                                int tableCheck = Convert.ToInt32(checkTableCommand.ExecuteScalar());
                                
                                // Only try to delete if one of the tables exists
                                if (tableCheck > 0)
                                {
                                    string tableName = tableCheck == 1 ? "OrderItemModifiers" : "OrderItem_Modifiers";
                                    
                                    using (Microsoft.Data.SqlClient.SqlCommand deleteModifiersCommand = new Microsoft.Data.SqlClient.SqlCommand($@"
                                        DELETE FROM {tableName} 
                                        WHERE OrderItemId IN (SELECT Id FROM OrderItems WHERE OrderId = @OrderId AND Status = 5)", 
                                        connection, transaction))
                                    {
                                        deleteModifiersCommand.Parameters.AddWithValue("@OrderId", id);
                                        deleteModifiersCommand.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                            TempData["SuccessMessage"] = "Order cancelled successfully.";
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Error cancelling order: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling order: " + ex.Message;
            }

            return SafeRedirectTo(returnUrl, nameof(Dashboard));
        }

        // Helper: redirect to a local returnUrl if provided, else to a controller action
        private IActionResult SafeRedirectTo(string? returnUrl, string fallbackAction)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction(fallbackAction);
        }

        // Cancel Order Item
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult CancelOrderItem(int orderId, int orderItemId)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();

                    // First check if the order item has already been sent to kitchen
                    using (Microsoft.Data.SqlClient.SqlCommand checkCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT Status 
                        FROM OrderItems 
                        WHERE Id = @OrderItemId AND OrderId = @OrderId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("@OrderItemId", orderItemId);
                        checkCommand.Parameters.AddWithValue("@OrderId", orderId);

                        var status = (int?)checkCommand.ExecuteScalar();

                        if (status == null)
                        {
                            TempData["ErrorMessage"] = "Order item not found.";
                            return RedirectToAction("Details", new { id = orderId });
                        }

                        if (status > 0) // If already sent to kitchen
                        {
                            TempData["ErrorMessage"] = "Cannot cancel item that has already been sent to kitchen.";
                            return RedirectToAction("Details", new { id = orderId });
                        }
                    }

                    // Begin transaction since we'll be updating multiple tables
                    using (Microsoft.Data.SqlClient.SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update order item status to cancelled
                            using (Microsoft.Data.SqlClient.SqlCommand updateCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                UPDATE OrderItems 
                                SET Status = 5, -- 5 = Cancelled
                                    UpdatedAt = GETDATE() 
                                WHERE Id = @OrderItemId AND OrderId = @OrderId", connection, transaction))
                            {
                                updateCommand.Parameters.AddWithValue("@OrderItemId", orderItemId);
                                updateCommand.Parameters.AddWithValue("@OrderId", orderId);
                                updateCommand.ExecuteNonQuery();
                            }

                            // Check if OrderItemModifiers table exists
                            using (Microsoft.Data.SqlClient.SqlCommand checkTableCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                SELECT CASE 
                                    WHEN OBJECT_ID('OrderItemModifiers', 'U') IS NOT NULL THEN 1
                                    WHEN OBJECT_ID('OrderItem_Modifiers', 'U') IS NOT NULL THEN 2
                                    ELSE 0
                                END", connection, transaction))
                            {
                                int tableCheck = Convert.ToInt32(checkTableCommand.ExecuteScalar());
                                
                                // Only try to delete if one of the tables exists
                                if (tableCheck > 0)
                                {
                                    string tableName = tableCheck == 1 ? "OrderItemModifiers" : "OrderItem_Modifiers";
                                    
                                    using (Microsoft.Data.SqlClient.SqlCommand deleteModifiersCommand = new Microsoft.Data.SqlClient.SqlCommand($@"
                                        DELETE FROM {tableName} 
                                        WHERE OrderItemId = @OrderItemId", connection, transaction))
                                    {
                                        deleteModifiersCommand.Parameters.AddWithValue("@OrderItemId", orderItemId);
                                        deleteModifiersCommand.ExecuteNonQuery();
                                    }
                                }
                            }

                            // Recalculate order totals
                            using (Microsoft.Data.SqlClient.SqlCommand updateOrderCommand = new Microsoft.Data.SqlClient.SqlCommand(@"
                                UPDATE o
                                SET o.Subtotal = (
                                        SELECT ISNULL(SUM(oi.Subtotal), 0)
                                        FROM OrderItems oi
                                        WHERE oi.OrderId = o.Id
                                          AND oi.Status != 5 -- Not cancelled
                                    ),
                                    o.TaxAmount = (
                                        SELECT ISNULL(SUM(oi.Subtotal), 0) * 0.10 -- 10% tax
                                        FROM OrderItems oi
                                        WHERE oi.OrderId = o.Id
                                          AND oi.Status != 5 -- Not cancelled
                                    ),
                                    o.UpdatedAt = GETDATE()
                                FROM Orders o
                                WHERE o.Id = @OrderId;

                                -- Update total amount
                                UPDATE Orders
                                SET TotalAmount = Subtotal + TaxAmount - DiscountAmount + TipAmount
                                WHERE Id = @OrderId;", connection, transaction))
                            {
                                updateOrderCommand.Parameters.AddWithValue("@OrderId", orderId);
                                updateOrderCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            TempData["SuccessMessage"] = "Order item cancelled successfully.";
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Error cancelling order item: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error cancelling order item: " + ex.Message;
            }

            return RedirectToAction("Details", new { id = orderId });
        }

        // Browse Menu Items
        public IActionResult BrowseMenu(int id)
        {
            var model = new OrderViewModel
            {
                Id = id,
                MenuCategories = new List<MenuCategoryViewModel>()
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT o.OrderNumber, ISNULL(t.TableName, 'N/A') AS TableName 
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    WHERE o.Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderNumber = reader.GetString(0);
                            model.TableName = reader.GetString(1);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
                
                // Get all categories
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name
                    FROM Categories
                    ORDER BY Name", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.MenuCategories.Add(new MenuCategoryViewModel
                            {
                                CategoryId = reader.GetInt32(0),
                                CategoryName = reader.GetString(1),
                                MenuItems = new List<MenuItem>()
                            });
                        }
                    }
                }
                
                // Get menu items for each category
                foreach (var category in model.MenuCategories)
                {
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT Id, Name, Description, Price, IsAvailable, ImagePath
                        FROM MenuItems
                        WHERE CategoryId = @CategoryId AND IsAvailable = 1
                        ORDER BY Name", connection))
                    {
                        command.Parameters.AddWithValue("@CategoryId", category.CategoryId);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                category.MenuItems.Add(new MenuItem
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    IsAvailable = reader.GetBoolean(4),
                                    ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    CategoryId = category.CategoryId
                                });
                            }
                        }
                    }
                }
                
                // Only keep categories that have menu items
                model.MenuCategories = model.MenuCategories.Where(c => c.MenuItems.Any()).ToList();
            }
            
            return View(model);
        }
        
        // Helper Methods
        
        /// <summary>
        /// Centralized method to recalculate and persist all GST and financial fields for an order.
        /// This ensures consistent GST calculation based on order type (BAR vs Foods) and persists
        /// all GST metadata to the database for reliable downstream consumption.
        /// </summary>
        /// <param name="orderId">The order ID to update</param>
        /// <param name="connection">Active database connection</param>
        /// <param name="transaction">Optional transaction context</param>
        private void UpdateOrderFinancials(int orderId, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction = null)
        {
            try
            {
                // Step 1: Read current order state and calculate subtotal from OrderItems
                decimal subtotalFromItems = 0m;
                decimal discountAmount = 0m;
                decimal tipAmount = 0m;
                bool isBarOrder = false;
                
                using (var readCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        ISNULL((SELECT SUM(oi.Subtotal) FROM OrderItems oi WHERE oi.OrderId = o.Id AND ISNULL(oi.Status,0) <> 5), 0) AS SubtotalFromItems,
                        ISNULL(o.DiscountAmount, 0) AS DiscountAmount,
                        ISNULL(o.TipAmount, 0) AS TipAmount,
                        CASE 
                            WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                                AND o.OrderKitchenType = 'Bar' THEN 1
                            WHEN EXISTS (SELECT 1 FROM KitchenTickets kt WHERE kt.OrderId = o.Id 
                                AND (kt.KitchenStation = 'BAR' OR kt.TicketNumber LIKE 'BOT-%')) THEN 1
                            ELSE 0
                        END AS IsBarOrder
                    FROM Orders o
                    WHERE o.Id = @OrderId", connection, transaction))
                {
                    readCmd.Parameters.AddWithValue("@OrderId", orderId);
                    using (var reader = readCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            subtotalFromItems = reader.GetDecimal(0);
                            discountAmount = reader.GetDecimal(1);
                            tipAmount = reader.GetDecimal(2);
                            isBarOrder = reader.GetInt32(3) == 1;
                        }
                        else
                        {
                            // Order not found - abort
                            return;
                        }
                    }
                }
                
                // Step 2: Get applicable GST percentage from settings (BAR vs Foods)
                decimal gstPercentage = 5.0m; // Default fallback
                try
                {
                    using (var settingsCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT 
                            ISNULL(DefaultGSTPercentage, 5.0) AS DefaultGSTPercentage,
                            ISNULL(BarGSTPerc, 5.0) AS BarGSTPerc
                        FROM dbo.RestaurantSettings", connection, transaction))
                    {
                        using (var reader = settingsCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                decimal defaultGst = reader.GetDecimal(0);
                                decimal barGst = reader.GetDecimal(1);
                                gstPercentage = isBarOrder ? barGst : defaultGst;
                            }
                        }
                    }
                }
                catch
                {
                    // If BarGSTPerc column doesn't exist, fall back to DefaultGSTPercentage only
                    using (var fallbackCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT ISNULL(DefaultGSTPercentage, 5.0) FROM dbo.RestaurantSettings", connection, transaction))
                    {
                        var result = fallbackCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            gstPercentage = Convert.ToDecimal(result);
                        }
                    }
                }
                
                // Step 3: Calculate GST based on order type
                decimal netSubtotal;
                decimal gstAmount;
                decimal adjustedSubtotal;
                decimal totalAmount;
                
                if (isBarOrder)
                {
                    // BAR Order: INCLUSIVE GST calculation
                    // Menu price INCLUDES GST, so we extract the taxable value
                    // Example: ₹100 menu price with 20% GST
                    //   Taxable Value = ₹100 ÷ 1.20 = ₹83.33
                    //   GST Amount = ₹83.33 × 20% = ₹16.67
                    //   Total = ₹100 (customer pays menu price)
                    
                    decimal menuPriceAfterDiscount = Math.Max(0m, subtotalFromItems - discountAmount);
                    decimal gstMultiplier = 1m + (gstPercentage / 100m); // e.g., 1.20 for 20% GST
                    
                    // Extract taxable value (GST-exclusive base amount)
                    decimal taxableValue = Math.Round(menuPriceAfterDiscount / gstMultiplier, 2, MidpointRounding.AwayFromZero);
                    
                    // Calculate GST on the taxable value
                    gstAmount = Math.Round(taxableValue * (gstPercentage / 100m), 2, MidpointRounding.AwayFromZero);
                    
                    // For BAR orders:
                    // - adjustedSubtotal = taxable value (stored in Subtotal column)
                    // - totalAmount = menu price after discount + tip
                    adjustedSubtotal = taxableValue;
                    netSubtotal = taxableValue; // For UI display
                    totalAmount = menuPriceAfterDiscount + tipAmount;
                }
                else
                {
                    // Foods Order: EXCLUSIVE GST calculation (existing logic)
                    // GST is added on top of the price
                    netSubtotal = Math.Max(0m, subtotalFromItems - discountAmount);
                    gstAmount = Math.Round(netSubtotal * gstPercentage / 100m, 2, MidpointRounding.AwayFromZero);
                    adjustedSubtotal = netSubtotal;
                    totalAmount = netSubtotal + gstAmount + tipAmount;
                }
                
                // Step 4: Split into CGST and SGST (equal split; handle last-cent rounding)
                decimal cgstPercentage = gstPercentage / 2m;
                decimal sgstPercentage = gstPercentage / 2m;
                decimal cgstAmount = Math.Round(gstAmount / 2m, 2, MidpointRounding.AwayFromZero);
                decimal sgstAmount = gstAmount - cgstAmount; // Ensures exact sum
                
                // Step 5: No additional calculation needed - totalAmount already calculated above
                
                // Step 6: Persist all calculated fields to Orders table (conditional update for schema compatibility)
                using (var updateCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    UPDATE Orders
                    SET 
                        Subtotal = @Subtotal,
                        TaxAmount = @GSTAmount,
                        TotalAmount = @TotalAmount,
                        UpdatedAt = GETDATE()
                    WHERE Id = @OrderId;
                    
                    -- Conditionally update new GST columns if they exist
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTPercentage')
                    BEGIN
                        UPDATE Orders
                        SET 
                            GSTPercentage = @GSTPercentage,
                            CGSTPercentage = @CGSTPercentage,
                            SGSTPercentage = @SGSTPercentage,
                            GSTAmount = @GSTAmount,
                            CGSTAmount = @CGSTAmount,
                            SGSTAmount = @SGSTAmount
                        WHERE Id = @OrderId;
                    END", connection, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@OrderId", orderId);
                    updateCmd.Parameters.AddWithValue("@Subtotal", adjustedSubtotal); // For BAR: taxable value; For Foods: net subtotal
                    updateCmd.Parameters.AddWithValue("@GSTPercentage", gstPercentage);
                    updateCmd.Parameters.AddWithValue("@CGSTPercentage", cgstPercentage);
                    updateCmd.Parameters.AddWithValue("@SGSTPercentage", sgstPercentage);
                    updateCmd.Parameters.AddWithValue("@GSTAmount", gstAmount);
                    updateCmd.Parameters.AddWithValue("@CGSTAmount", cgstAmount);
                    updateCmd.Parameters.AddWithValue("@SGSTAmount", sgstAmount);
                    updateCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                    
                    updateCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow order processing to continue even if GST persistence fails
                System.Diagnostics.Debug.WriteLine($"UpdateOrderFinancials failed for order {orderId}: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("CancelItem")]
        public IActionResult CancelOrderItem(int orderItemId)
        {
            if (orderItemId <= 0)
            {
                return Json(new { success = false, message = "Invalid item." });
            }

            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int orderId = 0;
                            int currentStatus = 0;
                            using (var readCmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT OrderId, ISNULL(Status,0) FROM OrderItems WHERE Id = @Id", connection, transaction))
                            {
                                readCmd.Parameters.AddWithValue("@Id", orderItemId);
                                using (var reader = readCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        orderId = reader.GetInt32(0);
                                        currentStatus = reader.GetInt32(1);
                                    }
                                    else
                                    {
                                        return Json(new { success = false, message = "Item not found." });
                                    }
                                }
                            }

                            // Only allow cancellation when status == 0 (New / not fired)
                            if (currentStatus > 0 && currentStatus != 5)
                            {
                                return Json(new { success = false, message = "Item already fired. Use Kitchen dashboard to cancel ticket, which will revert item to New." });
                            }

                            if (currentStatus == 5)
                            {
                                return Json(new { success = true, message = "Item already cancelled.", alreadyCancelled = true, orderId });
                            }

                            // Detect if CancelledAt column exists on OrderItems
                            bool hasCancelledAt = false;
                            using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                SELECT CASE WHEN EXISTS (
                                    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OrderItems') AND name = 'CancelledAt'
                                ) THEN 1 ELSE 0 END", connection, transaction))
                            {
                                hasCancelledAt = Convert.ToInt32(checkCmd.ExecuteScalar()) == 1;
                            }

                            string updateSql = hasCancelledAt
                                ? "UPDATE OrderItems SET Status = 5, CancelledAt = GETDATE() WHERE Id = @Id"
                                : "UPDATE OrderItems SET Status = 5 WHERE Id = @Id";

                            using (var upd = new Microsoft.Data.SqlClient.SqlCommand(updateSql, connection, transaction))
                            {
                                upd.Parameters.AddWithValue("@Id", orderItemId);
                                upd.ExecuteNonQuery();
                            }

                            // Optionally cancel related kitchen ticket items for this order item
                            try
                            {
                                using (var ktCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                    UPDATE kti SET Status = 4
                                    FROM KitchenTicketItems kti
                                    WHERE kti.OrderItemId = @OrderItemId", connection, transaction))
                                {
                                    ktCmd.Parameters.AddWithValue("@OrderItemId", orderItemId);
                                    ktCmd.ExecuteNonQuery();
                                }
                            }
                            catch { /* ignore if table schema differs */ }

                            // Recalculate financials excluding cancelled items
                            UpdateOrderFinancials(orderId, connection, transaction);

                            transaction.Commit();
                            return Json(new { success = true, message = "Item cancelled successfully.", orderId });
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            return Json(new { success = false, message = "Error cancelling item: " + ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error cancelling item: " + ex.Message });
            }
        }
    
    private OrderDashboardViewModel GetOrderDashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var canViewAllRecords = CurrentUserCanViewAllOrderData();
            var currentUserId = GetCurrentUserId();
            var model = new OrderDashboardViewModel
            {
                ActiveOrders = new List<OrderSummary>(),
                CompletedOrders = new List<OrderSummary>(),
                CancelledOrders = new List<OrderSummary>()
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order counts and total sales for today (exclude Bar orders)
                var orderSummarySql = @"
                    SELECT
                        SUM(CASE WHEN Status = 0 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS OpenCount,
                        SUM(CASE WHEN Status = 1 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS InProgressCount,
                        SUM(CASE WHEN Status = 2 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS ReadyCount,
                        SUM(CASE WHEN Status = 3 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS CompletedCount,
                        SUM(CASE WHEN Status = 3 AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE) THEN TotalAmount ELSE 0 END) AS TotalSales,
                        SUM(CASE WHEN Status = 4 AND CAST(ISNULL(UpdatedAt, CreatedAt) AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS CancelledCount
                    FROM Orders
                    WHERE (OrderKitchenType != 'Bar' OR OrderKitchenType IS NULL)";

                if (!canViewAllRecords)
                {
                    orderSummarySql += " AND UserId = @UserId";
                }

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(orderSummarySql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                
                // Get active orders (exclude Bar orders)
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
                    WHERE o.Status < 3 -- Not completed
                    AND (o.OrderKitchenType != 'Bar' OR o.OrderKitchenType IS NULL)";

                if (!canViewAllRecords)
                {
                    activeOrderSql += " AND o.UserId = @UserId";
                }

                activeOrderSql += " ORDER BY o.CreatedAt DESC";

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(activeOrderSql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                            
                            // Override with merged table names if available
                            summary.TableName = GetMergedTableDisplayName(summary.Id, summary.TableName);
                            model.ActiveOrders.Add(summary);
                        }
                    }
                }
                
                // Get completed orders (filtered by date range if provided, exclude Bar orders)
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
                        DATEDIFF(MINUTE, o.CreatedAt, o.CompletedAt) AS DurationMinutes
                    FROM Orders o
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    LEFT JOIN Users u ON o.UserId = u.Id
                    WHERE o.Status = 3 -- Completed
                    AND (o.OrderKitchenType != 'Bar' OR o.OrderKitchenType IS NULL)
                ";

                if (!canViewAllRecords)
                {
                    completedSql += " AND o.UserId = @UserId";
                }

                if (fromDate.HasValue && toDate.HasValue)
                {
                    completedSql += " AND CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate";
                    completedSql += " ORDER BY o.CompletedAt DESC";
                }
                else
                {
                    // default: today
                    completedSql += " AND CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE) ORDER BY o.CompletedAt DESC";
                }

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(completedSql, connection))
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
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                                Status = 3, // Completed
                                StatusDisplay = "Completed",
                                TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                GuestName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                ItemCount = reader.GetInt32(7),
                                TotalAmount = reader.GetDecimal(8),
                                CreatedAt = reader.GetDateTime(9),
                                Duration = TimeSpan.FromMinutes(reader.IsDBNull(10) ? 0 : reader.GetInt32(10))
                            };
                            
                            // Override with merged table names if available
                            completedSummary.TableName = GetMergedTableDisplayName(completedSummary.Id, completedSummary.TableName);
                            model.CompletedOrders.Add(completedSummary);
                        }
                    }
                }
                
                // Get cancelled orders for today (filtered by cancellation date, exclude Bar orders)
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
                    WHERE o.Status = 4 -- Cancelled
                    AND (o.OrderKitchenType != 'Bar' OR o.OrderKitchenType IS NULL)
                    AND CAST(ISNULL(o.UpdatedAt, o.CreatedAt) AS DATE) = CAST(GETDATE() AS DATE) -- Filter by cancellation date";

                if (!canViewAllRecords)
                {
                    cancelledSql += " AND o.UserId = @UserId";
                }

                cancelledSql += " ORDER BY ISNULL(o.UpdatedAt, o.CreatedAt) DESC";

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(cancelledSql, connection))
                {
                    if (!canViewAllRecords)
                    {
                        command.Parameters.AddWithValue("@UserId", currentUserId);
                    }
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
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
                            
                            // Override with merged table names if available
                            cancelledSummary.TableName = GetMergedTableDisplayName(cancelledSummary.Id, cancelledSummary.TableName);
                            model.CancelledOrders.Add(cancelledSummary);
                        }
                    }
                }
            }
            
            return model;
        }

        private bool CurrentUserCanViewAllOrderData()
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
        
        /// <summary>
        /// Helper method to build SQL queries with either StationId or KitchenStationId
        /// depending on the database schema
        /// </summary>
        private string GetSafeStationIdFieldName()
        {
            // Default to using KitchenStationId as that's the schema in the model
            return "KitchenStationId";
        }
        
        /// <summary>
        /// Helper method to get the correct table name for menu item relationships
        /// </summary>
        private string GetMenuItemRelationshipTableName(string relationship)
        {
            // Check if the table exists with underscore first (as in SQL scripts)
            bool tableWithUnderscoreExists = false;
            bool tableWithoutUnderscoreExists = false;
            
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection con = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    con.Open();
                    
                    // Check if table with underscore exists
                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand($"SELECT CASE WHEN OBJECT_ID('MenuItem_{relationship}', 'U') IS NOT NULL THEN 1 ELSE 0 END", con))
                    {
                        tableWithUnderscoreExists = Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                    
                    // Only check without underscore if underscore version doesn't exist
                    if (!tableWithUnderscoreExists)
                    {
                        using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand($"SELECT CASE WHEN OBJECT_ID('MenuItem{relationship}', 'U') IS NOT NULL THEN 1 ELSE 0 END", con))
                        {
                            tableWithoutUnderscoreExists = Convert.ToBoolean(cmd.ExecuteScalar());
                        }
                    }
                }
            }
            catch
            {
                // If any error occurs, assume neither table exists
                tableWithUnderscoreExists = false;
                tableWithoutUnderscoreExists = false;
            }
            
            if (tableWithUnderscoreExists)
                return $"MenuItem_{relationship}";
            else if (tableWithoutUnderscoreExists)
                return $"MenuItem{relationship}";
            else
                return $"MenuItem{relationship}"; // Default to version without underscore
        }
        
        /// <summary>
        /// Helper method to check if a column exists in a table
        /// </summary>
        private bool ColumnExistsInTable(string tableName, string columnName)
        {
            try
            {
                // Safety check - if table doesn't exist, column can't exist
                if (string.IsNullOrEmpty(tableName))
                {
                    return false;
                }

                // Clean table name (remove any brackets and schema)
                string cleanTableName = tableName.Replace("[", "").Replace("]", "");
                if (cleanTableName.Contains("."))
                {
                    cleanTableName = cleanTableName.Split('.').Last();
                }
                
                using (Microsoft.Data.SqlClient.SqlConnection con = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    con.Open();
                    
                    // First verify the table exists
                    string tableQuery = @"
                        SELECT COUNT(1)
                        FROM sys.tables
                        WHERE name = @TableName";
                        
                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(tableQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@TableName", cleanTableName);
                        int tableExists = Convert.ToInt32(cmd.ExecuteScalar());
                        
                        if (tableExists == 0)
                        {
                            return false; // Table doesn't exist
                        }
                    }
                    
                    // Now check if the column exists
                    string columnQuery = @"
                        SELECT COUNT(1)
                        FROM sys.columns c
                        JOIN sys.tables t ON c.object_id = t.object_id
                        WHERE t.name = @TableName AND c.name = @ColumnName";
                    
                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(columnQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@TableName", cleanTableName);
                        cmd.Parameters.AddWithValue("@ColumnName", columnName);
                        
                        int result = Convert.ToInt32(cmd.ExecuteScalar());
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception if possible
                
                // If any error occurs, assume the column doesn't exist
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to check if a table exists in the database
        /// </summary>
        private bool TableExists(string tableName)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection con = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    con.Open();
                    
                    using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand($"SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NOT NULL THEN 1 ELSE 0 END", con))
                    {
                        cmd.Parameters.AddWithValue("@TableName", tableName);
                        return Convert.ToBoolean(cmd.ExecuteScalar());
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Helper method to find the correct version of a table name
        /// </summary>
        private string GetCorrectTableName(string baseTableName, string alternativeTableName)
        {
            if (TableExists(baseTableName))
            {
                return baseTableName;
            }
            else if (TableExists(alternativeTableName))
            {
                return alternativeTableName;
            }
            
            // Return the base name as fallback
            return baseTableName;
        }

        // Helper to get merged table display name for an order
        private string GetMergedTableDisplayName(int orderId, string existingTableName)
        {
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT STRING_AGG(t.TableName, ' + ') WITHIN GROUP (ORDER BY t.TableName)
                        FROM OrderTables ot
                        INNER JOIN Tables t ON ot.TableId = t.Id
                        WHERE ot.OrderId = @OrderId", connection);
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    var aggregated = cmd.ExecuteScalar() as string;
                    
                    if (string.IsNullOrWhiteSpace(aggregated))
                        return existingTableName; // No merged tables, return original
                    
                    // If there's both a primary table and merged tables, combine without duplicates
                    if (!string.IsNullOrWhiteSpace(existingTableName) && !aggregated.Contains(existingTableName))
                        return existingTableName + " + " + aggregated;
                    
                    return aggregated; // Return merged table names
                }
            }
            catch
            {
                return existingTableName; // Fallback to existing if error
            }
        }
        
        private OrderViewModel GetOrderDetails(int id)
        {
            OrderViewModel order = null;
            
            // Use separate connections for different data readers to avoid nested DataReader issues
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details
                // First check if the UpdatedAt column exists in the Orders table
                bool hasUpdatedAtColumn = ColumnExistsInTable("Orders", "UpdatedAt");
                
                // Build the SQL query based on column existence
                string selectSql = hasUpdatedAtColumn 
                    ? @"SELECT 
                        o.Id,
                        o.OrderNumber,
                        o.TableTurnoverId,
                        o.OrderType,
                        o.Status,
                        o.UserId,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        o.CustomerName,
                        o.CustomerPhone,
                        o.Subtotal,
                        o.TaxAmount,
                        o.TipAmount,
                        o.DiscountAmount,
                        o.TotalAmount,
                        o.SpecialInstructions,
                        o.CreatedAt,
                        o.UpdatedAt,
                        o.CompletedAt,
                        ISNULL(o.GSTPercentage, 0) AS GSTPercentage,
                        ISNULL(o.CGSTPercentage, 0) AS CGSTPercentage,
                        ISNULL(o.SGSTPercentage, 0) AS SGSTPercentage,
                        ISNULL(o.GSTAmount, 0) AS GSTAmount,
                        ISNULL(o.CGSTAmount, 0) AS CGSTAmount,
                        ISNULL(o.SGSTAmount, 0) AS SGSTAmount,"
                    : @"SELECT 
                        o.Id,
                        o.OrderNumber,
                        o.TableTurnoverId,
                        o.OrderType,
                        o.Status,
                        o.UserId,
                        CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
                        o.CustomerName,
                        o.CustomerPhone,
                        o.Subtotal,
                        o.TaxAmount,
                        o.TipAmount,
                        o.DiscountAmount,
                        o.TotalAmount,
                        o.SpecialInstructions,
                        o.CreatedAt,
                        o.CreatedAt AS UpdatedAt, -- Use CreatedAt as a fallback
                        o.CompletedAt,
                        ISNULL(o.GSTPercentage, 0) AS GSTPercentage,
                        ISNULL(o.CGSTPercentage, 0) AS CGSTPercentage,
                        ISNULL(o.SGSTPercentage, 0) AS SGSTPercentage,
                        ISNULL(o.GSTAmount, 0) AS GSTAmount,
                        ISNULL(o.CGSTAmount, 0) AS CGSTAmount,
                        ISNULL(o.SGSTAmount, 0) AS SGSTAmount,";

                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(selectSql + @"
                        CASE 
                            WHEN o.TableTurnoverId IS NOT NULL THEN t.TableName 
                            ELSE NULL 
                        END AS TableName,
                        CASE 
                            WHEN o.TableTurnoverId IS NOT NULL THEN tt.GuestName 
                            ELSE o.CustomerName 
                        END AS GuestName
                    FROM Orders o
                    LEFT JOIN Users u ON o.UserId = u.Id
                    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
                    LEFT JOIN Tables t ON tt.TableId = t.Id
                    WHERE o.Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var orderType = reader.GetInt32(3);
                            string orderTypeDisplay = orderType switch
                            {
                                0 => "Dine In",
                                1 => "Take Out",
                                2 => "Delivery",
                                _ => "Unknown"
                            };
                            
                            var status = reader.GetInt32(4);
                            string statusDisplay = status switch
                            {
                                0 => "Open",
                                1 => "In Progress",
                                2 => "Ready",
                                3 => "Completed",
                                4 => "Cancelled",
                                _ => "Unknown"
                            };
                            
                            order = new OrderViewModel
                            {
                                Id = reader.GetInt32(0),
                                OrderNumber = reader.GetString(1),
                                TableTurnoverId = reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                                OrderType = orderType,
                                OrderTypeDisplay = orderTypeDisplay,
                                Status = status,
                                StatusDisplay = statusDisplay,
                                ServerName = reader.IsDBNull(6) ? null : reader.GetString(6),
                                CustomerName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                CustomerPhone = reader.IsDBNull(8) ? null : reader.GetString(8),
                                Subtotal = reader.GetDecimal(9),
                                TaxAmount = reader.GetDecimal(10),
                                TipAmount = reader.GetDecimal(11),
                                DiscountAmount = reader.GetDecimal(12),
                                TotalAmount = reader.GetDecimal(13),
                                SpecialInstructions = reader.IsDBNull(14) ? null : reader.GetString(14),
                                CreatedAt = reader.GetDateTime(15),
                                UpdatedAt = reader.GetDateTime(16), // We've handled this in the SQL query
                                CompletedAt = reader.IsDBNull(17) ? null : (DateTime?)reader.GetDateTime(17),
                                TableName = reader.IsDBNull(24) ? null : reader.GetString(24),
                                GuestName = reader.IsDBNull(25) ? null : reader.GetString(25),
                                // Read persisted GST metadata from Orders table
                                GSTPercentage = reader.IsDBNull(18) ? 0m : reader.GetDecimal(18),
                                CGSTAmount = reader.IsDBNull(22) ? 0m : reader.GetDecimal(22),
                                SGSTAmount = reader.IsDBNull(23) ? 0m : reader.GetDecimal(23),
                                Items = new List<OrderItemViewModel>(),
                                KitchenTickets = new List<KitchenTicketViewModel>(),
                                AvailableCourses = new List<CourseType>()
                            };
                            
                            // Override with merged table names if available
                            order.TableName = GetMergedTableDisplayName(order.Id, order.TableName);
                        }
                        else
                        {
                            return null; // Order not found
                        }
                    }
                }
                
                // Get order items
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        oi.Id,
                        oi.MenuItemId,
                        mi.Name AS MenuItemName,
                        mi.Description AS MenuItemDescription,
                        oi.Quantity,
                        oi.UnitPrice,
                        oi.Subtotal,
                        oi.SpecialInstructions,
                        oi.CourseId,
                        ct.Name AS CourseName,
                        oi.Status,
                        oi.FireTime,
                        oi.CompletionTime,
                        oi.DeliveryTime
                    FROM OrderItems oi
                    INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                    LEFT JOIN CourseTypes ct ON oi.CourseId = ct.Id
                    WHERE oi.OrderId = @OrderId
                    ORDER BY 
                        CASE WHEN oi.CourseId IS NULL THEN 999 ELSE oi.CourseId END,
                        oi.CreatedAt", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var status = reader.GetInt32(10);
                            string statusDisplay = status switch
                            {
                                0 => "New",
                                1 => "Fired",
                                2 => "Cooking",
                                3 => "Ready",
                                4 => "Delivered",
                                5 => "Cancelled",
                                _ => "Unknown"
                            };
                            
                            var orderItem = new OrderItemViewModel
                            {
                                Id = reader.GetInt32(0),
                                OrderId = id,
                                MenuItemId = reader.GetInt32(1),
                                MenuItemName = reader.GetString(2),
                                MenuItemDescription = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Quantity = reader.GetInt32(4),
                                UnitPrice = reader.GetDecimal(5),
                                Subtotal = reader.GetDecimal(6),
                                SpecialInstructions = reader.IsDBNull(7) ? null : reader.GetString(7),
                                CourseId = reader.IsDBNull(8) ? null : (int?)reader.GetInt32(8),
                                CourseName = reader.IsDBNull(9) ? null : reader.GetString(9),
                                Status = status,
                                StatusDisplay = statusDisplay,
                                FireTime = reader.IsDBNull(11) ? null : (DateTime?)reader.GetDateTime(11),
                                CompletionTime = reader.IsDBNull(12) ? null : (DateTime?)reader.GetDateTime(12),
                                DeliveryTime = reader.IsDBNull(13) ? null : (DateTime?)reader.GetDateTime(13),
                                Modifiers = new List<OrderItemModifierViewModel>()
                            };
                            
                            order.Items.Add(orderItem);
                        }
                    }
                }
            }
                
            // Get order item modifiers using separate connections for each item
            foreach (var item in order.Items)
            {
                // Check which version of the table exists (with or without underscore)
                string orderItemModifiersTable = GetCorrectTableName("OrderItemModifiers", "OrderItem_Modifiers");
                
                if (!string.IsNullOrEmpty(orderItemModifiersTable))
                {
                    // Use a separate connection for modifiers to avoid DataReader issues
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        string modifiersQuery = $@"
                            SELECT 
                                oim.Id,
                                oim.ModifierId,
                                m.Name AS ModifierName,
                                oim.Price
                            FROM {orderItemModifiersTable} oim
                            INNER JOIN Modifiers m ON oim.ModifierId = m.Id
                            WHERE oim.OrderItemId = @OrderItemId";
                            
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(modifiersQuery, connection))
                        {
                            command.Parameters.AddWithValue("@OrderItemId", item.Id);
                            
                            try
                            {
                                // First check if the table exists
                                bool tableExists = TableExists(orderItemModifiersTable);
                                
                                if (tableExists)
                                {
                                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            item.Modifiers.Add(new OrderItemModifierViewModel
                                            {
                                                Id = reader.GetInt32(0),
                                                OrderItemId = item.Id,
                                                ModifierId = reader.GetInt32(1),
                                                ModifierName = reader.GetString(2),
                                                Price = reader.GetDecimal(3)
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log the exception
                                
                            }
                        }
                    }
                }
            }
                
            // Get kitchen tickets using a separate connection
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                string stationIdFieldName = GetSafeStationIdFieldName();
                string kitchenTicketQuery = $@"
                    SELECT 
                        kt.Id,
                        kt.TicketNumber,
                        kt.{stationIdFieldName},
                        kt.Status,
                        kt.CreatedAt,
                        kt.CompletedAt
                    FROM KitchenTickets kt
                    WHERE kt.OrderId = @OrderId
                    ORDER BY kt.CreatedAt DESC";
                
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(kitchenTicketQuery, connection))
                {
                    command.Parameters.AddWithValue("@OrderId", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var status = reader.GetInt32(3);
                            string statusDisplay = status switch
                            {
                                0 => "New",
                                1 => "In Progress",
                                2 => "Ready",
                                3 => "Completed",
                                4 => "Cancelled",
                                _ => "Unknown"
                            };
                            
                            var kitchenTicket = new KitchenTicketViewModel
                            {
                                Id = reader.GetInt32(0),
                                TicketNumber = reader.GetString(1),
                                OrderId = id,
                                OrderNumber = order.OrderNumber,
                                StationId = reader.IsDBNull(2) ? null : (int?)reader.GetInt32(2),
                                Status = status,
                                StatusDisplay = statusDisplay,
                                CreatedAt = reader.GetDateTime(4),
                                CompletedAt = reader.IsDBNull(5) ? null : (DateTime?)reader.GetDateTime(5),
                                Items = new List<KitchenTicketItemViewModel>()
                            };
                            
                            order.KitchenTickets.Add(kitchenTicket);
                        }
                    }
                }
            }
            
            // paid/remaining will be computed after totals (GST/Discount) are finalized below

            // Use a new connection for kitchen ticket items to avoid DataReader issues
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                // Get kitchen ticket items
                foreach (var ticket in order.KitchenTickets)
                {
                    // Get the correct table name for kitchen ticket items
                    string kitchenTicketItemsTable = GetCorrectTableName("KitchenTicketItems", "Kitchen_TicketItems");
                    
                    string queryString;
                    if (kitchenTicketItemsTable == "KitchenTicketItems")
                    {
                        // Use direct field access because the schema might have changed
                        queryString = $@"
                            SELECT 
                                kti.Id,
                                kti.OrderItemId,
                                mi.Name,
                                oi.Quantity,
                                oi.SpecialInstructions,
                                kti.Status,
                                kti.StartTime,
                                kti.CompletionTime,
                                kti.Notes
                            FROM {kitchenTicketItemsTable} kti
                            INNER JOIN OrderItems oi ON kti.OrderItemId = oi.Id
                            INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                            WHERE kti.KitchenTicketId = @KitchenTicketId";
                    }
                    else
                    {
                        // Get field names for the alternate version of the table
                        queryString = $@"
                            SELECT 
                                kti.Id,
                                kti.OrderItemId,
                                mi.Name,
                                oi.Quantity,
                                oi.SpecialInstructions,
                                kti.Status,
                                kti.StartTime,
                                kti.CompletionTime,
                                kti.Notes
                            FROM {kitchenTicketItemsTable} kti
                            INNER JOIN OrderItems oi ON kti.OrderItemId = oi.Id
                            INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                            WHERE kti.KitchenTicketId = @KitchenTicketId";
                    }
                    
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(queryString, connection))
                    {
                        command.Parameters.AddWithValue("@KitchenTicketId", ticket.Id);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var status = reader.GetInt32(5);
                                string statusDisplay = status switch
                                {
                                    0 => "New",
                                    1 => "In Progress",
                                    2 => "Ready",
                                    3 => "Completed",
                                    4 => "Cancelled",
                                    _ => "Unknown"
                                };
                                
                                var ticketItem = new KitchenTicketItemViewModel
                                {
                                    Id = reader.GetInt32(0),
                                    KitchenTicketId = ticket.Id,
                                    OrderItemId = reader.GetInt32(1),
                                    MenuItemName = reader.GetString(2),
                                    Quantity = reader.GetInt32(3),
                                    SpecialInstructions = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    Status = status,
                                    StatusDisplay = statusDisplay,
                                    StartTime = reader.IsDBNull(6) ? null : (DateTime?)reader.GetDateTime(6),
                                    CompletionTime = reader.IsDBNull(7) ? null : (DateTime?)reader.GetDateTime(7),
                                    Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    Modifiers = new List<string>()
                                };
                                
                                // Get modifiers for this ticket item using a separate connection
                                string orderItemModifiersTable = GetCorrectTableName("OrderItemModifiers", "OrderItem_Modifiers");
                                
                                if (!string.IsNullOrEmpty(orderItemModifiersTable))
                                {
                                    // First check if the table exists
                                    bool tableExists = TableExists(orderItemModifiersTable);
                                    
                                    if (tableExists)
                                    {
                                        using (Microsoft.Data.SqlClient.SqlConnection modConnection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                                        {
                                            modConnection.Open();
                                            string modifiersQuery = $@"
                                                SELECT m.Name
                                                FROM {orderItemModifiersTable} oim
                                                INNER JOIN Modifiers m ON oim.ModifierId = m.Id
                                                WHERE oim.OrderItemId = @OrderItemId";
                                                
                                            using (Microsoft.Data.SqlClient.SqlCommand modifiersCommand = new Microsoft.Data.SqlClient.SqlCommand(modifiersQuery, modConnection))
                                            {
                                                modifiersCommand.Parameters.AddWithValue("@OrderItemId", ticketItem.OrderItemId);
                                            
                                                try
                                                {
                                                    using (Microsoft.Data.SqlClient.SqlDataReader modifiersReader = modifiersCommand.ExecuteReader())
                                                    {
                                                        while (modifiersReader.Read())
                                                        {
                                                            ticketItem.Modifiers.Add(modifiersReader.GetString(0));
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    // Log but don't crash if there are any remaining issues
                                                    
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                ticket.Items.Add(ticketItem);
                            }
                        }
                    }
                }
                
                // Get available courses for new items
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name
                    FROM CourseTypes
                    ORDER BY DisplayOrder", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            order.AvailableCourses.Add(new CourseType
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            
            // After loading all order core data and items, compute GST dynamically using settings
            // ONLY if GST was not already persisted (GSTPercentage = 0 means legacy order or order with no items)
            try
            {
                if (order != null && order.GSTPercentage == 0)
                {
                    // Legacy order without persisted GST or new order without items
                    // Check if this is a BAR order to use correct GST percentage
                    bool isBarOrder = false;
                    using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        // Check OrderKitchenType or KitchenTickets to determine if BAR order
                        using (var checkBarCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                            SELECT CASE 
                                WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'OrderKitchenType')
                                    AND EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId AND OrderKitchenType = 'Bar')
                                THEN 1
                                WHEN EXISTS (SELECT 1 FROM KitchenTickets WHERE OrderId = @OrderId AND KitchenStation = 'BAR')
                                THEN 1
                                ELSE 0
                            END", connection))
                        {
                            checkBarCmd.Parameters.AddWithValue("@OrderId", id);
                            var result = checkBarCmd.ExecuteScalar();
                            isBarOrder = result != null && Convert.ToInt32(result) == 1;
                        }
                        
                        // Retrieve appropriate GST % from settings table
                        string gstColumn = isBarOrder ? "BarGSTPerc" : "DefaultGSTPercentage";
                        using (var cmd = new Microsoft.Data.SqlClient.SqlCommand($"SELECT TOP 1 {gstColumn} FROM dbo.RestaurantSettings ORDER BY Id", connection))
                        {
                            var gstObj = cmd.ExecuteScalar();
                            decimal gstPercent = 0m;
                            if (gstObj != null && gstObj != DBNull.Value)
                            {
                                decimal.TryParse(gstObj.ToString(), out gstPercent);
                            }
                            order.GSTPercentage = gstPercent;
                            // Recalculate subtotal from items (exclude cancelled status=5)
                            var effectiveSubtotal = order.Items?.Where(i => i.Status != 5).Sum(i => i.Subtotal) ?? order.Subtotal;
                            // Calculate GST amount (round to 2 decimals)
                            var gstAmount = Math.Round(effectiveSubtotal * gstPercent / 100m, 2, MidpointRounding.AwayFromZero);
                            order.TaxAmount = gstAmount; // maintain backward compatibility field
                            order.CGSTAmount = Math.Round(gstAmount / 2m, 2, MidpointRounding.AwayFromZero);
                            order.SGSTAmount = gstAmount - order.CGSTAmount; // ensure total matches after rounding
                            order.TotalAmount = effectiveSubtotal + gstAmount + order.TipAmount - order.DiscountAmount;
                            order.Subtotal = effectiveSubtotal; // ensure stored value aligns
                        }
                    }
                }
                else if (order != null)
                {
                    // Modern order with persisted GST - use the stored values
                    // Just ensure TaxAmount is in sync with GSTAmount for backward compatibility
                    var effectiveSubtotal = order.Items?.Where(i => i.Status != 5).Sum(i => i.Subtotal) ?? order.Subtotal;
                    order.Subtotal = effectiveSubtotal;
                    // TaxAmount and CGSTAmount/SGSTAmount already read from database
                }
            }
            catch (Exception ex)
            {
                // Log and continue silently so page still loads
                
            }
            // After totals are finalized, compute paid amount (approved payments only) and remaining
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT ISNULL(SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)), 0) FROM Payments WHERE OrderId = @OrderId AND Status = 1
                    ", connection))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", id);
                        var obj = cmd.ExecuteScalar();
                        decimal paid = 0m;
                        if (obj != null && obj != DBNull.Value) paid = Convert.ToDecimal(obj);
                        order.PaidAmount = paid;
                        order.RemainingAmount = Math.Round(order.TotalAmount - paid, 2, MidpointRounding.AwayFromZero);
                    }
                }
            }
            catch { /* ignore payment read failures */ }
            return order;
        }
        private int GetCurrentUserId()
        {
            try
            {
                var claim = HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out int uid)) return uid;
            }
            catch { }
            // Fallback to admin for legacy behavior
            return 1;
        }

        private string GetCurrentUserName()
        {
            try
            {
                var name = HttpContext?.User?.Identity?.Name;
                if (!string.IsNullOrEmpty(name)) return name;
                var fullNameClaim = HttpContext?.User?.FindFirst("FullName");
                if (fullNameClaim != null) return fullNameClaim.Value;
            }
            catch { }
            return "System Admin";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateOrderItemQty(int orderId, int orderItemId, int quantity, string specialInstructions)
        {
            if (quantity < 1)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Quantity must be at least 1." });
                }
                TempData["ErrorMessage"] = "Quantity must be at least 1.";
                return RedirectToAction("Details", new { id = orderId });
            }
            
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    // Update quantity, subtotal, and special instructions
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"UPDATE OrderItems SET Quantity = @Quantity, Subtotal = UnitPrice * @Quantity, SpecialInstructions = @SpecialInstructions WHERE Id = @OrderItemId", connection))
                    {
                        command.Parameters.AddWithValue("@Quantity", quantity);
                        command.Parameters.AddWithValue("@OrderItemId", orderItemId);
                        command.Parameters.AddWithValue("@SpecialInstructions", (object?)specialInstructions ?? DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                    // Recalculate order totals
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Orders
                        SET Subtotal = (SELECT SUM(Subtotal) FROM OrderItems WHERE OrderId = @OrderId),
                            TotalAmount = (SELECT SUM(Subtotal) FROM OrderItems WHERE OrderId = @OrderId) + ISNULL(TaxAmount,0) + ISNULL(TipAmount,0) - ISNULL(DiscountAmount,0)
                        WHERE Id = @OrderId", connection))
                    {
                        command.Parameters.AddWithValue("@OrderId", orderId);
                        command.ExecuteNonQuery();
                    }
                }
                
                // For AJAX requests, return JSON
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Item updated successfully." });
                }
                
                // For standard requests, redirect with message
                TempData["SuccessMessage"] = "Item updated.";
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error updating item: " + ex.Message });
                }
                
                TempData["ErrorMessage"] = "Error updating item: " + ex.Message;
                return RedirectToAction("Details", new { id = orderId });
            }
        }
        
        // Model for bulk updates
        public class OrderItemUpdateModel
        {
            public int OrderItemId { get; set; }
            public int Quantity { get; set; }
            public string SpecialInstructions { get; set; }
            public bool IsNew { get; set; }
            public int? MenuItemId { get; set; }  // For new items
            public int? TempId { get; set; }      // For tracking new items client-side
        }
        
    [HttpPost]
    [ValidateAntiForgeryToken]
        public IActionResult UpdateMultipleOrderItems(int orderId, [FromBody] List<OrderItemUpdateModel> items)
        {
            if (items == null || !items.Any())
            {
                return Json(new { success = false, message = "No items to update." });
            }
            
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // First handle existing item updates
                            var existingItems = items.Where(i => !i.IsNew).ToList();
                            var newItems = items.Where(i => i.IsNew).ToList();
                            
                            // Update each existing item
                            foreach (var item in existingItems)
                            {
                                if (item.Quantity < 1)
                                {
                                    transaction.Rollback();
                                    return Json(new { success = false, message = $"Item #{item.OrderItemId}: Quantity must be at least 1." });
                                }
                                
                                using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"
                                    UPDATE OrderItems 
                                    SET Quantity = @Quantity, 
                                        Subtotal = UnitPrice * @Quantity, 
                                        SpecialInstructions = @SpecialInstructions 
                                    WHERE Id = @OrderItemId", connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@OrderItemId", item.OrderItemId);
                                    command.Parameters.AddWithValue("@SpecialInstructions", 
                                        string.IsNullOrEmpty(item.SpecialInstructions) ? DBNull.Value : (object)item.SpecialInstructions);
                                    command.ExecuteNonQuery();
                                }
                            }
                            
                            // Insert each new item
                            foreach (var item in newItems)
                            {
                                if (item.Quantity < 1 || !item.MenuItemId.HasValue)
                                {
                                    transaction.Rollback();
                                    return Json(new { success = false, message = "Invalid new item data." });
                                }
                                
                                // First get the unit price of the menu item
                                decimal unitPrice = 0;
                                using (var command = new Microsoft.Data.SqlClient.SqlCommand(
                                    "SELECT Price FROM MenuItems WHERE Id = @MenuItemId", connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@MenuItemId", item.MenuItemId.Value);
                                    var result = command.ExecuteScalar();
                                    if (result != null)
                                    {
                                        unitPrice = Convert.ToDecimal(result);
                                    }
                                    else
                                    {
                                        transaction.Rollback();
                                        return Json(new { success = false, message = $"Menu item {item.MenuItemId} not found." });
                                    }
                                }
                                
                                // Insert the new order item
                                using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"
                                    INSERT INTO OrderItems 
                                    (OrderId, MenuItemId, Quantity, UnitPrice, Subtotal, Status, SpecialInstructions, CreatedAt) 
                                    VALUES 
                                    (@OrderId, @MenuItemId, @Quantity, @UnitPrice, @Subtotal, 0, @SpecialInstructions, GETDATE());
                                    
                                    SELECT SCOPE_IDENTITY();", connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@OrderId", orderId);
                                    command.Parameters.AddWithValue("@MenuItemId", item.MenuItemId.Value);
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@UnitPrice", unitPrice);
                                    command.Parameters.AddWithValue("@Subtotal", unitPrice * item.Quantity);
                                    command.Parameters.AddWithValue("@SpecialInstructions", 
                                        string.IsNullOrEmpty(item.SpecialInstructions) ? DBNull.Value : (object)item.SpecialInstructions);
                                    
                                    // Get the new item ID
                                    var newItemId = Convert.ToInt32(command.ExecuteScalar());
                                    item.OrderItemId = newItemId; // Update the model with the real ID
                                }
                            }
                            
                            // Recalculate order totals and GST (handles both BAR inclusive and Foods exclusive)
                            UpdateOrderFinancials(orderId, connection, transaction);
                            
                            transaction.Commit();
                            return Json(new { success = true, message = "All items updated successfully." });
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            return Json(new { success = false, message = "Error updating items: " + ex.Message });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating items: " + ex.Message });
            }
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SubmitOrder(int orderId)
        {
            try
            {
                // Only update the order details and calculate totals
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Update order items prices and subtotals to ensure they're current
                            using (var command = new Microsoft.Data.SqlClient.SqlCommand(
                                @"UPDATE OrderItems 
                                  SET Subtotal = Quantity * UnitPrice 
                                  WHERE OrderId = @OrderId", 
                                connection, transaction))
                            {
                                command.Parameters.AddWithValue("@OrderId", orderId);
                                command.ExecuteNonQuery();
                            }
                            
                            // Recalculate order totals based on current items
                            using (var command = new Microsoft.Data.SqlClient.SqlCommand(
                                @"UPDATE Orders
                                  SET Subtotal = (SELECT SUM(Subtotal) FROM OrderItems WHERE OrderId = @OrderId),
                                      TotalAmount = (SELECT SUM(Subtotal) FROM OrderItems WHERE OrderId = @OrderId) + 
                                                    ISNULL(TaxAmount,0) + ISNULL(TipAmount,0) - ISNULL(DiscountAmount,0),
                                      UpdatedAt = GETDATE()
                                  WHERE Id = @OrderId", 
                                connection, transaction))
                            {
                                command.Parameters.AddWithValue("@OrderId", orderId);
                                command.ExecuteNonQuery();
                            }
                            
                            // Recalculate and persist GST fields on submit
                            UpdateOrderFinancials(orderId, connection, transaction);
                            
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Failed to update order details: " + ex.Message);
                        }
                    }
                }
                
                TempData["SuccessMessage"] = "Order details saved successfully.";
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error submitting order: " + ex.Message;
                return RedirectToAction("Details", new { id = orderId });
            }
        }
        
        // Helper method to seat guests at a table and return the turnover ID
        private int SeatGuestsAtTable(int tableId, string guestName, int partySize, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction)
        {
            int turnoverId = 0;
            
            // First, change table status to occupied
            using (Microsoft.Data.SqlClient.SqlCommand updateTableCmd = new Microsoft.Data.SqlClient.SqlCommand(
                "UPDATE Tables SET Status = 2 WHERE Id = @TableId", connection, transaction))
            {
                updateTableCmd.Parameters.AddWithValue("@TableId", tableId);
                updateTableCmd.ExecuteNonQuery();
            }
            
            // Then create a new turnover record
            using (Microsoft.Data.SqlClient.SqlCommand createTurnoverCmd = new Microsoft.Data.SqlClient.SqlCommand(
                @"INSERT INTO TableTurnovers (TableId, GuestName, PartySize, SeatedAt, Status)
                  OUTPUT INSERTED.Id
                  VALUES (@TableId, @GuestName, @PartySize, GETDATE(), 0)", connection, transaction))
            {
                createTurnoverCmd.Parameters.AddWithValue("@TableId", tableId);
                createTurnoverCmd.Parameters.AddWithValue("@GuestName", guestName);
                createTurnoverCmd.Parameters.AddWithValue("@PartySize", partySize);
                turnoverId = Convert.ToInt32(createTurnoverCmd.ExecuteScalar());
            }
            
            return turnoverId;
        }
        
        #region BOT (Beverage Order Ticket) Helper Methods

        /// <summary>
        /// Create BOT for beverage items
        /// </summary>
        private int CreateBOT(int orderId, List<int> barItemIds, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction)
        {
            try
            {
                // Get next BOT number
                string botNumber = null;
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("GetNextBOTNumber", connection, transaction))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    botNumber = (string)cmd.ExecuteScalar();
                }

                // Get order details
                string orderNumber = null, tableName = null, guestName = null, serverName = null;
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT o.OrderNumber, t.Name as TableName, o.GuestName, u.UserName as ServerName
                    FROM Orders o
                    LEFT JOIN Tables t ON o.TableId = t.Id
                    LEFT JOIN AspNetUsers u ON o.UserId = u.Id
                    WHERE o.Id = @OrderId", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            orderNumber = reader.IsDBNull(0) ? null : reader.GetString(0);
                            tableName = reader.IsDBNull(1) ? null : reader.GetString(1);
                            guestName = reader.IsDBNull(2) ? null : reader.GetString(2);
                            serverName = reader.IsDBNull(3) ? null : reader.GetString(3);
                        }
                    }
                }

                // Calculate totals for BOT items
                decimal subtotal = 0, taxAmount = 0, totalAmount = 0;
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT SUM(oi.Quantity * oi.Price) as Subtotal,
                           SUM(oi.Quantity * oi.Price * ISNULL(mi.GST_Perc, 0) / 100) as TaxAmount
                    FROM OrderItems oi
                    INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                    WHERE oi.Id IN (" + string.Join(",", barItemIds) + ")", connection, transaction))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            subtotal = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                            taxAmount = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            totalAmount = subtotal + taxAmount;
                        }
                    }
                }

                // Insert BOT Header with KitchenStation
                int botId = 0;
                
                // Check if KitchenStation column exists in BOT_Header
                bool hasKitchenStationColumn = false;
                using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM sys.columns 
                        WHERE object_id = OBJECT_ID('dbo.BOT_Header') AND name = 'KitchenStation'
                    ) THEN 1 ELSE 0 END", connection, transaction))
                {
                    hasKitchenStationColumn = (int)checkCmd.ExecuteScalar() == 1;
                }
                
                string insertSql = hasKitchenStationColumn
                    ? @"INSERT INTO BOT_Header (BOT_No, OrderId, OrderNumber, TableName, GuestName, ServerName, 
                                           KitchenStation, Status, SubtotalAmount, TaxAmount, TotalAmount, 
                                           CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                        VALUES (@BOT_No, @OrderId, @OrderNumber, @TableName, @GuestName, @ServerName,
                                'BAR', 0, @Subtotal, @Tax, @Total,
                                GETDATE(), @CreatedBy, GETDATE(), @UpdatedBy);
                        SELECT SCOPE_IDENTITY();"
                    : @"INSERT INTO BOT_Header (BOT_No, OrderId, OrderNumber, TableName, GuestName, ServerName, 
                                           Status, SubtotalAmount, TaxAmount, TotalAmount, 
                                           CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
                        VALUES (@BOT_No, @OrderId, @OrderNumber, @TableName, @GuestName, @ServerName,
                                0, @Subtotal, @Tax, @Total,
                                GETDATE(), @CreatedBy, GETDATE(), @UpdatedBy);
                        SELECT SCOPE_IDENTITY();";
                
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(insertSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@BOT_No", botNumber);
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    cmd.Parameters.AddWithValue("@OrderNumber", orderNumber ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TableName", tableName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@GuestName", guestName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ServerName", serverName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Subtotal", subtotal);
                    cmd.Parameters.AddWithValue("@Tax", taxAmount);
                    cmd.Parameters.AddWithValue("@Total", totalAmount);
                    cmd.Parameters.AddWithValue("@CreatedBy", User.Identity?.Name ?? "System");
                    cmd.Parameters.AddWithValue("@UpdatedBy", User.Identity?.Name ?? "System");
                    
                    botId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Insert BOT Detail items
                foreach (int itemId in barItemIds)
                {
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        INSERT INTO BOT_Detail (BOT_ID, OrderItemId, MenuItemId, MenuItemName, Quantity, 
                                               UnitPrice, Amount, TaxRate, TaxAmount, IsAlcoholic, 
                                               SpecialInstructions, Status)
                        SELECT @BOT_ID, oi.Id, oi.MenuItemId, mi.Name, oi.Quantity,
                               oi.Price, oi.Quantity * oi.Price, ISNULL(mi.GST_Perc, 0),
                               oi.Quantity * oi.Price * ISNULL(mi.GST_Perc, 0) / 100,
                               ISNULL(mi.IsAlcoholic, 0), oi.SpecialInstructions, 0
                        FROM OrderItems oi
                        INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                        WHERE oi.Id = @ItemId", connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@BOT_ID", botId);
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Log audit
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    INSERT INTO BOT_Audit (BOT_ID, BOT_No, Action, NewStatus, UserName, Timestamp)
                    VALUES (@BOT_ID, @BOT_No, 'CREATE', 0, @UserName, GETDATE())", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@BOT_ID", botId);
                    cmd.Parameters.AddWithValue("@BOT_No", botNumber);
                    cmd.Parameters.AddWithValue("@UserName", User.Identity?.Name ?? "System");
                    cmd.ExecuteNonQuery();
                }

                return botId;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create BOT: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get "Bar" menu item group ID
        /// </summary>
        private int? GetBarMenuItemGroupId(Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction)
        {
            try
            {
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT ID FROM menuitemgroup 
                    WHERE itemgroup = 'Bar' AND is_active = 1", connection, transaction))
                {
                    var result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : (int?)null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Classify order items as Bar (BOT) or Food (KOT)
        /// </summary>
        private (List<int> barItems, List<int> foodItems) ClassifyOrderItems(List<int> itemIds, Microsoft.Data.SqlClient.SqlConnection connection, Microsoft.Data.SqlClient.SqlTransaction transaction)
        {
            var barItems = new List<int>();
            var foodItems = new List<int>();

            int? barGroupId = GetBarMenuItemGroupId(connection, transaction);
            if (!barGroupId.HasValue)
            {
                // If no Bar group exists, all items are food
                foodItems.AddRange(itemIds);
                return (barItems, foodItems);
            }

            foreach (int itemId in itemIds)
            {
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT mi.menuitemgroupID
                    FROM OrderItems oi
                    INNER JOIN MenuItems mi ON oi.MenuItemId = mi.Id
                    WHERE oi.Id = @ItemId", connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                    var result = cmd.ExecuteScalar();
                    
                    if (result != null && result != DBNull.Value)
                    {
                        int groupId = Convert.ToInt32(result);
                        if (groupId == barGroupId.Value)
                        {
                            barItems.Add(itemId);
                        }
                        else
                        {
                            foodItems.Add(itemId);
                        }
                    }
                    else
                    {
                        // No group assigned, treat as food
                        foodItems.Add(itemId);
                    }
                }
            }

            return (barItems, foodItems);
        }

        #endregion

        // Menu Items & Estimation Page  
        public IActionResult Estimation()
        {
            ViewData["Title"] = "Menu Items & Estimation";
            return View();
        }

        /// <summary>
        /// API endpoint to generate encrypted order details URL
        /// </summary>
        [HttpGet]
        public JsonResult GenerateEncryptedOrderDetailsUrl(int id, bool fromBar = false)
        {
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    ["id"] = id.ToString(),
                    ["fromBar"] = fromBar.ToString()
                };

                var encryptedToken = _encryptionService.EncryptParameters(parameters);
                var encryptedUrl = $"/Order/Details?token={Uri.EscapeDataString(encryptedToken)}";
                
                return Json(new { success = true, url = encryptedUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Failed to generate encrypted URL" });
            }
        }
    }
}
