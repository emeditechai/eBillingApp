using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.Models.Authorization;
using RestaurantManagementSystem.ViewModels;
using RestaurantManagementSystem.Services;
using Microsoft.AspNetCore.Http;
using SkiaSharp;
using System.Data;
using System.Linq;
using System.Security.Claims;

namespace RestaurantManagementSystem.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IDayClosingService _dayClosingService;
        private readonly RolePermissionService _permissionService;

        private static class MenuCodes
        {
            public const string Sales = "NAV_REPORTS_SALES";
            public const string Orders = "NAV_REPORTS_ORDERS";
            public const string Menu = "NAV_REPORTS_MENU";
            public const string Customers = "NAV_REPORTS_CUSTOMERS";
            public const string Financial = "NAV_REPORTS_FINANCIAL";
            public const string Kitchen = "NAV_REPORTS_KITCHEN";
            public const string Bar = "NAV_REPORTS_BAR";
            public const string Discount = "NAV_REPORTS_DISCOUNT";
            public const string Gst = "NAV_REPORTS_GST";
            public const string Collection = "NAV_REPORTS_COLLECTION";
            public const string CashClosing = "NAV_REPORTS_CASHCLOSING";
            public const string Feedback = "NAV_REPORTS_FEEDBACK";
        }

        public ReportsController(IConfiguration configuration, IDayClosingService dayClosingService, RolePermissionService permissionService)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _dayClosingService = dayClosingService;
            _permissionService = permissionService;
        }

        private async Task SetViewPermissionsAsync(string menuCode)
        {
            if (_permissionService == null)
            {
                ViewBag.ReportPermissions = PermissionSet.FullAccess;
                ViewBag.CurrentMenuCode = menuCode;
                return;
            }

            var permissions = await _permissionService.GetPermissionsForUserAsync(User, menuCode);
            ViewBag.ReportPermissions = permissions;
            ViewBag.CurrentMenuCode = menuCode;
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Sales, PermissionAction.View)]
        public async Task<IActionResult> Sales()
        {
            ViewData["Title"] = "Sales Reports";
            
            var viewModel = new SalesReportViewModel();
            var canViewAllReports = CurrentUserCanViewAllReportData();
            var currentUserId = GetCurrentUserId();
            ViewBag.CanViewAllSalesUsers = canViewAllReports;
            if (!canViewAllReports)
            {
                viewModel.Filter.UserId = currentUserId;
            }
            
            // Load available users for the filter dropdown
            await LoadAvailableUsersAsync(viewModel, canViewAllReports, currentUserId);
            
            // Load default report (last 30 days)
            await LoadSalesReportDataAsync(viewModel, canViewAllReports, currentUserId);
            await SetViewPermissionsAsync(MenuCodes.Sales);
            
            return View(viewModel);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Sales, PermissionAction.View)]
        public async Task<IActionResult> Sales(SalesReportFilter filter)
        {
            ViewData["Title"] = "Sales Reports";
            
            var viewModel = new SalesReportViewModel
            {
                Filter = filter
            };
            var canViewAllReports = CurrentUserCanViewAllReportData();
            var currentUserId = GetCurrentUserId();
            ViewBag.CanViewAllSalesUsers = canViewAllReports;
            if (!canViewAllReports)
            {
                viewModel.Filter.UserId = currentUserId;
            }
            
            // Load available users for the filter dropdown
            await LoadAvailableUsersAsync(viewModel, canViewAllReports, currentUserId);
            
            // Load report data based on filter
            await LoadSalesReportDataAsync(viewModel, canViewAllReports, currentUserId);
            await SetViewPermissionsAsync(MenuCodes.Sales);
            
            return View(viewModel);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Orders, PermissionAction.View)]
        public async Task<IActionResult> Orders()
        {
            ViewData["Title"] = "Order Reports";
            
            var viewModel = new OrderReportViewModel();
            
            // Load available users for the filter dropdown
            await LoadOrderReportUsersAsync(viewModel);
            
            // Load default report (today's orders)
            await LoadOrderReportDataAsync(viewModel);
            await SetViewPermissionsAsync(MenuCodes.Orders);
            
            return View(viewModel);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Orders, PermissionAction.View)]
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
            await SetViewPermissionsAsync(MenuCodes.Orders);
            
            return View(viewModel);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Menu, PermissionAction.View)]
        public async Task<IActionResult> Menu()
        {
            ViewData["Title"] = "Menu Analysis";
            var viewModel = new MenuReportViewModel();
            // Load default report (last 30 days)
            await LoadMenuReportDataAsync(viewModel);
            await SetViewPermissionsAsync(MenuCodes.Menu);
            return View(viewModel);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Menu, PermissionAction.View)]
        public async Task<IActionResult> Menu(MenuReportFilter filter)
        {
            ViewData["Title"] = "Menu Analysis";
            var viewModel = new MenuReportViewModel
            {
                Filter = filter
            };

            await LoadMenuReportDataAsync(viewModel);
            await SetViewPermissionsAsync(MenuCodes.Menu);
            return View(viewModel);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Customers, PermissionAction.View)]
        public async Task<IActionResult> Customers()
        {
            ViewData["Title"] = "Customer Reports";
            var model = new CustomerReportViewModel();
            await LoadCustomerReportDataAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Customers);
            return View(model);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Customers, PermissionAction.View)]
        public async Task<IActionResult> Customers(CustomerReportFilter filter)
        {
            ViewData["Title"] = "Customer Reports";
            var model = new CustomerReportViewModel { Filter = filter };
            await LoadCustomerReportDataAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Customers);
            return View(model);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Financial, PermissionAction.View)]
        public async Task<IActionResult> Financial()
        {
            ViewData["Title"] = "Financial Summary";
            
            var viewModel = new FinancialSummaryViewModel();
            
            // Load default report (last 30 days)
            await LoadFinancialSummaryDataAsync(viewModel);
            await SetViewPermissionsAsync(MenuCodes.Financial);
            
            return View(viewModel);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Financial, PermissionAction.View)]
        public async Task<IActionResult> Financial(FinancialSummaryFilter filter)
        {
            ViewData["Title"] = "Financial Summary";
            
            var viewModel = new FinancialSummaryViewModel
            {
                Filter = filter
            };
            
            // Load filtered report
            await LoadFinancialSummaryDataAsync(viewModel);
            await SetViewPermissionsAsync(MenuCodes.Financial);
            
            return View(viewModel);
        }

        private async Task LoadFinancialSummaryDataAsync(FinancialSummaryViewModel viewModel)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("usp_GetFinancialSummary", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = 60
                };

                // Add parameters
                command.Parameters.AddWithValue("@StartDate", viewModel.Filter.StartDate);
                command.Parameters.AddWithValue("@EndDate", viewModel.Filter.EndDate);
                command.Parameters.AddWithValue("@ComparisonPeriodDays", viewModel.Filter.ComparisonPeriodDays);

                using var reader = await command.ExecuteReaderAsync();

                // Result Set 1: Summary Statistics
                if (await reader.ReadAsync())
                {
                    viewModel.Summary = new FinancialSummarySummary
                    {
                        TotalOrders = reader.GetInt32(reader.GetOrdinal("TotalOrders")),
                        TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                        SubTotal = reader.GetDecimal(reader.GetOrdinal("SubTotal")),
                        TotalTax = reader.GetDecimal(reader.GetOrdinal("TotalTax")),
                        TotalTips = reader.GetDecimal(reader.GetOrdinal("TotalTips")),
                        TotalDiscounts = reader.GetDecimal(reader.GetOrdinal("TotalDiscounts")),
                        AverageOrderValue = reader.GetDecimal(reader.GetOrdinal("AverageOrderValue")),
                        PaidAmount = reader.GetDecimal(reader.GetOrdinal("PaidAmount")),
                        UnpaidAmount = reader.GetDecimal(reader.GetOrdinal("UnpaidAmount")),
                        UniqueItemsSold = reader.GetInt32(reader.GetOrdinal("UniqueItemsSold")),
                        TotalQuantitySold = reader.GetInt32(reader.GetOrdinal("TotalQuantitySold")),
                        CashPayments = reader.GetDecimal(reader.GetOrdinal("CashPayments")),
                        CardPayments = reader.GetDecimal(reader.GetOrdinal("CardPayments")),
                        UPIPayments = reader.GetDecimal(reader.GetOrdinal("UPIPayments")),
                        NetBankingPayments = reader.GetDecimal(reader.GetOrdinal("NetBankingPayments")),
                        ComplimentaryPayments = reader.GetDecimal(reader.GetOrdinal("ComplimentaryPayments")),
                        OtherPayments = reader.GetDecimal(reader.GetOrdinal("OtherPayments")),
                        NetRevenue = reader.GetDecimal(reader.GetOrdinal("NetRevenue")),
                        NetProfitMargin = reader.GetDecimal(reader.GetOrdinal("NetProfitMargin")),
                        PeriodStartDate = reader.GetDateTime(reader.GetOrdinal("PeriodStartDate")),
                        PeriodEndDate = reader.GetDateTime(reader.GetOrdinal("PeriodEndDate")),
                        TotalDays = reader.GetInt32(reader.GetOrdinal("TotalDays"))
                    };
                }

                // Result Set 2: Payment Method Breakdown
                if (await reader.NextResultAsync())
                {
                    viewModel.PaymentMethods = new List<FinancialPaymentMethodBreakdown>();
                    while (await reader.ReadAsync())
                    {
                        viewModel.PaymentMethods.Add(new FinancialPaymentMethodBreakdown
                        {
                            PaymentMethod = reader.GetString(reader.GetOrdinal("PaymentMethod")),
                            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                            TransactionCount = reader.GetInt32(reader.GetOrdinal("TransactionCount")),
                            TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                            AverageAmount = reader.GetDecimal(reader.GetOrdinal("AverageAmount")),
                            Percentage = reader.GetDecimal(reader.GetOrdinal("Percentage"))
                        });
                    }
                }

                // Result Set 3: Daily Financial Breakdown
                if (await reader.NextResultAsync())
                {
                    viewModel.DailyData = new List<DailyFinancialData>();
                    while (await reader.ReadAsync())
                    {
                        viewModel.DailyData.Add(new DailyFinancialData
                        {
                            Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                            DayOfWeek = reader.GetString(reader.GetOrdinal("DayOfWeek")),
                            OrderCount = reader.GetInt32(reader.GetOrdinal("OrderCount")),
                            Revenue = reader.GetDecimal(reader.GetOrdinal("Revenue")),
                            SubTotal = reader.GetDecimal(reader.GetOrdinal("SubTotal")),
                            Tax = reader.GetDecimal(reader.GetOrdinal("Tax")),
                            Tips = reader.GetDecimal(reader.GetOrdinal("Tips")),
                            Discounts = reader.GetDecimal(reader.GetOrdinal("Discounts")),
                            AvgOrderValue = reader.GetDecimal(reader.GetOrdinal("AvgOrderValue")),
                            NetRevenue = reader.GetDecimal(reader.GetOrdinal("NetRevenue")),
                            CashAmount = reader.GetDecimal(reader.GetOrdinal("CashAmount")),
                            DigitalAmount = reader.GetDecimal(reader.GetOrdinal("DigitalAmount"))
                        });
                    }
                }

                // Result Set 4: Revenue by Category
                if (await reader.NextResultAsync())
                {
                    viewModel.CategoryRevenues = new List<CategoryRevenue>();
                    while (await reader.ReadAsync())
                    {
                        viewModel.CategoryRevenues.Add(new CategoryRevenue
                        {
                            Category = reader.GetString(reader.GetOrdinal("Category")),
                            ItemCount = reader.GetInt32(reader.GetOrdinal("ItemCount")),
                            TotalQuantity = reader.GetInt32(reader.GetOrdinal("TotalQuantity")),
                            TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                            AvgPrice = reader.GetDecimal(reader.GetOrdinal("AvgPrice")),
                            RevenuePercentage = reader.GetDecimal(reader.GetOrdinal("RevenuePercentage"))
                        });
                    }
                }

                // Result Set 5: Top Performing Items
                if (await reader.NextResultAsync())
                {
                    viewModel.TopItems = new List<TopPerformingItem>();
                    while (await reader.ReadAsync())
                    {
                        viewModel.TopItems.Add(new TopPerformingItem
                        {
                            MenuItemId = reader.GetInt32(reader.GetOrdinal("MenuItemId")),
                            ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                            Category = reader.GetString(reader.GetOrdinal("Category")),
                            Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                            QuantitySold = reader.GetInt32(reader.GetOrdinal("QuantitySold")),
                            TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue")),
                            AvgRevenue = reader.GetDecimal(reader.GetOrdinal("AvgRevenue")),
                            OrderCount = reader.GetInt32(reader.GetOrdinal("OrderCount")),
                            RevenueContribution = reader.GetDecimal(reader.GetOrdinal("RevenueContribution"))
                        });
                    }
                }

                // Result Set 6: Period Comparison
                if (await reader.NextResultAsync())
                {
                    var periods = new List<PeriodData>();
                    while (await reader.ReadAsync())
                    {
                        periods.Add(new PeriodData
                        {
                            Period = reader.GetString(reader.GetOrdinal("Period")),
                            Orders = reader.GetInt32(reader.GetOrdinal("Orders")),
                            Revenue = reader.GetDecimal(reader.GetOrdinal("Revenue")),
                            AvgOrderValue = reader.GetDecimal(reader.GetOrdinal("AvgOrderValue")),
                            Discounts = reader.GetDecimal(reader.GetOrdinal("Discounts")),
                            Tax = reader.GetDecimal(reader.GetOrdinal("Tax"))
                        });
                    }

                    if (periods.Count >= 2)
                    {
                        viewModel.Comparison = new PeriodComparison
                        {
                            CurrentPeriod = periods.FirstOrDefault(p => p.Period == "Current Period") ?? new PeriodData(),
                            PreviousPeriod = periods.FirstOrDefault(p => p.Period == "Previous Period") ?? new PeriodData()
                        };
                    }
                }

                // Result Set 7: Hourly Revenue Pattern
                if (await reader.NextResultAsync())
                {
                    viewModel.HourlyPattern = new List<HourlyRevenue>();
                    decimal maxRevenue = 0;
                    
                    var tempList = new List<HourlyRevenue>();
                    while (await reader.ReadAsync())
                    {
                        var hourlyData = new HourlyRevenue
                        {
                            Hour = reader.GetInt32(reader.GetOrdinal("Hour")),
                            OrderCount = reader.GetInt32(reader.GetOrdinal("OrderCount")),
                            Revenue = reader.GetDecimal(reader.GetOrdinal("Revenue")),
                            AvgOrderValue = reader.GetDecimal(reader.GetOrdinal("AvgOrderValue"))
                        };
                        
                        if (hourlyData.Revenue > maxRevenue)
                            maxRevenue = hourlyData.Revenue;
                        
                        tempList.Add(hourlyData);
                    }
                    
                    // Mark peak hours (top 80% of max revenue)
                    var peakThreshold = maxRevenue * 0.8m;
                    foreach (var hour in tempList)
                    {
                        hour.IsPeakHour = hour.Revenue >= peakThreshold;
                        viewModel.HourlyPattern.Add(hour);
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error loading financial summary: {ex.Message}";
                // Initialize empty collections to avoid null reference errors
                viewModel.PaymentMethods = new List<FinancialPaymentMethodBreakdown>();
                viewModel.DailyData = new List<DailyFinancialData>();
                viewModel.CategoryRevenues = new List<CategoryRevenue>();
                viewModel.TopItems = new List<TopPerformingItem>();
                viewModel.HourlyPattern = new List<HourlyRevenue>();
            }
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Kitchen, PermissionAction.View)]
        public async Task<IActionResult> Kitchen(DateTime? from, DateTime? to, string station)
        {
            ViewData["Title"] = "Kitchen KOT Report";
            var model = new KitchenReportViewModel();
            model.Filter.FromDate = from;
            model.Filter.ToDate = to;
            model.Filter.Station = station;
            await SetViewPermissionsAsync(MenuCodes.Kitchen);

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using (var cmd = new SqlCommand("usp_GetKitchenKOTReport", connection) { CommandType = CommandType.StoredProcedure })
                {
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
                            KOTNumber = reader.IsDBNull("KOTNumber") ? "" : reader.GetString("KOTNumber"),
                            TableName = reader.IsDBNull("TableName") ? "" : reader.GetString("TableName"),
                            ItemName = reader.IsDBNull("ItemName") ? "" : reader.GetString("ItemName"),
                            Quantity = reader.IsDBNull("Quantity") ? 0 : reader.GetInt32("Quantity"),
                            Station = reader.IsDBNull("Station") ? "" : reader.GetString("Station"),
                            Status = reader.IsDBNull("Status") ? "" : reader.GetString("Status"),
                            RequestedAt = reader.IsDBNull("RequestedAt") ? DateTime.MinValue : reader.GetDateTime("RequestedAt")
                        });
                    }
                }

                // Load stations list (after first reader is closed)
                using var cmd2 = new SqlCommand(@"SELECT DISTINCT s.Name FROM KitchenStations s WHERE s.Name <> 'Bar' AND s.IsActive = 1 ORDER BY s.Name", connection);
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
    [RequirePermission(MenuCodes.Kitchen, PermissionAction.Export)]
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

        [HttpGet]
        [RequirePermission(MenuCodes.Bar, PermissionAction.View)]
        public async Task<IActionResult> Bar(DateTime? from, DateTime? to, string station)
        {
            ViewData["Title"] = "Bar BOT Report";
            var model = new BarReportViewModel();
            await SetViewPermissionsAsync(MenuCodes.Bar);

            string BuildUom(IDataRecord record)
            {
                string? ReadString(string column)
                {
                    if (!record.ColumnExists(column)) return null;
                    var ordinal = record.GetOrdinal(column);
                    return record.IsDBNull(ordinal) ? null : record.GetString(ordinal);
                }

                decimal? ReadDecimal(string column)
                {
                    if (!record.ColumnExists(column)) return null;
                    var ordinal = record.GetOrdinal(column);
                    if (record.IsDBNull(ordinal)) return null;
                    var value = record.GetValue(ordinal);
                    if (value == null || value == DBNull.Value) return null;
                    return Convert.ToDecimal(value);
                }

                var uomName = ReadString("UOMName");
                var uomType = ReadString("UOMType");
                var quantity = ReadDecimal("UOMQuantityML");

                if (string.IsNullOrWhiteSpace(uomName) && string.IsNullOrWhiteSpace(uomType) && !quantity.HasValue)
                {
                    return string.Empty;
                }

                var detailParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(uomType)) detailParts.Add(uomType);
                if (quantity.HasValue) detailParts.Add($"{quantity.Value:0.##} ml");

                if (string.IsNullOrWhiteSpace(uomName))
                {
                    return detailParts.Count > 0 ? string.Join(" - ", detailParts) : string.Empty;
                }

                return detailParts.Count > 0
                    ? $"{uomName} ({string.Join(" - ", detailParts)})"
                    : uomName;
            }

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("usp_GetBarBOTReport", connection) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@FromDate", from.HasValue ? (object)from.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", to.HasValue ? (object)to.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@Station", !string.IsNullOrWhiteSpace(station) ? (object)station : DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var item = new BOTItem
                    {
                        OrderId = reader.GetInt32("OrderId"),
                        OrderNumber = reader.GetString("OrderNumber"),
                        TableName = reader.IsDBNull("TableName") ? "" : reader.GetString("TableName"),
                        ItemName = reader.IsDBNull("ItemName") ? "" : reader.GetString("ItemName"),
                        Quantity = reader.IsDBNull("Quantity") ? 0 : reader.GetInt32("Quantity"),
                        Station = reader.IsDBNull("Station") ? "" : reader.GetString("Station"),
                        Status = reader.IsDBNull("Status") ? "" : reader.GetString("Status"),
                        RequestedAt = reader.IsDBNull("RequestedAt") ? DateTime.MinValue : reader.GetDateTime("RequestedAt")
                    };

                    item.UOM = BuildUom(reader);

                    model.Items.Add(item);
                }

                // Load stations list for bar items
                using var cmd2 = new SqlCommand(@"SELECT DISTINCT s.Name 
                    FROM KitchenStations s 
                    INNER JOIN MenuItems mi ON s.Id = mi.KitchenStationId 
                    INNER JOIN menuitemgroup mig ON mi.menuitemgroupID = mig.ID 
                    WHERE mig.itemgroup = 'BAR' 
                    ORDER BY s.Name", connection);
                using var reader2 = await cmd2.ExecuteReaderAsync();
                while (await reader2.ReadAsync()) model.AvailableStations.Add(reader2.GetString(0));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading bar report: {ex.Message}");
            }

            return View(model);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Bar, PermissionAction.Export)]
        public async Task<IActionResult> BarExport(DateTime? from, DateTime? to, string station)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("OrderNumber,TableName,ItemName,UOM,Quantity,Station,Status,RequestedAt");

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = new SqlCommand("usp_GetBarBOTReport", connection) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("@FromDate", from.HasValue ? (object)from.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@ToDate", to.HasValue ? (object)to.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue("@Station", !string.IsNullOrWhiteSpace(station) ? (object)station : DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string uomValue = string.Empty;
                    try
                    {
                        var uomOrd = reader.GetOrdinal("UOM");
                        if (uomOrd >= 0 && !reader.IsDBNull(uomOrd)) uomValue = reader.GetString(uomOrd);
                    }
                    catch { /* ignore if column not present */ }

                    var line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4},\"{5}\",\"{6}\",\"{7}\"",
                        reader.GetString(reader.GetOrdinal("OrderNumber")),
                        reader.IsDBNull(reader.GetOrdinal("TableName")) ? "" : reader.GetString(reader.GetOrdinal("TableName")),
                        reader.IsDBNull(reader.GetOrdinal("ItemName")) ? "" : reader.GetString(reader.GetOrdinal("ItemName")),
                        uomValue,
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
                Console.WriteLine($"Error exporting bar report: {ex.Message}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "BarBOTReport.csv");
        }

        private async Task LoadAvailableUsersAsync(SalesReportViewModel viewModel, bool canViewAllUsers, int currentUserId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                viewModel.AvailableUsers.Clear();

                if (!canViewAllUsers)
                {
                    using var singleUserCommand = new SqlCommand(@"
                        SELECT TOP 1 Id, 
                               ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(FirstName, '') + ' ' + ISNULL(LastName, ''))), ''), Username) AS FullName,
                               Username
                        FROM Users WHERE Id = @UserId", connection);
                    singleUserCommand.Parameters.AddWithValue("@UserId", currentUserId);
                    using var singleReader = await singleUserCommand.ExecuteReaderAsync();
                    if (await singleReader.ReadAsync())
                    {
                        viewModel.AvailableUsers.Add(new UserSelectItem
                        {
                            Id = singleReader.GetInt32(singleReader.GetOrdinal("Id")),
                            Name = singleReader.IsDBNull(singleReader.GetOrdinal("FullName")) ? string.Empty : singleReader.GetString(singleReader.GetOrdinal("FullName")),
                            Username = singleReader.IsDBNull(singleReader.GetOrdinal("Username")) ? string.Empty : singleReader.GetString(singleReader.GetOrdinal("Username"))
                        });
                    }
                    else
                    {
                        viewModel.AvailableUsers.Add(new UserSelectItem
                        {
                            Id = currentUserId,
                            Name = User?.Identity?.Name ?? "Current User",
                            Username = User?.Identity?.Name ?? "Current User"
                        });
                    }
                    return;
                }
                
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

        private async Task LoadSalesReportDataAsync(SalesReportViewModel viewModel, bool canViewAllRecords, int currentUserId)
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
                int? requestedUserId = viewModel.Filter.UserId;
                if (!canViewAllRecords)
                {
                    requestedUserId = currentUserId;
                    viewModel.Filter.UserId = currentUserId;
                }
                command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) 
                { 
                    Value = requestedUserId.HasValue ? requestedUserId.Value : DBNull.Value 
                });
                
                // Clear existing collections to avoid accumulation when posting back
                viewModel.DailySales.Clear();
                viewModel.TopMenuItems.Clear();
                viewModel.ServerPerformance.Clear();
                viewModel.OrderStatusData.Clear();
                viewModel.HourlySalesPattern.Clear();
                viewModel.OrderListing.Clear();

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
                
                // Read Order Listing (Seventh Result Set)
                if (await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        viewModel.OrderListing.Add(new OrderListingData
                        {
                            OrderId = reader.GetInt32("OrderId"),
                            OrderNumber = reader.GetString("OrderNumber"),
                            CreatedAt = reader.GetDateTime("CreatedAt"),
                            BillValue = reader.GetDecimal("BillValue"),
                            DiscountAmount = reader.GetDecimal("DiscountAmount"),
                            NetAmount = reader.GetDecimal("NetAmount"),
                            TaxAmount = reader.GetDecimal("TaxAmount"),
                            TipAmount = reader.GetDecimal("TipAmount"),
                            TotalAmount = reader.GetDecimal("TotalAmount"),
                            Status = reader.GetInt32("Status"),
                            StatusText = reader.GetString("StatusText"),
                            ServerName = reader.GetString("ServerName")
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
                                        WHERE u.IsActive = 1
                                            AND u.FirstName IS NOT NULL AND u.LastName IS NOT NULL
                                            AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
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
                        TakeoutOrders = reader.GetInt32("TakeoutOrders"),
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
        [RequirePermission(MenuCodes.Discount, PermissionAction.View)]
        public async Task<IActionResult> DiscountReport()
        {
            ViewData["Title"] = "Discount Report";
            var model = new DiscountReportViewModel();
            await LoadDiscountReportAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Discount);
            return View(model);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Discount, PermissionAction.View)]
        public async Task<IActionResult> DiscountReport(DiscountReportFilter filter)
        {
            ViewData["Title"] = "Discount Report";
            var model = new DiscountReportViewModel { Filter = filter };
            await LoadDiscountReportAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Discount);
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
                            Status = reader.GetInt32(reader.GetOrdinal("Status")),
                            StatusText = reader.GetString(reader.GetOrdinal("StatusText"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading discount report: {ex.Message}");
            }
        }

        [RequirePermission(MenuCodes.Gst, PermissionAction.View)]
        public async Task<IActionResult> GSTBreakup()
        {
            ViewData["Title"] = "GST Breakup Report";
            var model = new GSTBreakupReportViewModel();
            await LoadGSTBreakupReportAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Gst);
            return View(model);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Gst, PermissionAction.View)]
        public async Task<IActionResult> GSTBreakup(GSTBreakupReportFilter filter)
        {
            ViewData["Title"] = "GST Breakup Report";
            var model = new GSTBreakupReportViewModel { Filter = filter };
            await LoadGSTBreakupReportAsync(model);
            await SetViewPermissionsAsync(MenuCodes.Gst);
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
                            GSTPercentage = reader.IsDBNull(reader.GetOrdinal("GSTPercentage")) ? 0 : reader.GetDecimal(reader.GetOrdinal("GSTPercentage")),
                            CGSTPercentage = reader.IsDBNull(reader.GetOrdinal("CGSTPercentage")) ? 0 : reader.GetDecimal(reader.GetOrdinal("CGSTPercentage")),
                            CGSTAmount = reader.IsDBNull(reader.GetOrdinal("CGSTAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("CGSTAmount")),
                            SGSTPercentage = reader.IsDBNull(reader.GetOrdinal("SGSTPercentage")) ? 0 : reader.GetDecimal(reader.GetOrdinal("SGSTPercentage")),
                            SGSTAmount = reader.IsDBNull(reader.GetOrdinal("SGSTAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("SGSTAmount")),
                            InvoiceTotal = reader.IsDBNull(reader.GetOrdinal("InvoiceTotal")) ? 0 : reader.GetDecimal(reader.GetOrdinal("InvoiceTotal")),
                            OrderType = reader.IsDBNull(reader.GetOrdinal("OrderType")) ? string.Empty : reader.GetString(reader.GetOrdinal("OrderType")),
                            TableNumber = reader.IsDBNull(reader.GetOrdinal("TableNumber")) ? string.Empty : reader.GetString(reader.GetOrdinal("TableNumber"))
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
                model.CustomerList.Clear();

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

            // Load Takeout/Delivery customer list grouped by phone
            try
            {
                using var connection2 = new SqlConnection(_connectionString);
                await connection2.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT 
                        ISNULL(NULLIF(o.CustomerName,''), 'Unknown') AS Name,
                        ISNULL(NULLIF(o.CustomerPhone,''), '') AS Phone,
                        MAX(ISNULL(o.Customeremailid,'')) AS Email,
                        MAX(ISNULL(o.CustomerAddress,'')) AS Address,
                        CASE o.OrderType WHEN 0 THEN 'Dine-In' WHEN 1 THEN 'Takeout' WHEN 2 THEN 'Delivery' ELSE 'Other' END AS OrderType,
                        COUNT(*) AS Visits
                    FROM Orders o WITH (NOLOCK)
                    WHERE o.OrderType IN (0,1,2)
                      AND (@From IS NULL OR o.CreatedAt >= @From)
                      AND (@To IS NULL OR o.CreatedAt < DATEADD(DAY,1,@To))
                    GROUP BY 
                        ISNULL(NULLIF(o.CustomerName,''), 'Unknown'),
                        ISNULL(NULLIF(o.CustomerPhone,''), ''),
                        CASE o.OrderType WHEN 0 THEN 'Dine-In' WHEN 1 THEN 'Takeout' WHEN 2 THEN 'Delivery' ELSE 'Other' END
                    HAVING (ISNULL(NULLIF(o.CustomerPhone,''), '') <> '' OR MAX(ISNULL(o.Customeremailid,'')) <> '')
                    ORDER BY Visits DESC, Name ASC
                ", connection2)
                {
                    CommandType = CommandType.Text,
                    CommandTimeout = 60
                };

                cmd.Parameters.Add(new SqlParameter("@From", SqlDbType.Date) { Value = (object?)model.Filter.From?.Date ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@To", SqlDbType.Date) { Value = (object?)model.Filter.To?.Date ?? DBNull.Value });

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    model.CustomerList.Add(new CustomerListRow
                    {
                        Name = r.IsDBNull(r.GetOrdinal("Name")) ? string.Empty : r.GetString(r.GetOrdinal("Name")),
                        Phone = r.IsDBNull(r.GetOrdinal("Phone")) ? string.Empty : r.GetString(r.GetOrdinal("Phone")),
                        Email = r.IsDBNull(r.GetOrdinal("Email")) ? string.Empty : r.GetString(r.GetOrdinal("Email")),
                        Address = r.IsDBNull(r.GetOrdinal("Address")) ? string.Empty : r.GetString(r.GetOrdinal("Address")),
                        OrderType = r.IsDBNull(r.GetOrdinal("OrderType")) ? string.Empty : r.GetString(r.GetOrdinal("OrderType")),
                        Visits = r.IsDBNull(r.GetOrdinal("Visits")) ? 0 : r.GetInt32(r.GetOrdinal("Visits"))
                    });
                }
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Error loading customers list: {ex2.Message}");
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
        [RequirePermission(MenuCodes.Collection, PermissionAction.View)]
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

            // Default Counter filter from POS session (if selected)
            try
            {
                var posCounterId = HttpContext?.Session?.GetInt32("POS.SelectedCounterId") ?? 0;
                if (posCounterId > 0)
                {
                    model.Filter.CounterId = posCounterId;
                }
            }
            catch
            {
                // ignore
            }

            var canViewAllCollections = CurrentUserCanViewAllReportData();
            var currentUserId = GetCurrentUserId();
            ViewBag.CanViewAllCollectionUsers = canViewAllCollections;
            if (!canViewAllCollections)
            {
                model.Filter.UserId = currentUserId;
            }
            await LoadPaymentMethodsAsync(model);
            await LoadCountersAsync(model);
            await LoadCollectionRegisterDataAsync(model, canViewAllCollections, currentUserId);
            await SetViewPermissionsAsync(MenuCodes.Collection);
            return View(model);
        }

        [HttpPost]
        [RequirePermission(MenuCodes.Collection, PermissionAction.View)]
        public async Task<IActionResult> CollectionRegister(CollectionRegisterFilter filter)
        {
            ViewData["Title"] = "Order Wise Payment Method Wise Collection Register";
            var model = new CollectionRegisterViewModel { Filter = filter };
            var canViewAllCollections = CurrentUserCanViewAllReportData();
            var currentUserId = GetCurrentUserId();
            ViewBag.CanViewAllCollectionUsers = canViewAllCollections;
            if (!canViewAllCollections)
            {
                model.Filter.UserId = currentUserId;
            }
            await LoadPaymentMethodsAsync(model);
            await LoadCountersAsync(model);
            await LoadCollectionRegisterDataAsync(model, canViewAllCollections, currentUserId);
            await SetViewPermissionsAsync(MenuCodes.Collection);
            return View(model);
        }

        [HttpGet]
        [RequirePermission(MenuCodes.Collection, PermissionAction.Export)]
        public async Task<IActionResult> CollectionRegisterPdf(DateTime? fromDate, DateTime? toDate, int? paymentMethodId, int? counterId)
        {
            ViewData["Title"] = "Order Wise Payment Method Wise Collection Register";

            var model = new CollectionRegisterViewModel
            {
                Filter = new CollectionRegisterFilter
                {
                    FromDate = fromDate?.Date ?? DateTime.Today,
                    ToDate = toDate?.Date ?? DateTime.Today,
                    PaymentMethodId = paymentMethodId,
                    CounterId = counterId
                }
            };

            var canViewAllCollections = CurrentUserCanViewAllReportData();
            var currentUserId = GetCurrentUserId();
            if (!canViewAllCollections)
            {
                model.Filter.UserId = currentUserId;
            }

            await LoadPaymentMethodsAsync(model);
            await LoadCountersAsync(model);
            await LoadCollectionRegisterDataAsync(model, canViewAllCollections, currentUserId);

            var pdfBytes = BuildCollectionRegisterPdf(model);
            var fileName = BuildCollectionRegisterExportFileName(model.Filter, "pdf");
            return File(pdfBytes, "application/pdf", fileName);
        }

        private static string BuildCollectionRegisterExportFileName(CollectionRegisterFilter filter, string extension)
        {
            var from = (filter.FromDate ?? DateTime.Today).ToString("yyyy-MM-dd");
            var to = (filter.ToDate ?? DateTime.Today).ToString("yyyy-MM-dd");
            var counter = string.IsNullOrWhiteSpace(filter.CounterName) ? "ALL" : filter.CounterName;
            var safeCounter = new string(counter.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == ' ').ToArray())
                .Trim()
                .Replace(' ', '_');
            if (string.IsNullOrWhiteSpace(safeCounter)) safeCounter = "ALL";
            return $"CollectionRegister_{from}_to_{to}_{safeCounter}.{extension}";
        }

        private static byte[] BuildCollectionRegisterPdf(CollectionRegisterViewModel model)
        {
            using var stream = new MemoryStream();
            using var document = SKDocument.CreatePdf(stream);

            // A4 landscape in points (1/72 inch)
            const float pageWidth = 842f;
            const float pageHeight = 595f;
            const float margin = 28f;

            var regularTypeface = SKTypeface.Default;
            var boldTypeface = SKTypeface.FromFamilyName(regularTypeface?.FamilyName, SKFontStyle.Bold) ?? regularTypeface;

            var headerPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var subHeaderPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var mutedPaint = new SKPaint { Color = SKColors.DimGray, IsAntialias = true };
            var linePaint = new SKPaint { Color = SKColors.LightGray, StrokeWidth = 1, IsAntialias = true };

            using var headerFont = new SKFont(boldTypeface, 16);
            using var subHeaderFont = new SKFont(regularTypeface, 10);
            using var labelFont = new SKFont(boldTypeface, 9);
            using var textFont = new SKFont(regularTypeface, 9);
            using var mutedFont = new SKFont(regularTypeface, 9);

            float rowHeight = 16f;
            float headerHeight = 56f;
            float filterHeight = 24f;
            float tableTopPadding = 8f;

            // Columns (sum should fit within (pageWidth - 2*margin))
            var contentWidth = pageWidth - (2 * margin);
            // Date, Order, Table, User, Counter, Actual, Disc, GST, Round, Receipt, Method, Details
            float[] colWidths = { 90, 70, 45, 60, 95, 60, 55, 55, 55, 65, 60, 176 };
            var totalWidth = colWidths.Sum();
            if (totalWidth > contentWidth)
            {
                // Scale down proportionally if needed
                var scale = contentWidth / totalWidth;
                for (int i = 0; i < colWidths.Length; i++) colWidths[i] *= scale;
            }

            string[] headers = { "Date/Time", "Order No", "Table", "User", "Counter", "Actual", "Discount", "GST", "Round", "Receipt", "Method", "Details" };

            int rowIndex = 0;
            int pageNumber = 0;
            while (rowIndex < model.Rows.Count || rowIndex == 0)
            {
                pageNumber++;
                using var canvas = document.BeginPage(pageWidth, pageHeight);
                float y = margin;

                // Header
                canvas.DrawText("Collection Register Report", margin, y + 16, SKTextAlign.Left, headerFont, headerPaint);
                var periodText = $"Period: {(model.Filter.FromDate ?? DateTime.Today):dd-MMM-yyyy} to {(model.Filter.ToDate ?? DateTime.Today):dd-MMM-yyyy} | Payment: {model.Filter.PaymentMethodName} | Counter: {model.Filter.CounterName}";
                canvas.DrawText(periodText, margin, y + 34, SKTextAlign.Left, subHeaderFont, subHeaderPaint);
                canvas.DrawText($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}", margin, y + 50, SKTextAlign.Left, mutedFont, mutedPaint);
                y += headerHeight;

                // Summary line
                var summaryText = $"Transactions: {model.Summary.TotalTransactions} | Total Receipt: {model.Summary.TotalReceiptAmount:N2} | Round Off: {model.Summary.TotalRoundOff:+0.00;-0.00;0.00}";
                canvas.DrawText(summaryText, margin, y + 12, SKTextAlign.Left, mutedFont, mutedPaint);
                y += filterHeight;
                y += tableTopPadding;

                // Table header
                float x = margin;
                float headerY = y;
                for (int c = 0; c < headers.Length; c++)
                {
                    canvas.DrawText(headers[c], x + 2, headerY + 12, SKTextAlign.Left, labelFont, labelPaint);
                    x += colWidths[c];
                }
                y += rowHeight;
                canvas.DrawLine(margin, y, margin + contentWidth, y, linePaint);

                // Rows
                int rowsPerPage = (int)((pageHeight - margin - y - 18f) / rowHeight);
                if (rowsPerPage < 1) rowsPerPage = 1;

                int end = Math.Min(model.Rows.Count, rowIndex + rowsPerPage);
                for (int i = rowIndex; i < end; i++)
                {
                    var r = model.Rows[i];
                    x = margin;
                    var isRefund = r.PaymentStatus == 3;
                    var rowPaint = isRefund
                        ? new SKPaint { Color = SKColors.DarkRed, IsAntialias = true }
                        : textPaint;

                    string[] values = {
                        r.PaymentDate.ToString("dd-MMM HH:mm"),
                        r.OrderNo ?? "",
                        r.TableNo ?? "",
                        r.Username ?? "",
                        string.IsNullOrWhiteSpace(r.CounterName) ? "-" : r.CounterName,
                        r.ActualBillAmount.ToString("N2"),
                        Math.Abs(r.DiscountAmount).ToString("N2"),
                        r.GSTAmount.ToString("N2"),
                        r.RoundOffAmount.ToString("+0.00;-0.00;0.00"),
                        r.ReceiptAmount.ToString("N2"),
                        r.PaymentMethod ?? "",
                        r.Details ?? ""
                    };

                    for (int c = 0; c < values.Length; c++)
                    {
                        var val = values[c] ?? "";
                        // simple clipping/truncation
                        var maxWidth = colWidths[c] - 4;
                        if (textFont.MeasureText(val) > maxWidth)
                        {
                            while (val.Length > 1 && textFont.MeasureText(val + "…") > maxWidth) val = val.Substring(0, val.Length - 1);
                            val = val + "…";
                        }
                        canvas.DrawText(val, x + 2, y + 12, SKTextAlign.Left, textFont, rowPaint);
                        x += colWidths[c];
                    }

                    y += rowHeight;
                    canvas.DrawLine(margin, y, margin + contentWidth, y, linePaint);
                }

                rowIndex = end;

                // Footer / page number
                var pageText = $"Page {pageNumber}";
                canvas.DrawText(pageText, pageWidth - margin, pageHeight - margin + 6, SKTextAlign.Right, mutedFont, mutedPaint);

                document.EndPage();

                if (model.Rows.Count == 0) break;
            }

            document.Close();
            return stream.ToArray();
        }

        private async Task LoadCountersAsync(CollectionRegisterViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                model.Counters.Clear();
                model.Counters.Add(new SelectListItem { Value = "", Text = "ALL Counters" });

                using (var existsCmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.Counters', 'U') IS NULL THEN 0 ELSE 1 END", connection))
                {
                    var hasCountersTableObj = await existsCmd.ExecuteScalarAsync();
                    var hasCountersTable = false;
                    try { hasCountersTable = Convert.ToInt32(hasCountersTableObj) == 1; } catch { hasCountersTable = false; }
                    if (!hasCountersTable)
                    {
                        return;
                    }
                }

                using var command = new SqlCommand(@"
SELECT Id, CounterCode, CounterName, IsActive
FROM dbo.Counters WITH (NOLOCK)
ORDER BY CounterCode, CounterName", connection);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    var code = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    var isActive = !reader.IsDBNull(3) && reader.GetBoolean(3);

                    var displayName = string.IsNullOrWhiteSpace(code)
                        ? name
                        : (string.IsNullOrWhiteSpace(name) ? code : $"{code} - {name}");

                    if (!isActive)
                    {
                        displayName = string.IsNullOrWhiteSpace(displayName) ? "(Inactive)" : $"{displayName} (Inactive)";
                    }

                    model.Counters.Add(new SelectListItem
                    {
                        Value = id > 0 ? id.ToString() : string.Empty,
                        Text = string.IsNullOrWhiteSpace(displayName) ? $"Counter #{id}" : displayName
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading counters: {ex.Message}");
                if (!model.Counters.Any())
                {
                    model.Counters.Add(new SelectListItem { Value = "", Text = "ALL Counters" });
                }
            }
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

        private async Task LoadCollectionRegisterDataAsync(CollectionRegisterViewModel model, bool canViewAllRecords, int currentUserId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                int? requestedUserId = model.Filter.UserId;
                if (!canViewAllRecords)
                {
                    requestedUserId = currentUserId;
                    model.Filter.UserId = currentUserId;
                }

                string? scopedUserDisplayName = model.Filter.UserDisplayName;
                if (requestedUserId.HasValue)
                {
                    using var userCmd = new SqlCommand(@"
                        SELECT TOP 1 ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(FirstName, '') + ' ' + ISNULL(LastName, ''))), ''), Username)
                        FROM Users WHERE Id = @UserId", connection);
                    userCmd.Parameters.AddWithValue("@UserId", requestedUserId.Value);
                    var result = await userCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        scopedUserDisplayName = result.ToString();
                    }
                }

                using var command = new SqlCommand("usp_GetCollectionRegister", connection)
                { CommandType = CommandType.StoredProcedure };
                
                command.Parameters.AddWithValue("@FromDate", (object?)model.Filter.FromDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@ToDate", (object?)model.Filter.ToDate?.Date ?? DBNull.Value);
                command.Parameters.AddWithValue("@PaymentMethodId", (object?)model.Filter.PaymentMethodId ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserId", (object?)requestedUserId ?? DBNull.Value);

                // Counter filter (only if the stored procedure supports it)
                var hasCounterParam = false;
                try
                {
                    using var paramCheck = new SqlCommand(@"
SELECT COUNT(1)
FROM sys.parameters
WHERE object_id = OBJECT_ID('dbo.usp_GetCollectionRegister')
  AND name = '@CounterId'", connection);
                    var hasParamObj = await paramCheck.ExecuteScalarAsync();
                    try { hasCounterParam = Convert.ToInt32(hasParamObj) > 0; } catch { hasCounterParam = false; }
                    if (hasCounterParam)
                    {
                        command.Parameters.AddWithValue("@CounterId", (object?)model.Filter.CounterId ?? DBNull.Value);
                    }
                }
                catch
                {
                    // ignore - keep compatibility
                }

                using var reader = await command.ExecuteReaderAsync();

                model.Rows.Clear();
                // Read detail rows
                while (await reader.ReadAsync())
                {
                    int? counterId = null;
                    string counterName = string.Empty;
                    try
                    {
                        var counterIdOrdinal = reader.GetOrdinal("CounterId");
                        if (!reader.IsDBNull(counterIdOrdinal))
                        {
                            var raw = reader.GetValue(counterIdOrdinal);
                            if (raw != null && raw != DBNull.Value)
                            {
                                try { counterId = Convert.ToInt32(raw); } catch { counterId = null; }
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try
                    {
                        var counterNameOrdinal = reader.GetOrdinal("CounterName");
                        counterName = reader.IsDBNull(counterNameOrdinal) ? string.Empty : reader.GetString(counterNameOrdinal);
                    }
                    catch { /* ignore */ }

                    var row = new CollectionRegisterRow
                    {
                        OrderNo = reader.GetString(reader.GetOrdinal("OrderNo")),
                        TableNo = reader.GetString(reader.GetOrdinal("TableNo")),
                        Username = reader.GetString(reader.GetOrdinal("Username")),
                        CounterId = counterId,
                        CounterName = counterName,
                        ActualBillAmount = reader.GetDecimal(reader.GetOrdinal("ActualBillAmount")),
                        DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                        GSTAmount = reader.GetDecimal(reader.GetOrdinal("GSTAmount")),
                        RoundOffAmount = reader.GetDecimal(reader.GetOrdinal("RoundOffAmount")),
                        ReceiptAmount = reader.GetDecimal(reader.GetOrdinal("ReceiptAmount")),
                        PaymentMethod = reader.GetString(reader.GetOrdinal("PaymentMethod")),
                        Details = reader.GetString(reader.GetOrdinal("Details")),
                        PaymentDate = reader.GetDateTime(reader.GetOrdinal("PaymentDate")),
                        PaymentStatus = reader.GetInt32(reader.GetOrdinal("PaymentStatus"))
                    };
                    model.Rows.Add(row);
                }

                // Backward-compatible Counter enrichment:
                // Some DBs may not yet return CounterId/CounterName in usp_GetCollectionRegister.
                // If missing, fetch Counter details by OrderNumber from Orders (+ Counters if present).
                if (model.Rows.Any(r => !r.CounterId.HasValue || string.IsNullOrWhiteSpace(r.CounterName)))
                {
                    string? ordersCounterCol = null;
                    try
                    {
                        using var ordersCounterColCmd = new SqlCommand(@"
SELECT CASE
  WHEN COL_LENGTH('dbo.Orders','CounterID') IS NOT NULL THEN 'CounterID'
  WHEN COL_LENGTH('dbo.Orders','CounterId') IS NOT NULL THEN 'CounterId'
  WHEN COL_LENGTH('dbo.Orders','Counter_Id') IS NOT NULL THEN 'Counter_Id'
  WHEN COL_LENGTH('dbo.Orders','Counter') IS NOT NULL THEN 'Counter'
  ELSE NULL
END", connection);
                        var obj = await ordersCounterColCmd.ExecuteScalarAsync();
                        if (obj != null && obj != DBNull.Value) ordersCounterCol = obj.ToString();
                    }
                    catch { ordersCounterCol = null; }

                    if (!string.IsNullOrWhiteSpace(ordersCounterCol))
                    {
                        var hasCountersTable = false;
                        try
                        {
                            using var existsCmd = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.Counters', 'U') IS NULL THEN 0 ELSE 1 END", connection);
                            var obj = await existsCmd.ExecuteScalarAsync();
                            hasCountersTable = Convert.ToInt32(obj) == 1;
                        }
                        catch { hasCountersTable = false; }

                        async Task<Dictionary<string, (int? CounterId, string CounterDisplay)>> LoadCounterMapByOrderNoAsync(List<string> orderNos)
                        {
                            var map = new Dictionary<string, (int? CounterId, string CounterDisplay)>(StringComparer.OrdinalIgnoreCase);
                            if (orderNos.Count == 0) return map;

                            const int chunkSize = 500;
                            for (var offset = 0; offset < orderNos.Count; offset += chunkSize)
                            {
                                var chunk = orderNos.Skip(offset).Take(chunkSize).ToList();
                                if (chunk.Count == 0) continue;

                                var paramNames = new List<string>();
                                using var cmd = new SqlCommand();
                                cmd.Connection = connection;

                                for (int i = 0; i < chunk.Count; i++)
                                {
                                    var pname = "@o" + (offset + i);
                                    paramNames.Add(pname);
                                    cmd.Parameters.AddWithValue(pname, chunk[i]);
                                }

                                cmd.CommandText = $@"
SELECT o.OrderNumber,
       TRY_CONVERT(int, o.[{ordersCounterCol}]) AS CounterId,
       {(hasCountersTable ? "c.CounterCode" : "CAST(NULL AS nvarchar(50)) AS CounterCode")},
       {(hasCountersTable ? "c.CounterName" : "CAST(NULL AS nvarchar(100)) AS CounterName")}
FROM dbo.Orders o WITH (NOLOCK)
{(hasCountersTable ? $"LEFT JOIN dbo.Counters c WITH (NOLOCK) ON c.Id = TRY_CONVERT(int, o.[{ordersCounterCol}])" : string.Empty)}
WHERE o.OrderNumber IN ({string.Join(",", paramNames)})";

                                using var r = await cmd.ExecuteReaderAsync();
                                while (await r.ReadAsync())
                                {
                                    var orderNo = r.IsDBNull(0) ? string.Empty : r.GetString(0);
                                    if (string.IsNullOrWhiteSpace(orderNo)) continue;

                                    int? cid = null;
                                    if (!r.IsDBNull(1))
                                    {
                                        var raw = r.GetValue(1);
                                        if (raw != null && raw != DBNull.Value)
                                        {
                                            try { cid = Convert.ToInt32(raw); } catch { cid = null; }
                                        }
                                    }

                                    var code = r.IsDBNull(2) ? string.Empty : Convert.ToString(r.GetValue(2)) ?? string.Empty;
                                    var name = r.IsDBNull(3) ? string.Empty : Convert.ToString(r.GetValue(3)) ?? string.Empty;
                                    var display = string.Empty;
                                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name)) display = $"{code} - {name}";
                                    else if (!string.IsNullOrWhiteSpace(name)) display = name;
                                    else if (!string.IsNullOrWhiteSpace(code)) display = code;
                                    else if (cid.HasValue && cid.Value > 0) display = $"Counter #{cid.Value}";

                                    if (!map.ContainsKey(orderNo)) map[orderNo] = (cid, display);
                                }
                            }

                            return map;
                        }

                        var distinctOrderNos = model.Rows
                            .Select(r => (r.OrderNo ?? string.Empty).Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var counterMap = await LoadCounterMapByOrderNoAsync(distinctOrderNos);

                        foreach (var row in model.Rows)
                        {
                            var orderNo = (row.OrderNo ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(orderNo) && counterMap.TryGetValue(orderNo, out var info))
                            {
                                if (!row.CounterId.HasValue && info.CounterId.HasValue) row.CounterId = info.CounterId;
                                if (string.IsNullOrWhiteSpace(row.CounterName) && !string.IsNullOrWhiteSpace(info.CounterDisplay)) row.CounterName = info.CounterDisplay;
                            }

                            // Resolve CounterName from loaded counter dropdown (if we have the ID)
                            if (string.IsNullOrWhiteSpace(row.CounterName) && row.CounterId.HasValue)
                            {
                                var selected = model.Counters.FirstOrDefault(c => c.Value == row.CounterId.Value.ToString());
                                if (selected != null && !string.IsNullOrWhiteSpace(selected.Text))
                                {
                                    row.CounterName = selected.Text;
                                }
                            }

                            // If server-side filter was applied and we still couldn't resolve per-row display,
                            // fall back to the selected filter counter name.
                            if (hasCounterParam && model.Filter.CounterId.HasValue && (row.CounterId is null || row.CounterId <= 0))
                            {
                                row.CounterId = model.Filter.CounterId;
                            }
                            if (hasCounterParam && model.Filter.CounterId.HasValue && string.IsNullOrWhiteSpace(row.CounterName))
                            {
                                row.CounterName = model.Filter.CounterName;
                            }
                        }
                    }
                }

                // If the DB proc doesn't support CounterId param, apply client-side counter filtering (only when possible).
                if (!hasCounterParam && model.Filter.CounterId.HasValue && model.Rows.Any(r => r.CounterId.HasValue))
                {
                    var wanted = model.Filter.CounterId.Value;
                    model.Rows = model.Rows.Where(r => r.CounterId.HasValue && r.CounterId.Value == wanted).ToList();
                }

                // Calculate summary
                model.Summary.TotalTransactions = model.Rows.Count;
                model.Summary.TotalActualAmount = model.Rows.Sum(r => r.ActualBillAmount);
                model.Summary.TotalDiscount = model.Rows.Sum(r => r.DiscountAmount);
                model.Summary.TotalGST = model.Rows.Sum(r => r.GSTAmount);
                model.Summary.TotalRoundOff = model.Rows.Sum(r => r.RoundOffAmount);
                model.Summary.TotalReceiptAmount = model.Rows.Sum(r => r.ReceiptAmount);
                
                // Set payment method name for display
                if (model.Filter.PaymentMethodId.HasValue)
                {
                    var selectedMethod = model.PaymentMethods.FirstOrDefault(pm => pm.Value == model.Filter.PaymentMethodId.ToString());
                    model.Filter.PaymentMethodName = selectedMethod?.Text ?? "ALL";
                }

                // Set counter name for display
                if (model.Filter.CounterId.HasValue)
                {
                    var selectedCounter = model.Counters.FirstOrDefault(c => c.Value == model.Filter.CounterId.ToString());
                    model.Filter.CounterName = selectedCounter?.Text ?? "ALL";
                }

                if (!string.IsNullOrEmpty(scopedUserDisplayName))
                {
                    model.Filter.UserDisplayName = scopedUserDisplayName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading collection register: {ex.Message}");
            }
        }

        // =====================================================
        // Cash Closing Report
        // =====================================================
        
        [HttpGet]
        [Authorize(Roles = "Administrator,Manager")]
        [RequirePermission(MenuCodes.CashClosing, PermissionAction.View)]
        public async Task<IActionResult> CashClosing()
        {
            ViewData["Title"] = "Cash Closing Report";
            
            var viewModel = new CashClosingReportViewModel();
            
            // Set default dates (last 7 days)
            viewModel.Filters.StartDate = DateTime.Today.AddDays(-7);
            viewModel.Filters.EndDate = DateTime.Today;
            
            // Load cashiers for filter dropdown
            await LoadCashiersForFilterAsync(viewModel);
            
            // Load default report
            var reportData = await _dayClosingService.GenerateCashClosingReportAsync(
                viewModel.Filters.StartDate, 
                viewModel.Filters.EndDate, 
                viewModel.Filters.CashierId
            );
            
            viewModel.Summary = reportData.Summary;
            viewModel.DailySummaries = reportData.DailySummaries;
            viewModel.DetailRecords = reportData.DetailRecords;
            viewModel.CashierPerformance = reportData.CashierPerformance;
            viewModel.DayLockAudits = reportData.DayLockAudits;
            await SetViewPermissionsAsync(MenuCodes.CashClosing);
            
            return View(viewModel);
        }

        private int GetCurrentUserId()
        {
            try
            {
                var claim = HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out var parsedId))
                {
                    return parsedId;
                }
            }
            catch
            {
                // ignore and fall through to default
            }

            return 1; // legacy fallback
        }

        private bool CurrentUserCanViewAllReportData()
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

        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        [RequirePermission(MenuCodes.CashClosing, PermissionAction.View)]
        public async Task<IActionResult> CashClosing(CashClosingReportFilters filters)
        {
            ViewData["Title"] = "Cash Closing Report";
            
            var viewModel = new CashClosingReportViewModel
            {
                Filters = filters
            };
            
            // Validate dates
            if (filters.StartDate > filters.EndDate)
            {
                ModelState.AddModelError("", "Start date cannot be after end date");
                await LoadCashiersForFilterAsync(viewModel);
                return View(viewModel);
            }
            
            // Load cashiers for filter dropdown
            await LoadCashiersForFilterAsync(viewModel);
            
            // Load report based on filters
            var reportData = await _dayClosingService.GenerateCashClosingReportAsync(
                filters.StartDate, 
                filters.EndDate, 
                filters.CashierId
            );
            
            viewModel.Summary = reportData.Summary;
            viewModel.DailySummaries = reportData.DailySummaries;
            viewModel.DetailRecords = reportData.DetailRecords;
            viewModel.CashierPerformance = reportData.CashierPerformance;
            viewModel.DayLockAudits = reportData.DayLockAudits;
            await SetViewPermissionsAsync(MenuCodes.CashClosing);
            
            return View(viewModel);
        }

        private async Task LoadCashiersForFilterAsync(CashClosingReportViewModel viewModel)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT DISTINCT 
                            u.Id, 
                            u.Username
                        FROM Users u
                        INNER JOIN UserRoles ur ON ur.UserId = u.Id
                        INNER JOIN Roles r ON r.Id = ur.RoleId
                        WHERE u.IsActive = 1 
                          AND r.Name IN ('Administrator', 'Manager', 'Cashier')
                        ORDER BY u.Username";

                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var cashiers = new List<(int Id, string Name)>();
                        while (await reader.ReadAsync())
                        {
                            cashiers.Add((
                                reader.GetInt32(0),
                                reader.GetString(1)
                            ));
                        }
                        
                        ViewBag.Cashiers = cashiers;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading cashiers: {ex.Message}");
                ViewBag.Cashiers = new List<(int, string)>();
            }
        }

        // Feedback Survey Report
        [HttpGet]
        [RequirePermission(MenuCodes.Feedback, PermissionAction.View)]
        public async Task<IActionResult> FeedbackSurveyReport(DateTime? fromDate, DateTime? toDate, string location, int? minRating, int? maxRating)
        {
            ViewData["Title"] = "Feedback Survey Report";
            
            var viewModel = new FeedbackSurveyReportViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                Location = location,
                MinRating = minRating,
                MaxRating = maxRating
            };

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var command = new SqlCommand("usp_GetFeedbackSurveyReport", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@FromDate", viewModel.FromDate);
                        command.Parameters.AddWithValue("@ToDate", viewModel.ToDate);
                        command.Parameters.AddWithValue("@Location", (object)viewModel.Location ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MinRating", (object)viewModel.MinRating ?? DBNull.Value);
                        command.Parameters.AddWithValue("@MaxRating", (object)viewModel.MaxRating ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            int ReadInt(string column)
                            {
                                var value = reader[column];
                                return value == DBNull.Value ? 0 : Convert.ToInt32(value);
                            }

                            int? ReadNullableInt(string column)
                            {
                                var ordinal = reader.GetOrdinal(column);
                                return reader.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(reader.GetValue(ordinal));
                            }

                            // First result set: Feedback items
                            while (await reader.ReadAsync())
                            {
                                viewModel.FeedbackItems.Add(new FeedbackSurveyReportItem
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    VisitDate = reader.GetDateTime(reader.GetOrdinal("VisitDate")),
                                    Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? "" : reader.GetString(reader.GetOrdinal("Location")),
                                    IsFirstVisit = reader.IsDBNull(reader.GetOrdinal("IsFirstVisit")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsFirstVisit")),
                                    OverallRating = ReadInt("OverallRating"),
                                    FoodRating = ReadNullableInt("FoodRating"),
                                    ServiceRating = ReadNullableInt("ServiceRating"),
                                    CleanlinessRating = ReadNullableInt("CleanlinessRating"),
                                    StaffRating = ReadNullableInt("StaffRating"),
                                    AmbienceRating = ReadNullableInt("AmbienceRating"),
                                    ValueRating = ReadNullableInt("ValueRating"),
                                    SpeedRating = ReadNullableInt("SpeedRating"),
                                    Tags = reader.IsDBNull(reader.GetOrdinal("Tags")) ? "" : reader.GetString(reader.GetOrdinal("Tags")),
                                    Comments = reader.IsDBNull(reader.GetOrdinal("Comments")) ? "" : reader.GetString(reader.GetOrdinal("Comments")),
                                    GuestName = reader.IsDBNull(reader.GetOrdinal("GuestName")) ? "" : reader.GetString(reader.GetOrdinal("GuestName")),
                                    Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? "" : reader.GetString(reader.GetOrdinal("Email")),
                                    Phone = reader.IsDBNull(reader.GetOrdinal("Phone")) ? "" : reader.GetString(reader.GetOrdinal("Phone")),
                                    GuestBirthDate = reader.IsDBNull(reader.GetOrdinal("GuestBirthDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("GuestBirthDate")),
                                    AnniversaryDate = reader.IsDBNull(reader.GetOrdinal("AnniversaryDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("AnniversaryDate")),
                                    SurveyJson = reader.IsDBNull(reader.GetOrdinal("SurveyJson")) ? "" : reader.GetString(reader.GetOrdinal("SurveyJson")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    AverageRating = reader.GetDecimal(reader.GetOrdinal("AverageRating")),
                                    SurveyQuestionsAnswered = ReadInt("SurveyQuestionsAnswered")
                                });
                            }

                            // Second result set: Summary
                            if (await reader.NextResultAsync() && await reader.ReadAsync())
                            {
                                viewModel.Summary = new FeedbackSurveyReportSummary
                                {
                                    TotalFeedback = reader.GetInt32(reader.GetOrdinal("TotalFeedback")),
                                    UniqueLocations = reader.GetInt32(reader.GetOrdinal("UniqueLocations")),
                                    UniqueGuests = reader.GetInt32(reader.GetOrdinal("UniqueGuests")),
                                    FirstTimeVisitors = reader.GetInt32(reader.GetOrdinal("FirstTimeVisitors")),
                                    ReturningVisitors = reader.GetInt32(reader.GetOrdinal("ReturningVisitors")),
                                    AvgOverallRating = reader.IsDBNull(reader.GetOrdinal("AvgOverallRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgOverallRating")),
                                    AvgFoodRating = reader.IsDBNull(reader.GetOrdinal("AvgFoodRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgFoodRating")),
                                    AvgServiceRating = reader.IsDBNull(reader.GetOrdinal("AvgServiceRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgServiceRating")),
                                    AvgCleanlinessRating = reader.IsDBNull(reader.GetOrdinal("AvgCleanlinessRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgCleanlinessRating")),
                                    AvgStaffRating = reader.IsDBNull(reader.GetOrdinal("AvgStaffRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgStaffRating")),
                                    AvgAmbienceRating = reader.IsDBNull(reader.GetOrdinal("AvgAmbienceRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgAmbienceRating")),
                                    AvgValueRating = reader.IsDBNull(reader.GetOrdinal("AvgValueRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgValueRating")),
                                    AvgSpeedRating = reader.IsDBNull(reader.GetOrdinal("AvgSpeedRating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("AvgSpeedRating")),
                                    FeedbackWithSurvey = reader.GetInt32(reader.GetOrdinal("FeedbackWithSurvey")),
                                    EarliestFeedback = reader.IsDBNull(reader.GetOrdinal("EarliestFeedback")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EarliestFeedback")),
                                    LatestFeedback = reader.IsDBNull(reader.GetOrdinal("LatestFeedback")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("LatestFeedback"))
                                };
                            }

                            // Third result set: Rating distribution
                            if (await reader.NextResultAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    viewModel.RatingDistribution.Add(new RatingDistribution
                                    {
                                        Rating = ReadInt("Rating"),
                                        Count = ReadInt("Count"),
                                        Percentage = reader.GetDecimal(reader.GetOrdinal("Percentage"))
                                    });
                                }
                            }

                            // Fourth result set: Top tags
                            if (await reader.NextResultAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    viewModel.TopTags.Add(new TagCount
                                    {
                                        Tag = reader.GetString(reader.GetOrdinal("Tag")),
                                        Count = ReadInt("Count")
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Feedback Survey Report: {ex.Message}");
                TempData["Error"] = $"Error loading report: {ex.Message}";
            }

            await SetViewPermissionsAsync(MenuCodes.Feedback);
            return View(viewModel);
        }
    }
}