using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantManagementSystem.Models
{
    public class OrderReportViewModel
    {
        public OrderReportFilter Filter { get; set; } = new OrderReportFilter();
        public OrderReportSummary Summary { get; set; } = new OrderReportSummary();
        public List<OrderReportItem> Orders { get; set; } = new List<OrderReportItem>();
        public List<SelectListItem> AvailableUsers { get; set; } = new List<SelectListItem>();
        public List<HourlyOrderDistribution> HourlyDistribution { get; set; } = new List<HourlyOrderDistribution>();
        
        // Pagination properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class OrderReportFilter
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime FromDate { get; set; } = DateTime.Today;

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime ToDate { get; set; } = DateTime.Today;

        [Display(Name = "User")]
        public int? UserId { get; set; }

        [Display(Name = "Status")]
        public int? Status { get; set; }

        [Display(Name = "Order Type")]
        public int? OrderType { get; set; }

        [Display(Name = "Search")]
        [StringLength(100)]
        public string SearchTerm { get; set; } = string.Empty;

        [Display(Name = "Page Size")]
        public int PageSize { get; set; } = 50;
    }

    public class OrderReportSummary
    {
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int InProgressOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int DineInOrders { get; set; }
        public int TakeoutOrders { get; set; }
        public int DeliveryOrders { get; set; }

        // Calculated properties
        public string FormattedTotalRevenue => $"₹{TotalRevenue:N2}";
        public string FormattedAverageOrderValue => $"₹{AverageOrderValue:N2}";
        public double CompletionRate => TotalOrders > 0 ? (double)CompletedOrders / TotalOrders * 100 : 0;
        public double CancellationRate => TotalOrders > 0 ? (double)CancelledOrders / TotalOrders * 100 : 0;
    }

    public class OrderReportItem
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string WaiterName { get; set; } = string.Empty;
        public int OrderType { get; set; }
        public string OrderTypeName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TipAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string SpecialInstructions { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? PreparationTimeMinutes { get; set; }
        public int ItemCount { get; set; }
        public int TotalQuantity { get; set; }

        // Formatted properties
        public string FormattedSubtotal => $"₹{Subtotal:N2}";
        public string FormattedTaxAmount => $"₹{TaxAmount:N2}";
        public string FormattedTipAmount => $"₹{TipAmount:N2}";
        public string FormattedDiscountAmount => $"₹{DiscountAmount:N2}";
        public string FormattedTotalAmount => $"₹{TotalAmount:N2}";
        public string FormattedCreatedAt => CreatedAt.ToString("dd/MM/yyyy HH:mm");
        public string FormattedCompletedAt => CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-";
        public string FormattedPreparationTime => PreparationTimeMinutes.HasValue ? 
            $"{PreparationTimeMinutes} min" : "-";

        // Status badge class for UI
        public string StatusBadgeClass => Status switch
        {
            0 => "badge bg-primary text-white",  // New Order
            1 => "badge bg-warning text-dark",   // Pending
            2 => "badge bg-info text-white",     // In Progress
            3 => "badge bg-success text-white",  // Completed
            4 => "badge bg-danger text-white",   // Cancelled
            _ => "badge bg-secondary text-white"
        };

        // Order type badge class for UI
        public string OrderTypeBadgeClass => OrderType switch
        {
            0 => "badge bg-primary text-white",   // Dine-In
            1 => "badge bg-warning text-dark",    // Takeout
            2 => "badge bg-info text-white",      // Delivery
            3 => "badge bg-info text-white",      // Delivery
            _ => "badge bg-secondary text-white"
        };

        public bool HasSpecialInstructions => !string.IsNullOrWhiteSpace(SpecialInstructions);
    }

    public class HourlyOrderDistribution
    {
        public int Hour { get; set; }
        public int OrderCount { get; set; }
        public decimal HourlyRevenue { get; set; }
        
        public string FormattedHour => $"{Hour:D2}:00";
        public string FormattedRevenue => $"₹{HourlyRevenue:N2}";
    }

    // Additional enums for better type safety
    public enum OrderStatus
    {
        NewOrder = 0,
        Pending = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4
    }

    public enum OrderType
    {
        WalkIn = 0,
        DineIn = 1,
        Takeaway = 2,
        Delivery = 3
    }
}