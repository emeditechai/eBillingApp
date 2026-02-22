namespace RestaurantManagementSystem.Models
{
    public class InventoryStock
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public int GodownId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal? LowLevelQty { get; set; }
    }
}
