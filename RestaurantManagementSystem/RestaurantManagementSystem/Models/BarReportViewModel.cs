using System;
using System.Collections.Generic;

namespace RestaurantManagementSystem.Models
{
    public class BarReportFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Station { get; set; }
        public bool ShowCompleted { get; set; } = true;
    }

    public class BOTItem
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string TableName { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public string Station { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public class BarReportViewModel
    {
        public BarReportFilter Filter { get; set; } = new BarReportFilter();
        public List<BOTItem> Items { get; set; } = new List<BOTItem>();
        public List<string> AvailableStations { get; set; } = new List<string>();
    }
}
