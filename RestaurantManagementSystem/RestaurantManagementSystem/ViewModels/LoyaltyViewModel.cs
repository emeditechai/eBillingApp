using System;
using System.Collections.Generic;

namespace RestaurantManagementSystem.ViewModels
{
    public class LoyaltyConfigViewModel
    {
        public LoyaltyConfigItem? RestaurantConfig { get; set; }
        public LoyaltyConfigItem? BarConfig { get; set; }
    }

    public class LoyaltyConfigItem
    {
        public int Id { get; set; }
        public string OutletType { get; set; } = string.Empty;
        public decimal EarnRate { get; set; }
        public decimal RedemptionValue { get; set; }
        public decimal MinBillToEarn { get; set; }
        public decimal MaxPointsPerBill { get; set; }
        public int ExpiryDays { get; set; }
        public string EligiblePaymentModes { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        
        // Display properties
        public string EarnRateDisplay => $"1 point per ₹{EarnRate} spend";
        public string RedemptionDisplay => $"1 point = ₹{RedemptionValue}";
        public string MinBillDisplay => MinBillToEarn.ToString("N0");
        public string MaxPointsDisplay => MaxPointsPerBill.ToString("N0");
        public string ExpiryDisplay => $"{ExpiryDays} days";
        public string PaymentModesDisplay => EligiblePaymentModes;
    }

    public class GuestLoyaltyViewModel
    {
        public string CardNo { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public DateTime JoinDate { get; set; }
        public decimal TotalPoints { get; set; }
        public string Status { get; set; } = string.Empty;
        public int DaysSinceJoined { get; set; }
    }

    public class LoyaltyRedemptionRequest
    {
        public string CardNo { get; set; } = string.Empty;
        public decimal PointsToRedeem { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public string OutletType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class LoyaltyRedemptionResponse
    {
        public bool Success { get; set; }
        public decimal RedemptionValue { get; set; }
        public decimal RemainingPoints { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LoyaltyEarnRequest
    {
        public string CardNo { get; set; } = string.Empty;
        public string BillNo { get; set; } = string.Empty;
        public decimal BillAmount { get; set; }
        public string OutletType { get; set; } = string.Empty;
        public string PaymentMode { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class LoyaltyEarnResponse
    {
        public bool Success { get; set; }
        public decimal PointsEarned { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LoyaltyPointsCalculation
    {
        public bool IsEligible { get; set; }
        public decimal PointsEarned { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
