using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    /// <summary>
    /// Main view model for Financial Summary Report
    /// </summary>
    public class FinancialSummaryViewModel
    {
        public FinancialSummaryFilter Filter { get; set; } = new();
        public FinancialSummarySummary Summary { get; set; } = new();
        public List<FinancialPaymentMethodBreakdown> PaymentMethods { get; set; } = new();
        public List<DailyFinancialData> DailyData { get; set; } = new();
        public List<CategoryRevenue> CategoryRevenues { get; set; } = new();
        public List<TopPerformingItem> TopItems { get; set; } = new();
        public PeriodComparison Comparison { get; set; } = new();
        public List<HourlyRevenue> HourlyPattern { get; set; } = new();
    }

    /// <summary>
    /// Filter parameters for the financial summary report
    /// </summary>
    public class FinancialSummaryFilter
    {
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);

        [Display(Name = "End Date")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "Comparison Period (Days)")]
        public int ComparisonPeriodDays { get; set; } = 30;

        public string DateRangeDisplay => $"{StartDate:MMM dd, yyyy} - {EndDate:MMM dd, yyyy}";
        public int TotalDays => (EndDate - StartDate).Days + 1;
    }

    /// <summary>
    /// Summary statistics for the financial report
    /// </summary>
    public class FinancialSummarySummary
    {
        // Order Metrics
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalTips { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal UnpaidAmount { get; set; }

        // Item Metrics
        public int UniqueItemsSold { get; set; }
        public int TotalQuantitySold { get; set; }

        // Payment Method Totals
        public decimal CashPayments { get; set; }
        public decimal CardPayments { get; set; }
        public decimal UPIPayments { get; set; }
        public decimal NetBankingPayments { get; set; }
        public decimal ComplimentaryPayments { get; set; }
        public decimal OtherPayments { get; set; }

        // Profit Metrics
        public decimal NetRevenue { get; set; }
        public decimal NetProfitMargin { get; set; }

        // Period Info
        public DateTime PeriodStartDate { get; set; }
        public DateTime PeriodEndDate { get; set; }
        public int TotalDays { get; set; }

        // Calculated Properties
        public decimal DailyAverageRevenue => TotalDays > 0 ? TotalRevenue / TotalDays : 0;
        public decimal PaymentCollectionRate => TotalRevenue > 0 ? (PaidAmount / TotalRevenue) * 100 : 0;
        public decimal DiscountPercentage => SubTotal > 0 ? (TotalDiscounts / SubTotal) * 100 : 0;
        public decimal TaxPercentage => SubTotal > 0 ? (TotalTax / SubTotal) * 100 : 0;
        public decimal DigitalPaymentTotal => CardPayments + UPIPayments + NetBankingPayments;
        public decimal DigitalPaymentPercentage => TotalRevenue > 0 ? (DigitalPaymentTotal / TotalRevenue) * 100 : 0;
        
        // Display Helpers
        public string TotalRevenueDisplay => TotalRevenue.ToString("C2");
        public string AverageOrderValueDisplay => AverageOrderValue.ToString("C2");
        public string NetRevenueDisplay => NetRevenue.ToString("C2");
        public string DailyAverageRevenueDisplay => DailyAverageRevenue.ToString("C2");
    }

    /// <summary>
    /// Payment method breakdown details for Financial Summary
    /// </summary>
    public class FinancialPaymentMethodBreakdown
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public decimal Percentage { get; set; }

        // Display Helpers
        public string TotalAmountDisplay => TotalAmount.ToString("C2");
        public string AverageAmountDisplay => AverageAmount.ToString("C2");
        public string PercentageDisplay => Percentage.ToString("F2") + "%";
        
        // Icon mapping for UI
        public string IconClass => PaymentMethod switch
        {
            "CASH" => "fas fa-money-bill-wave text-success",
            "CREDIT_CARD" => "fas fa-credit-card text-primary",
            "DEBIT_CARD" => "fas fa-credit-card text-info",
            "UPI" => "fas fa-mobile-alt text-warning",
            "NET_BANKING" => "fas fa-university text-secondary",
            "COMPLIMENTARY" => "fas fa-gift text-danger",
            _ => "fas fa-wallet text-muted"
        };

        // Color coding for charts
        public string ChartColor => PaymentMethod switch
        {
            "CASH" => "#28a745",
            "CREDIT_CARD" => "#007bff",
            "DEBIT_CARD" => "#17a2b8",
            "UPI" => "#ffc107",
            "NET_BANKING" => "#6c757d",
            "COMPLIMENTARY" => "#dc3545",
            _ => "#6c757d"
        };
    }

    /// <summary>
    /// Daily financial data for trend analysis
    /// </summary>
    public class DailyFinancialData
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Tips { get; set; }
        public decimal Discounts { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal CashAmount { get; set; }
        public decimal DigitalAmount { get; set; }

        // Display Helpers
        public string DateDisplay => Date.ToString("MMM dd, yyyy");
        public string ShortDateDisplay => Date.ToString("MMM dd");
        public string RevenueDisplay => Revenue.ToString("C2");
        public string NetRevenueDisplay => NetRevenue.ToString("C2");
        public string AvgOrderValueDisplay => AvgOrderValue.ToString("C2");
        
        // Calculated Properties
        public decimal CashPercentage => Revenue > 0 ? (CashAmount / Revenue) * 100 : 0;
        public decimal DigitalPercentage => Revenue > 0 ? (DigitalAmount / Revenue) * 100 : 0;
    }

    /// <summary>
    /// Revenue breakdown by category
    /// </summary>
    public class CategoryRevenue
    {
        public string Category { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal RevenuePercentage { get; set; }

        // Display Helpers
        public string TotalRevenueDisplay => TotalRevenue.ToString("C2");
        public string AvgPriceDisplay => AvgPrice.ToString("C2");
        public string RevenuePercentageDisplay => RevenuePercentage.ToString("F2") + "%";
        
        // Icon for category
        public string CategoryIcon => Category.ToUpper() switch
        {
            "FOOD" => "fas fa-utensils",
            "BEVERAGE" => "fas fa-coffee",
            "DRINKS" => "fas fa-wine-glass",
            "APPETIZER" => "fas fa-drumstick-bite",
            "DESSERT" => "fas fa-ice-cream",
            "MAIN_COURSE" => "fas fa-hamburger",
            _ => "fas fa-tag"
        };

        // Chart color assignment
        public string ChartColor
        {
            get
            {
                var hash = Category.GetHashCode();
                var colors = new[] { "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40" };
                return colors[Math.Abs(hash) % colors.Length];
            }
        }
    }

    /// <summary>
    /// Top performing menu items
    /// </summary>
    public class TopPerformingItem
    {
        public int MenuItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AvgRevenue { get; set; }
        public int OrderCount { get; set; }
        public decimal RevenueContribution { get; set; }

        // Display Helpers
        public string PriceDisplay => Price.ToString("C2");
        public string TotalRevenueDisplay => TotalRevenue.ToString("C2");
        public string AvgRevenueDisplay => AvgRevenue.ToString("C2");
        public string ContributionDisplay => RevenueContribution.ToString("F2") + "%";
        
        // Badge color based on performance
        public string PerformanceBadge => RevenueContribution switch
        {
            >= 10 => "badge bg-danger",
            >= 5 => "badge bg-warning",
            >= 2 => "badge bg-success",
            _ => "badge bg-secondary"
        };
    }

    /// <summary>
    /// Period comparison data (current vs previous period)
    /// </summary>
    public class PeriodComparison
    {
        public PeriodData CurrentPeriod { get; set; } = new();
        public PeriodData PreviousPeriod { get; set; } = new();

        // Growth Calculations
        public decimal OrdersGrowth => CalculateGrowth(CurrentPeriod.Orders, PreviousPeriod.Orders);
        public decimal RevenueGrowth => CalculateGrowth(CurrentPeriod.Revenue, PreviousPeriod.Revenue);
        public decimal AvgOrderValueGrowth => CalculateGrowth(CurrentPeriod.AvgOrderValue, PreviousPeriod.AvgOrderValue);
        
        private decimal CalculateGrowth(decimal current, decimal previous)
        {
            if (previous == 0) return current > 0 ? 100 : 0;
            return ((current - previous) / previous) * 100;
        }

        // Display Helpers
        public string OrdersGrowthDisplay => FormatGrowth(OrdersGrowth);
        public string RevenueGrowthDisplay => FormatGrowth(RevenueGrowth);
        public string AvgOrderValueGrowthDisplay => FormatGrowth(AvgOrderValueGrowth);
        
        private string FormatGrowth(decimal growth)
        {
            var sign = growth >= 0 ? "+" : "";
            return $"{sign}{growth:F2}%";
        }

        public string OrdersGrowthClass => OrdersGrowth >= 0 ? "text-success" : "text-danger";
        public string RevenueGrowthClass => RevenueGrowth >= 0 ? "text-success" : "text-danger";
        public string AvgOrderValueGrowthClass => AvgOrderValueGrowth >= 0 ? "text-success" : "text-danger";
    }

    /// <summary>
    /// Data for a specific period
    /// </summary>
    public class PeriodData
    {
        public string Period { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal Discounts { get; set; }
        public decimal Tax { get; set; }

        // Display Helpers
        public string RevenueDisplay => Revenue.ToString("C2");
        public string AvgOrderValueDisplay => AvgOrderValue.ToString("C2");
        public string DiscountsDisplay => Discounts.ToString("C2");
        public string TaxDisplay => Tax.ToString("C2");
    }

    /// <summary>
    /// Hourly revenue pattern for identifying peak hours
    /// </summary>
    public class HourlyRevenue
    {
        public int Hour { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal AvgOrderValue { get; set; }

        // Display Helpers
        public string HourDisplay => $"{Hour:D2}:00";
        public string TimeRange => $"{Hour:D2}:00 - {(Hour + 1):D2}:00";
        public string RevenueDisplay => Revenue.ToString("C2");
        public string AvgOrderValueDisplay => AvgOrderValue.ToString("C2");
        
        // Peak hour identification
        public bool IsPeakHour { get; set; }
        public string HourClass => IsPeakHour ? "table-warning" : "";
    }
}
