using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class CustomerReportViewModel
    {
        public CustomerReportFilter Filter { get; set; } = new CustomerReportFilter();
        public CustomerSummary Summary { get; set; } = new CustomerSummary();
        public List<TopCustomer> TopCustomers { get; set; } = new List<TopCustomer>();
        public List<VisitFrequency> VisitFrequencies { get; set; } = new List<VisitFrequency>();
        public List<LoyaltyBucket> LoyaltyStats { get; set; } = new List<LoyaltyBucket>();
        public List<DemographicRow> Demographics { get; set; } = new List<DemographicRow>();
        public List<CustomerListRow> CustomerList { get; set; } = new List<CustomerListRow>();
    }

    public class CustomerReportFilter
    {
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime? From { get; set; } = DateTime.Today.AddMonths(-1);

        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime? To { get; set; } = DateTime.Today;
    }

    public class CustomerSummary
    {
        public int TotalCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public decimal AverageVisitsPerCustomer { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopCustomer
    {
        public int? CustomerId { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public int Visits { get; set; }
        public decimal Revenue { get; set; }
        public decimal LTV { get; set; }
    }

    public class VisitFrequency
    {
        public string Period { get; set; } = "";
        public int Visits { get; set; }
        public decimal Revenue { get; set; }
    }

    public class LoyaltyBucket
    {
        public string Bucket { get; set; } = ""; // e.g., 1 visit, 2-3 visits
        public int CustomerCount { get; set; }
    }

    public class DemographicRow
    {
        public string Category { get; set; } = ""; // e.g., AgeGroup or Gender
        public int Count { get; set; }
    }

    public class CustomerListRow
    {
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string OrderType { get; set; } = ""; // Takeout / Delivery
        public int Visits { get; set; }
    }
}
