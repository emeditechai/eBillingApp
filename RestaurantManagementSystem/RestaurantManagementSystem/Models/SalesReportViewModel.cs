using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class SalesReportViewModel
    {
        public SalesReportFilter Filter { get; set; } = new SalesReportFilter();
        public SalesReportSummary Summary { get; set; } = new SalesReportSummary();
        public List<DailySalesData> DailySales { get; set; } = new List<DailySalesData>();
        public List<TopMenuItem> TopMenuItems { get; set; } = new List<TopMenuItem>();
        public List<ServerPerformance> ServerPerformance { get; set; } = new List<ServerPerformance>();
        public List<OrderStatusData> OrderStatusData { get; set; } = new List<OrderStatusData>();
        public List<HourlySalesData> HourlySalesPattern { get; set; } = new List<HourlySalesData>();
        public List<OrderListingData> OrderListing { get; set; } = new List<OrderListingData>();
        public List<UserSelectItem> AvailableUsers { get; set; } = new List<UserSelectItem>();
    }

    public class SalesReportFilter
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; } = DateTime.Today.AddDays(-30);

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Cashier/User")]
        public int? UserId { get; set; }
    }

    public class SalesReportSummary
    {
        public int TotalOrders { get; set; }
        public decimal TotalSales { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalSubtotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalTips { get; set; }
        public decimal TotalDiscounts { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }

        // Calculated Properties
        public decimal CompletionRate => TotalOrders > 0 ? (CompletedOrders * 100m / TotalOrders) : 0;
        public decimal CancellationRate => TotalOrders > 0 ? (CancelledOrders * 100m / TotalOrders) : 0;
    }

    public class DailySalesData
    {
        public DateTime SalesDate { get; set; }
        public int OrderCount { get; set; }
        public decimal DailySales { get; set; }
        public decimal AvgOrderValue { get; set; }
        
        public string SalesDateFormatted => SalesDate.ToString("MMM dd, yyyy");
    }

    public class TopMenuItem
    {
        public string ItemName { get; set; } = "";
        public int MenuItemId { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AveragePrice { get; set; }
        public int OrderCount { get; set; }
    }

    public class ServerPerformance
    {
        public string ServerName { get; set; } = "";
        public string Username { get; set; } = "";
        public int? UserId { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalSales { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal TotalTips { get; set; }
        
        public decimal TipPercentage => TotalSales > 0 ? (TotalTips * 100 / TotalSales) : 0;
    }

    public class OrderStatusData
    {
        public string OrderStatus { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class HourlySalesData
    {
        public int HourOfDay { get; set; }
        public int OrderCount { get; set; }
        public decimal HourlySales { get; set; }
        public decimal AvgOrderValue { get; set; }
        
        public string HourFormatted => $"{HourOfDay:D2}:00";
        public string HourRange => $"{HourOfDay:D2}:00 - {(HourOfDay + 1):D2}:00";
    }

    public class UserSelectItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Username { get; set; } = "";
        
        public string DisplayName => !string.IsNullOrEmpty(Name) ? $"{Name} ({Username})" : Username;
    }

    public class OrderListingData
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public decimal BillValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TipAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; } = "";
        public string ServerName { get; set; } = "";
        
        public string CreatedAtFormatted => CreatedAt.ToString("MMM dd, yyyy hh:mm tt");
        public decimal DiscountPercentage => BillValue > 0 ? (DiscountAmount * 100 / BillValue) : 0;
    }
}