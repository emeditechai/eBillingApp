using System;
using System.Collections.Generic;

namespace RestaurantManagementSystem.Models
{
    public class KitchenReportFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Station { get; set; }
        public bool ShowCompleted { get; set; } = true;
    }

    public class KOTItem
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string KOTNumber { get; set; }
        public string TableName { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public string Station { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public class KitchenReportViewModel
    {
        public KitchenReportFilter Filter { get; set; } = new KitchenReportFilter();
        public List<KOTItem> Items { get; set; } = new List<KOTItem>();
        public List<string> AvailableStations { get; set; } = new List<string>();
    }
}
