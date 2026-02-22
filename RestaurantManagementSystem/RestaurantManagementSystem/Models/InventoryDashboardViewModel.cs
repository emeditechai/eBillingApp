namespace RestaurantManagementSystem.Models
{
    public class InventoryDashboardViewModel
    {
        public int TotalTrackedItems { get; set; }
        public decimal TotalQuantityOnHand { get; set; }
        public int LowStockItemsCount { get; set; }
        public decimal TodayStockInQuantity { get; set; }
        public decimal TodayStockOutQuantity { get; set; }
        public int ActiveGodownsCount { get; set; }
        public int ActivePartiesCount { get; set; }

        public List<InventoryLowStockItem> LowStockItems { get; set; } = new();
        public List<InventoryItemWiseStockInfo> ItemWiseStocks { get; set; } = new();
    }

    public class InventoryLowStockItem
    {
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public int GodownId { get; set; }
        public string GodownName { get; set; } = string.Empty;
        public decimal QuantityOnHand { get; set; }
        public decimal? LowLevelQty { get; set; }
    }

    public class InventoryItemWiseStockInfo
    {
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal TotalQuantityOnHand { get; set; }
        public int GodownCount { get; set; }
        public string GodownNames { get; set; } = string.Empty;
        public int LowStockGodownCount { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }
}
