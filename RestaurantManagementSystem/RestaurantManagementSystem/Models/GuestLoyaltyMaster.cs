using System;

namespace RestaurantManagementSystem.Models
{
    public class GuestLoyaltyMaster
    {
        public string CardNo { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime JoinDate { get; set; }
        public decimal TotalPoints { get; set; }
        public string Status { get; set; } = "ACTIVE"; // ACTIVE, BLOCKED, EXPIRED
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
    }
}
