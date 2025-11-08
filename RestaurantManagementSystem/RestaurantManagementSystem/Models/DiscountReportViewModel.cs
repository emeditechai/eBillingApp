using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class DiscountReportViewModel
    {
        public DiscountReportFilter Filter { get; set; } = new DiscountReportFilter();
        public DiscountReportSummary Summary { get; set; } = new DiscountReportSummary();
        public List<DiscountReportRow> Rows { get; set; } = new List<DiscountReportRow>();
    }

    public class DiscountReportFilter
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; } = DateTime.Today;

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; } = DateTime.Today;
    }

    public class DiscountReportSummary
    {
        public int TotalDiscountedOrders { get; set; }
        public decimal TotalDiscountAmount { get; set; }
        public decimal AvgDiscountPerOrder { get; set; }
        public decimal MaxDiscount { get; set; }
        public decimal MinDiscount { get; set; }
        public decimal TotalGrossBeforeDiscount { get; set; }
        public decimal NetAfterDiscount { get; set; }

        public decimal DiscountPercentageOfGross => TotalGrossBeforeDiscount > 0 ? (TotalDiscountAmount * 100m / TotalGrossBeforeDiscount) : 0m;
        public decimal AverageGrossBeforeDiscount => TotalDiscountedOrders > 0 ? (TotalGrossBeforeDiscount / TotalDiscountedOrders) : 0m;
    }

    public class DiscountReportRow
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TipAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountApplied { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusText { get; set; } = string.Empty;

        public string ServerDisplay => !string.IsNullOrWhiteSpace(FirstName) ? $"{FirstName} {LastName}" : Username;
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd HH:mm");
        
        // Discount Percentage = (DiscountAmount / Subtotal) * 100
        public decimal DiscountPercentage => Subtotal > 0 ? (DiscountAmount * 100m / Subtotal) : 0m;
    }
}
