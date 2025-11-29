using System;

namespace RestaurantManagementSystem.Models
{
    public class LoyaltyConfig
    {
        public int Id { get; set; }
        public string OutletType { get; set; } = string.Empty; // RESTAURANT, BAR
        public decimal EarnRate { get; set; }
        public decimal RedemptionValue { get; set; }
        public decimal MinBillToEarn { get; set; }
        public decimal MaxPointsPerBill { get; set; }
        public int ExpiryDays { get; set; }
        public string? EligiblePaymentModes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
    }
}
