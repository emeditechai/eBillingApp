using System;

namespace RestaurantManagementSystem.Models
{
    public class GuestLoyaltyTransaction
    {
        public int TxnId { get; set; }
        public string CardNo { get; set; } = string.Empty;
        public string? BillNo { get; set; }
        public string? OutletType { get; set; } // RESTAURANT, BAR
        public string TxnType { get; set; } = string.Empty; // EARN, REDEEM, ADJUSTMENT, EXPIRY
        public decimal TxnPoints { get; set; }
        public decimal? ValueAmount { get; set; }
        public DateTime TxnDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public string? Remarks { get; set; }
        public string? CreatedBy { get; set; }
    }
}
