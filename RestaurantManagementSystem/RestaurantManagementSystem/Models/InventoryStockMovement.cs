namespace RestaurantManagementSystem.Models
{
    public class InventoryStockMovement
    {
        public int Id { get; set; }
        public string MovementType { get; set; } = "IN";
        public string ReferenceType { get; set; } = "STOCK_IN";
        public int? ReferenceId { get; set; }
        public int? OrderId { get; set; }
        public int MenuItemId { get; set; }
        public int GodownId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public int? PartyId { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
