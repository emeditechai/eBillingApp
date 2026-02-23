using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    // Model for UC-004: Process Payments
    
    public class PaymentMethod
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public bool RequiresCardInfo { get; set; }
        
        public bool RequiresCardPresent { get; set; }
        
        public bool RequiresApproval { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class Payment
    {
        public int Id { get; set; }
        
        public int OrderId { get; set; }
        
        public int PaymentMethodId { get; set; }
        public string PaymentMethodName { get; set; }
        public string PaymentMethodDisplay { get; set; }
        
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal Amount { get; set; }
        
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal TipAmount { get; set; }
        
        // GST-related columns for storing calculated GST information
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal? GSTAmount { get; set; }
        
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal? CGSTAmount { get; set; }
        
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal? SGSTAmount { get; set; }
        
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal? DiscAmount { get; set; }
        
        [Range(0, 100)]
        public decimal? GST_Perc { get; set; }
        
        [Range(0, 100)]
        public decimal? CGST_Perc { get; set; }
        
        [Range(0, 100)]
        public decimal? SGST_Perc { get; set; }
        
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal? Amount_ExclGST { get; set; }

    [Range(-1000, 1000)]
    public decimal? RoundoffAdjustmentAmt { get; set; }
        
        public int Status { get; set; } // 0=Pending, 1=Approved, 2=Rejected, 3=Voided
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "Pending",
                    1 => "Approved",
                    2 => "Rejected",
                    3 => "Voided",
                    _ => "Unknown"
                };
            }
        }
        
        [StringLength(100)]
        public string ReferenceNumber { get; set; }
        
        [StringLength(4)]
        public string LastFourDigits { get; set; }
        
        [StringLength(50)]
        public string CardType { get; set; }
        
        [StringLength(50)]
        public string AuthorizationCode { get; set; }
        
        [StringLength(500)]
        public string Notes { get; set; }
        
        public int? ProcessedBy { get; set; }
        
        [StringLength(100)]
        public string ProcessedByName { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class SplitBill
    {
        public int Id { get; set; }
        
        public int OrderId { get; set; }
        
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal Amount { get; set; }
        
        [Required]
        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal TotalAmount => Amount + TaxAmount;
        
        public int Status { get; set; } // 0=Open, 1=Paid, 2=Voided
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "Open",
                    1 => "Paid",
                    2 => "Voided",
                    _ => "Unknown"
                };
            }
        }
        
        [StringLength(500)]
        public string Notes { get; set; }
        
        public int? CreatedBy { get; set; }
        
        [StringLength(100)]
        public string CreatedByName { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public List<SplitBillItem> Items { get; set; } = new List<SplitBillItem>();
    }
    
    public class SplitBillItem
    {
        public int Id { get; set; }
        
        public int SplitBillId { get; set; }
        
        public int OrderItemId { get; set; }
        
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
        
        [Required]
        [Range(typeof(decimal), "0.01", "9999999999999999")]
        public decimal Amount { get; set; }
        
        // Navigation properties
        public string MenuItemName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
