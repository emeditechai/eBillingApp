using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.ViewModels;
using System.Data;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ReportsController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        [HttpGet]
        public async Task<IActionResult> Sales()
        {
            ViewData["Title"] = "Sales Reports";
            
            var viewModel = new SalesReportViewModel();
            
            // Load available users for the filter dropdown
            await LoadAvailableUsersAsync(viewModel);
            
            // Load default report (last 30 days)
            await LoadSalesReportDataAsync(viewModel);
            
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Sales(SalesReportFilter filter)
        {
            ViewData["Title"] = "Sales Reports";
            
            var viewModel = new SalesReportViewModel
            {
                Filter = filter
            };
            
            // Load available users for the filter dropdown
            await LoadAvailableUsersAsync(viewModel);
            
            // Load report data based on filter
            await LoadSalesReportDataAsync(viewModel);
            
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            ViewData["Title"] = "Order Reports";
            
            var viewModel = new OrderReportViewModel();
            
            // Load available users for the filter dropdown
            await LoadOrderReportUsersAsync(viewModel);
            
            // Load default report (today's orders)
            await LoadOrderReportDataAsync(viewModel);
            
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Orders(OrderReportFilter filter, int page = 1)
        {
            ViewData["Title"] = "Order Reports";
            
            var viewModel = new OrderReportViewModel
            {
                Filter = filter,
                CurrentPage = page
            };
            
            // Load available users for the filter dropdown
            await LoadOrderReportUsersAsync(viewModel);
            
            // Load report data based on filter
            await LoadOrderReportDataAsync(viewModel);
            
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Menu()
        {
            ViewData["Title"] = "Menu Analysis";
            var viewModel = new MenuReportViewModel();
            // Load default report (last 30 days)
            await LoadMenuReportDataAsync(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Menu(MenuReportFilter filter)
        {
            ViewData["Title"] = "Menu Analysis";
            var viewModel = new MenuReportViewModel
            {
                Filter = filter
            };

            await LoadMenuReportDataAsync(viewModel);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Customers()
        {
            ViewData["Title"] = "Customer Reports";
            var model = new CustomerReportViewModel();
            await LoadCustomerReportDataAsync(model);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Customers(CustomerReportFilter filter)
        {
            ViewData["Title"] = "Customer Reports";
            var model = new CustomerReportViewModel { Filter = filter };
            await LoadCustomerReportDataAsync(model);
            return View(model);
        }

        public IActionResult Financial()
        {
            ViewData["Title"] = "Financial Summary";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Kitchen(DateTime? from, DateTime? to, string station)
        {
            ViewData["Title"] = "Kitchen KOT Report";
            var model = new KitchenReportViewModel();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("usp_GetKitchenKOTReport", connection) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@FromDate", from.HasValue ? (object)from.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", to.HasValue ? (object)to.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@Station", !string.IsNullOrWhiteSpace(station) ? (object)station : DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    model.Items.Add(new KOTItem
                    {
                        OrderId = reader.GetInt32("OrderId"),
                        OrderNumber = reader.GetString("OrderNumber"),
                        TableName = reader.IsDBNull("TableName") ? "" : reader.GetString("TableName"),
                        ItemName = reader.IsDBNull("ItemName") ? "" : reader.GetString("ItemName"),
                        Quantity = reader.IsDBNull("Quantity") ? 0 : reader.GetInt32("Quantity"),
                        Station = reader.IsDBNull("Station") ? "" : reader.GetString("Station"),
                        Status = reader.IsDBNull("Status") ? "" : reader.GetString("Status"),
                        RequestedAt = reader.IsDBNull("RequestedAt") ? DateTime.MinValue : reader.GetDateTime("RequestedAt")
                    });
                }

                // Load stations list
                using var cmd2 = new SqlCommand(@"SELECT DISTINCT s.Name FROM Stations s ORDER BY s.Name", connection);
                using var reader2 = await cmd2.ExecuteReaderAsync();
                while (await reader2.ReadAsync()) model.AvailableStations.Add(reader2.GetString(0));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading kitchen report: {ex.Message}");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> KitchenExport(DateTime? from, DateTime? to, string station)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("OrderNumber,TableName,ItemName,Quantity,Station,Status,RequestedAt");

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("usp_GetKitchenKOTReport", connection) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@FromDate", from.HasValue ? (object)from.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", to.HasValue ? (object)to.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@Station", !string.IsNullOrWhiteSpace(station) ? (object)station : DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var line = string.Format("\"{0}\",\"{1}\",\"{2}\",{3},\"{4}\",\"{5}\",\"{6}\"",
                        reader.GetString(reader.GetOrdinal("OrderNumber")),
                        reader.IsDBNull(reader.GetOrdinal("TableName")) ? "" : reader.GetString(reader.GetOrdinal("TableName")),
                        reader.IsDBNull(reader.GetOrdinal("ItemName")) ? "" : reader.GetString(reader.GetOrdinal("ItemName")),
                        reader.IsDBNull(reader.GetOrdinal("Quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("Quantity")),
                        reader.IsDBNull(reader.GetOrdinal("Station")) ? "" : reader.GetString(reader.GetOrdinal("Station")),
                        reader.IsDBNull(reader.GetOrdinal("Status")) ? "" : reader.GetString(reader.GetOrdinal("Status")),
                        reader.IsDBNull(reader.GetOrdinal("RequestedAt")) ? "" : reader.GetDateTime(reader.GetOrdinal("RequestedAt")).ToString("o")
                    );
                    sb.AppendLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting kitchen report: {ex.Message}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "KitchenKOTReport.csv");
        }

        private async Task LoadAvailableUsersAsync(SalesReportViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqlCommand(@"
                    SELECT DISTINCT u.Id, u.FirstName, u.LastName, u.Username 
                    FROM Users u 
                    INNER JOIN Orders o ON u.Id = o.UserId 
                    WHERE u.FirstName IS NOT NULL AND u.LastName IS NOT NULL
                    ORDER BY u.FirstName, u.LastName", connection);
                
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    viewModel.AvailableUsers.Add(new UserSelectItem
                    {
                        Id = reader.GetInt32("Id"),
                        Name = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                        Username = reader.GetString("Username")
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error and continue with empty user list
                Console.WriteLine($"Error loading users: {ex.Message}");
            }
        }

        private async Task LoadSalesReportDataAsync(SalesReportViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqlCommand("usp_GetSalesReport", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                
                // Add parameters
                command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) 
                { 
                    Value = viewModel.Filter.StartDate?.Date ?? DateTime.Today.AddDays(-30) 
                });
                command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) 
                { 
                    Value = viewModel.Filter.EndDate?.Date ?? DateTime.Today 
                });
                command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) 
                { 
                    Value = viewModel.Filter.UserId.HasValue ? viewModel.Filter.UserId.Value : DBNull.Value 
                });
                
                // Clear existing collections to avoid accumulation when posting back
                viewModel.DailySales.Clear();
                viewModel.TopMenuItems.Clear();
                viewModel.ServerPerformance.Clear();
                viewModel.OrderStatusData.Clear();
                viewModel.HourlySalesPattern.Clear();

                using var reader = await command.ExecuteReaderAsync();
                
                // Read Summary Statistics (First Result Set)
                if (await reader.ReadAsync())
                {
                    viewModel.Summary = new SalesReportSummary
                    {
                        TotalOrders = reader.GetInt32("TotalOrders"),
                        TotalSales = reader.GetDecimal("TotalSales"),
                        AverageOrderValue = reader.GetDecimal("AverageOrderValue"),
                        TotalSubtotal = reader.GetDecimal("TotalSubtotal"),
                        TotalTax = reader.GetDecimal("TotalTax"),
                        TotalTips = reader.GetDecimal("TotalTips"),
                        TotalDiscounts = reader.GetDecimal("TotalDiscounts"),
                        CompletedOrders = reader.GetInt32("CompletedOrders"),
                        CancelledOrders = reader.GetInt32("CancelledOrders")
                    };
                }
                
                // Read Daily Sales (Second Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.DailySales.Add(new DailySalesData
                        {
                            SalesDate = reader.GetDateTime("SalesDate"),
                            OrderCount = reader.GetInt32("OrderCount"),
                            DailySales = reader.GetDecimal("DailySales"),
                            AvgOrderValue = reader.GetDecimal("AvgOrderValue")
                        });
                    }
                }
                
                // Read Top Menu Items (Third Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        try
                        {
                            // Defensive: ensure no null related exceptions break entire report
                            viewModel.TopMenuItems.Add(new TopMenuItem
                            {
                                ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? "" : reader.GetString(reader.GetOrdinal("ItemName")),
                                MenuItemId = reader.IsDBNull(reader.GetOrdinal("MenuItemId")) ? 0 : reader.GetInt32(reader.GetOrdinal("MenuItemId")),
                                TotalQuantitySold = reader.IsDBNull(reader.GetOrdinal("TotalQuantitySold")) ? 0 : reader.GetInt32(reader.GetOrdinal("TotalQuantitySold")),
                                TotalRevenue = reader.IsDBNull(reader.GetOrdinal("TotalRevenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                                AveragePrice = reader.IsDBNull(reader.GetOrdinal("AveragePrice")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AveragePrice")),
                                OrderCount = reader.IsDBNull(reader.GetOrdinal("OrderCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("OrderCount"))
                            });
                        }
                        catch (Exception exItem)
                        {
                            Console.WriteLine($"TopMenuItems row skipped: {exItem.Message}");
                        }
                    }
                }
                
                // Read Server Performance (Fourth Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.ServerPerformance.Add(new ServerPerformance
                        {
                            ServerName = reader.GetString("ServerName"),
                            Username = reader.GetString("Username"),
                            UserId = reader.IsDBNull("UserId") ? null : reader.GetInt32("UserId"),
                            OrderCount = reader.GetInt32("OrderCount"),
                            TotalSales = reader.GetDecimal("TotalSales"),
                            AvgOrderValue = reader.GetDecimal("AvgOrderValue"),
                            TotalTips = reader.GetDecimal("TotalTips")
                        });
                    }
                }
                
                // Read Order Status Data (Fifth Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.OrderStatusData.Add(new OrderStatusData
                        {
                            OrderStatus = reader.GetString("OrderStatus"),
                            OrderCount = reader.GetInt32("OrderCount"),
                            TotalAmount = reader.GetDecimal("TotalAmount"),
                            Percentage = reader.GetDecimal("Percentage")
                        });
                    }
                }
                
                // Read Hourly Sales Pattern (Sixth Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.HourlySalesPattern.Add(new HourlySalesData
                        {
                            HourOfDay = reader.GetInt32("HourOfDay"),
                            OrderCount = reader.GetInt32("OrderCount"),
                            HourlySales = reader.GetDecimal("HourlySales"),
                            AvgOrderValue = reader.GetDecimal("AvgOrderValue")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and provide default empty data
                Console.WriteLine($"Error loading sales report data: {ex.Message}");
                viewModel.Summary = new SalesReportSummary();
            }
        }

        private async Task LoadOrderReportUsersAsync(OrderReportViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqlCommand(@"
                    SELECT DISTINCT u.Id, u.FirstName, u.LastName 
                    FROM Users u 
                    INNER JOIN Orders o ON u.Id = o.UserId 
                    WHERE u.IsActive = 1 AND u.FirstName IS NOT NULL AND u.LastName IS NOT NULL
                    ORDER BY u.FirstName, u.LastName", connection);
                
                using var reader = await command.ExecuteReaderAsync();
                
                // Add "All Users" option
                viewModel.AvailableUsers.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = "",
                    Text = "All Users"
                });
                
                while (await reader.ReadAsync())
                {
                    viewModel.AvailableUsers.Add(new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = reader.GetInt32("Id").ToString(),
                        Text = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading users for order report: {ex.Message}");
            }
        }

        private async Task LoadOrderReportDataAsync(OrderReportViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                
                using var command = new SqlCommand("usp_GetOrderReport", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };
                
                // Add parameters
                command.Parameters.AddWithValue("@FromDate", viewModel.Filter.FromDate);
                command.Parameters.AddWithValue("@ToDate", viewModel.Filter.ToDate);
                command.Parameters.AddWithValue("@UserId", viewModel.Filter.UserId.HasValue ? (object)viewModel.Filter.UserId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@Status", viewModel.Filter.Status.HasValue ? (object)viewModel.Filter.Status.Value : DBNull.Value);
                command.Parameters.AddWithValue("@OrderType", viewModel.Filter.OrderType.HasValue ? (object)viewModel.Filter.OrderType.Value : DBNull.Value);
                command.Parameters.AddWithValue("@SearchTerm", !string.IsNullOrWhiteSpace(viewModel.Filter.SearchTerm) ? (object)viewModel.Filter.SearchTerm : DBNull.Value);
                command.Parameters.AddWithValue("@PageNumber", viewModel.CurrentPage);
                command.Parameters.AddWithValue("@PageSize", viewModel.Filter.PageSize);
                
                using var reader = await command.ExecuteReaderAsync();
                
                // Read Summary Statistics (First Result Set)
                if (await reader.ReadAsync())
                {
                    viewModel.Summary = new OrderReportSummary
                    {
                        TotalOrders = reader.GetInt32("TotalOrders"),
                        PendingOrders = reader.GetInt32("PendingOrders"),
                        InProgressOrders = reader.GetInt32("InProgressOrders"),
                        CompletedOrders = reader.GetInt32("CompletedOrders"),
                        CancelledOrders = reader.GetInt32("CancelledOrders"),
                        TotalRevenue = reader.GetDecimal("TotalRevenue"),
                        AverageOrderValue = reader.GetDecimal("AverageOrderValue"),
                        DineInOrders = reader.GetInt32("DineInOrders"),
                        TakeawayOrders = reader.GetInt32("TakeawayOrders"),
                        DeliveryOrders = reader.GetInt32("DeliveryOrders")
                    };
                }
                
                // Read Order Details (Second Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.Orders.Add(new OrderReportItem
                        {
                            Id = reader.GetInt32("Id"),
                            OrderNumber = reader.GetString("OrderNumber"),
                            CustomerName = reader.IsDBNull("CustomerName") ? "" : reader.GetString("CustomerName"),
                            CustomerPhone = reader.IsDBNull("CustomerPhone") ? "" : reader.GetString("CustomerPhone"),
                            WaiterName = reader.GetString("WaiterName"),
                            OrderType = reader.GetInt32("OrderType"),
                            OrderTypeName = reader.GetString("OrderTypeName"),
                            Status = reader.GetInt32("Status"),
                            StatusName = reader.GetString("StatusName"),
                            Subtotal = reader.GetDecimal("Subtotal"),
                            TaxAmount = reader.GetDecimal("TaxAmount"),
                            TipAmount = reader.GetDecimal("TipAmount"),
                            DiscountAmount = reader.GetDecimal("DiscountAmount"),
                            TotalAmount = reader.GetDecimal("TotalAmount"),
                            SpecialInstructions = reader.IsDBNull("SpecialInstructions") ? "" : reader.GetString("SpecialInstructions"),
                            CreatedAt = reader.GetDateTime("CreatedAt"),
                            CompletedAt = reader.IsDBNull("CompletedAt") ? null : reader.GetDateTime("CompletedAt"),
                            PreparationTimeMinutes = reader.IsDBNull("PreparationTimeMinutes") ? null : reader.GetInt32("PreparationTimeMinutes"),
                            ItemCount = reader.GetInt32("ItemCount"),
                            TotalQuantity = reader.GetInt32("TotalQuantity")
                        });
                    }
                }
                
                // Read Total Count (Third Result Set)
                if (await reader.NextResultAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        viewModel.TotalCount = reader.GetInt32("TotalCount");
                    }
                }
                
                // Skip Users result set (Fourth Result Set) - already loaded separately
                if (await reader.NextResultAsync())
                {
                    // Skip this result set
                }
                
                // Read Hourly Distribution (Fifth Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.HourlyDistribution.Add(new HourlyOrderDistribution
                        {
                            Hour = reader.GetInt32("Hour"),
                            OrderCount = reader.GetInt32("OrderCount"),
                            HourlyRevenue = reader.GetDecimal("HourlyRevenue")
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading order report data: {ex.Message}");
                viewModel.Summary = new OrderReportSummary();
            }
        }

        [HttpGet]
        public async Task<IActionResult> DiscountReport()
        {
            ViewData["Title"] = "Discount Report";
            var model = new DiscountReportViewModel();
            await LoadDiscountReportAsync(model);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> DiscountReport(DiscountReportFilter filter)
        {
            ViewData["Title"] = "Discount Report";
            var model = new DiscountReportViewModel { Filter = filter };
            await LoadDiscountReportAsync(model);
            return View(model);
        }

        private async Task LoadDiscountReportAsync(DiscountReportViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("usp_GetDiscountReport", connection)
                { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("@StartDate", (object?)model.Filter.StartDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@EndDate", (object?)model.Filter.EndDate?.Date ?? DBNull.Value);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.Summary = new DiscountReportSummary
                    {
                        TotalDiscountedOrders = reader.GetInt32(reader.GetOrdinal("TotalDiscountedOrders")),
                        TotalDiscountAmount = reader.GetDecimal(reader.GetOrdinal("TotalDiscountAmount")),
                        AvgDiscountPerOrder = reader.GetDecimal(reader.GetOrdinal("AvgDiscountPerOrder")),
                        MaxDiscount = reader.GetDecimal(reader.GetOrdinal("MaxDiscount")),
                        MinDiscount = reader.GetDecimal(reader.GetOrdinal("MinDiscount")),
                        TotalGrossBeforeDiscount = reader.GetDecimal(reader.GetOrdinal("TotalGrossBeforeDiscount")),
                        NetAfterDiscount = reader.GetDecimal(reader.GetOrdinal("NetAfterDiscount"))
                    };
                }
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Rows.Add(new DiscountReportRow
                        {
                            OrderId = reader.GetInt32(reader.GetOrdinal("OrderId")),
                            OrderNumber = reader.GetString(reader.GetOrdinal("OrderNumber")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            Subtotal = reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                            TaxAmount = reader.GetDecimal(reader.GetOrdinal("TaxAmount")),
                            TipAmount = reader.GetDecimal(reader.GetOrdinal("TipAmount")),
                            TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                            GrossAmount = reader.GetDecimal(reader.GetOrdinal("GrossAmount")),
                            DiscountApplied = reader.GetDecimal(reader.GetOrdinal("DiscountApplied")),
                            Username = reader.IsDBNull(reader.GetOrdinal("Username")) ? string.Empty : reader.GetString(reader.GetOrdinal("Username")),
                            FirstName = reader.IsDBNull(reader.GetOrdinal("FirstName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FirstName")),
                            LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? string.Empty : reader.GetString(reader.GetOrdinal("LastName")),
                            Status = reader.GetInt32(reader.GetOrdinal("Status"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading discount report: {ex.Message}");
            }
        }

        public async Task<IActionResult> GSTBreakup()
        {
            ViewData["Title"] = "GST Breakup Report";
            var model = new GSTBreakupReportViewModel();
            await LoadGSTBreakupReportAsync(model);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> GSTBreakup(GSTBreakupReportFilter filter)
        {
            ViewData["Title"] = "GST Breakup Report";
            var model = new GSTBreakupReportViewModel { Filter = filter };
            await LoadGSTBreakupReportAsync(model);
            return View(model);
        }

        private async Task LoadGSTBreakupReportAsync(GSTBreakupReportViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("usp_GetGSTBreakupReport", connection)
                { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("@StartDate", (object?)model.Filter.StartDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@EndDate", (object?)model.Filter.EndDate?.Date ?? DBNull.Value);
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.Summary = new GSTBreakupReportSummary
                    {
                        InvoiceCount = reader.IsDBNull(reader.GetOrdinal("InvoiceCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("InvoiceCount")),
                        TotalTaxableValue = reader.IsDBNull(reader.GetOrdinal("TotalTaxableValue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalTaxableValue")),
                        TotalDiscount = reader.IsDBNull(reader.GetOrdinal("TotalDiscount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalDiscount")),
                        TotalCGST = reader.IsDBNull(reader.GetOrdinal("TotalCGST")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalCGST")),
                        TotalSGST = reader.IsDBNull(reader.GetOrdinal("TotalSGST")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalSGST")),
                        NetAmount = reader.IsDBNull(reader.GetOrdinal("NetAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("NetAmount")),
                        AverageTaxablePerInvoice = reader.IsDBNull(reader.GetOrdinal("AverageTaxablePerInvoice")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AverageTaxablePerInvoice")),
                        AverageGSTPerInvoice = reader.IsDBNull(reader.GetOrdinal("AverageGSTPerInvoice")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AverageGSTPerInvoice"))
                    };
                }
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Rows.Add(new GSTBreakupReportRow
                        {
                            PaymentDate = reader.IsDBNull(reader.GetOrdinal("PaymentDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("PaymentDate")),
                            OrderNumber = reader.IsDBNull(reader.GetOrdinal("OrderNumber")) ? string.Empty : reader.GetString(reader.GetOrdinal("OrderNumber")),
                            TaxableValue = reader.IsDBNull(reader.GetOrdinal("TaxableValue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TaxableValue")),
                            DiscountAmount = reader.IsDBNull(reader.GetOrdinal("DiscountAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            CGSTPercentage = reader.IsDBNull(reader.GetOrdinal("CGSTPercentage")) ? 0 : reader.GetDecimal(reader.GetOrdinal("CGSTPercentage")),
                            CGSTAmount = reader.IsDBNull(reader.GetOrdinal("CGSTAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("CGSTAmount")),
                            SGSTPercentage = reader.IsDBNull(reader.GetOrdinal("SGSTPercentage")) ? 0 : reader.GetDecimal(reader.GetOrdinal("SGSTPercentage")),
                            SGSTAmount = reader.IsDBNull(reader.GetOrdinal("SGSTAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("SGSTAmount")),
                            InvoiceTotal = reader.IsDBNull(reader.GetOrdinal("InvoiceTotal")) ? 0 : reader.GetDecimal(reader.GetOrdinal("InvoiceTotal"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading GST Breakup report: {ex.Message}");
            }
        }

        private async Task LoadCustomerReportDataAsync(CustomerReportViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("usp_GetCustomerAnalysis", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("@FromDate", (object?)model.Filter.From?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@ToDate", (object?)model.Filter.To?.Date ?? DBNull.Value);

                // Clear existing
                model.TopCustomers.Clear();
                model.VisitFrequencies.Clear();
                model.LoyaltyStats.Clear();
                model.Demographics.Clear();

                using var reader = await command.ExecuteReaderAsync();

                // Summary
                if (await reader.ReadAsync())
                {
                    model.Summary = new CustomerSummary
                    {
                        TotalCustomers = reader.IsDBNull(reader.GetOrdinal("TotalCustomers")) ? 0 : reader.GetInt32(reader.GetOrdinal("TotalCustomers")),
                        NewCustomers = reader.IsDBNull(reader.GetOrdinal("NewCustomers")) ? 0 : reader.GetInt32(reader.GetOrdinal("NewCustomers")),
                        ReturningCustomers = reader.IsDBNull(reader.GetOrdinal("ReturningCustomers")) ? 0 : reader.GetInt32(reader.GetOrdinal("ReturningCustomers")),
                        AverageVisitsPerCustomer = reader.IsDBNull(reader.GetOrdinal("AverageVisitsPerCustomer")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AverageVisitsPerCustomer")),
                        TotalRevenue = reader.IsDBNull(reader.GetOrdinal("TotalRevenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalRevenue"))
                    };
                }

                // Top Customers
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.TopCustomers.Add(new TopCustomer
                        {
                            CustomerId = reader.IsDBNull(reader.GetOrdinal("CustomerId")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("CustomerId")),
                            Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? string.Empty : reader.GetString(reader.GetOrdinal("Name")),
                            Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? string.Empty : reader.GetString(reader.GetOrdinal("Phone")),
                            Visits = reader.IsDBNull(reader.GetOrdinal("Visits")) ? 0 : reader.GetInt32(reader.GetOrdinal("Visits")),
                            Revenue = reader.IsDBNull(reader.GetOrdinal("Revenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Revenue")),
                            LTV = reader.IsDBNull(reader.GetOrdinal("LTV")) ? 0 : reader.GetDecimal(reader.GetOrdinal("LTV"))
                        });
                    }
                }

                // Visit Frequency
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.VisitFrequencies.Add(new VisitFrequency
                        {
                            Period = reader.IsDBNull(reader.GetOrdinal("PeriodLabel")) ? string.Empty : reader.GetString(reader.GetOrdinal("PeriodLabel")),
                            Visits = reader.IsDBNull(reader.GetOrdinal("Visits")) ? 0 : reader.GetInt32(reader.GetOrdinal("Visits")),
                            Revenue = reader.IsDBNull(reader.GetOrdinal("Revenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Revenue"))
                        });
                    }
                }

                // Loyalty buckets
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.LoyaltyStats.Add(new LoyaltyBucket
                        {
                            Bucket = reader.IsDBNull(reader.GetOrdinal("Bucket")) ? string.Empty : reader.GetString(reader.GetOrdinal("Bucket")),
                            CustomerCount = reader.IsDBNull(reader.GetOrdinal("CustomerCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("CustomerCount"))
                        });
                    }
                }

                // Demographics
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        model.Demographics.Add(new DemographicRow
                        {
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? string.Empty : reader.GetString(reader.GetOrdinal("Category")),
                            Count = reader.IsDBNull(reader.GetOrdinal("Count")) ? 0 : reader.GetInt32(reader.GetOrdinal("Count"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading customer report: {ex.Message}");
            }
        }

        private async Task LoadMenuReportDataAsync(MenuReportViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("usp_GetMenuAnalysis", connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.Date) { Value = (object?)viewModel.Filter.From?.Date ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.Date) { Value = (object?)viewModel.Filter.To?.Date ?? DBNull.Value });

                // Clear existing collections
                viewModel.TopItems.Clear();
                viewModel.CategoryPerformance.Clear();
                viewModel.SeasonalTrends.Clear();
                viewModel.Recommendations.Clear();

                using var reader = await command.ExecuteReaderAsync();

                // Summary (first result set)
                if (await reader.ReadAsync())
                {
                    viewModel.Summary = new MenuSummary
                    {
                        TotalItemsSold = reader.IsDBNull(reader.GetOrdinal("TotalItemsSold")) ? 0 : reader.GetInt32(reader.GetOrdinal("TotalItemsSold")),
                        TotalRevenue = reader.IsDBNull(reader.GetOrdinal("TotalRevenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                        AveragePrice = reader.IsDBNull(reader.GetOrdinal("AveragePrice")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AveragePrice")),
                        OverallGP = reader.IsDBNull(reader.GetOrdinal("OverallGP")) ? 0 : reader.GetDecimal(reader.GetOrdinal("OverallGP"))
                    };
                }

                // Top Items (second result set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        try
                        {
                            viewModel.TopItems.Add(new MenuTopItem
                            {
                                MenuItemId = reader.IsDBNull(reader.GetOrdinal("MenuItemId")) ? 0 : reader.GetInt32(reader.GetOrdinal("MenuItemId")),
                                Name = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ItemName")),
                                Quantity = reader.IsDBNull(reader.GetOrdinal("QuantitySold")) ? 0 : reader.GetInt32(reader.GetOrdinal("QuantitySold")),
                                Revenue = reader.IsDBNull(reader.GetOrdinal("Revenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Revenue")),
                                Profit = reader.IsDBNull(reader.GetOrdinal("Profit")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Profit"))
                            });
                        }
                        catch (Exception exItem)
                        {
                            Console.WriteLine($"TopItems row skipped: {exItem.Message}");
                        }
                    }
                }

                // Category Performance (third result set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.CategoryPerformance.Add(new CategoryPerformance
                        {
                            Category = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? string.Empty : reader.GetString(reader.GetOrdinal("CategoryName")),
                            ItemsSold = reader.IsDBNull(reader.GetOrdinal("ItemsSold")) ? 0 : reader.GetInt32(reader.GetOrdinal("ItemsSold")),
                            Revenue = reader.IsDBNull(reader.GetOrdinal("Revenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Revenue")),
                            AverageGP = reader.IsDBNull(reader.GetOrdinal("AverageGP")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AverageGP"))
                        });
                    }
                }

                // Seasonal Trends (fourth result set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.SeasonalTrends.Add(new SeasonalTrend
                        {
                            Period = reader.IsDBNull(reader.GetOrdinal("PeriodLabel")) ? string.Empty : reader.GetString(reader.GetOrdinal("PeriodLabel")),
                            ItemsSold = reader.IsDBNull(reader.GetOrdinal("ItemsSold")) ? 0 : reader.GetInt32(reader.GetOrdinal("ItemsSold")),
                            Revenue = reader.IsDBNull(reader.GetOrdinal("Revenue")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Revenue"))
                        });
                    }
                }

                // Recommendations (fifth result set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.Recommendations.Add(new MenuRecommendation
                        {
                            Recommendation = reader.IsDBNull(reader.GetOrdinal("RecommendationText")) ? string.Empty : reader.GetString(reader.GetOrdinal("RecommendationText")),
                            Rationale = reader.IsDBNull(reader.GetOrdinal("Rationale")) ? string.Empty : reader.GetString(reader.GetOrdinal("Rationale"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading menu report data: {ex.Message}");
                viewModel.Summary = new MenuSummary();
            }
        }

        // Collection Register Report
        public async Task<IActionResult> CollectionRegister()
        {
            ViewData["Title"] = "Order Wise Payment Method Wise Collection Register";
            var model = new CollectionRegisterViewModel
            {
                Filter = new CollectionRegisterFilter
                {
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today
                }
            };
            await LoadPaymentMethodsAsync(model);
            await LoadCollectionRegisterDataAsync(model);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CollectionRegister(CollectionRegisterFilter filter)
        {
            ViewData["Title"] = "Order Wise Payment Method Wise Collection Register";
            var model = new CollectionRegisterViewModel { Filter = filter };
            await LoadPaymentMethodsAsync(model);
            await LoadCollectionRegisterDataAsync(model);
            return View(model);
        }

        private async Task LoadPaymentMethodsAsync(CollectionRegisterViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("SELECT Id, Name FROM PaymentMethods WHERE IsActive = 1 ORDER BY Name", connection);
                using var reader = await command.ExecuteReaderAsync();
                
                model.PaymentMethods.Add(new SelectListItem { Value = "", Text = "ALL Payment Methods" });
                while (await reader.ReadAsync())
                {
                    model.PaymentMethods.Add(new SelectListItem
                    {
                        Value = reader.GetInt32(0).ToString(),
                        Text = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading payment methods: {ex.Message}");
            }
        }

        private async Task LoadCollectionRegisterDataAsync(CollectionRegisterViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand("usp_GetCollectionRegister", connection)
                { CommandType = CommandType.StoredProcedure };
                
                command.Parameters.AddWithValue("@FromDate", (object?)model.Filter.FromDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@ToDate", (object?)model.Filter.ToDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@PaymentMethodId", (object?)model.Filter.PaymentMethodId ?? DBNull.Value);

                using var reader = await command.ExecuteReaderAsync();

                // Read detail rows
                while (await reader.ReadAsync())
                {
                    var row = new CollectionRegisterRow
                    {
                        OrderNo = reader.GetString(reader.GetOrdinal("OrderNo")),
                        TableNo = reader.GetString(reader.GetOrdinal("TableNo")),
                        Username = reader.GetString(reader.GetOrdinal("Username")),
                        ActualBillAmount = reader.GetDecimal(reader.GetOrdinal("ActualBillAmount")),
                        DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                        RoundOffAmount = reader.GetDecimal(reader.GetOrdinal("RoundOffAmount")),
                        ReceiptAmount = reader.GetDecimal(reader.GetOrdinal("ReceiptAmount")),
                        PaymentMethod = reader.GetString(reader.GetOrdinal("PaymentMethod")),
                        Details = reader.GetString(reader.GetOrdinal("Details")),
                        PaymentDate = reader.GetDateTime(reader.GetOrdinal("PaymentDate"))
                    };
                    model.Rows.Add(row);
                }

                // Calculate summary
                model.Summary.TotalTransactions = model.Rows.Count;
                model.Summary.TotalActualAmount = model.Rows.Sum(r => r.ActualBillAmount);
                model.Summary.TotalDiscount = model.Rows.Sum(r => r.DiscountAmount);
                model.Summary.TotalRoundOff = model.Rows.Sum(r => r.RoundOffAmount);
                model.Summary.TotalReceiptAmount = model.Rows.Sum(r => r.ReceiptAmount);
                
                // Set payment method name for display
                if (model.Filter.PaymentMethodId.HasValue)
                {
                    var selectedMethod = model.PaymentMethods.FirstOrDefault(pm => pm.Value == model.Filter.PaymentMethodId.ToString());
                    model.Filter.PaymentMethodName = selectedMethod?.Text ?? "ALL";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading collection register: {ex.Message}");
            }
        }
    }
}