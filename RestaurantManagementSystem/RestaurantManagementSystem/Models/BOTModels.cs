using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    /// <summary>
    /// BOT (Beverage Order Ticket) Header - Main beverage order entity
    /// Maps to BOT_Header table
    /// </summary>
    public class BeverageOrderTicket
    {
        public int BOT_ID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string BOT_No { get; set; } // Format: BOT-YYYYMM-####
        
        public int OrderId { get; set; }
        
        [StringLength(50)]
        public string OrderNumber { get; set; }
        
        [StringLength(100)]
        public string TableName { get; set; }
        
        [StringLength(200)]
        public string GuestName { get; set; }
        
        [StringLength(200)]
        public string ServerName { get; set; }
        
        /// <summary>
        /// 0=New/Open, 1=InProgress, 2=Served/Ready, 3=Billed/Closed, 4=Void
        /// </summary>
        public int Status { get; set; }
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "New",
                    1 => "In Progress",
                    2 => "Ready/Served",
                    3 => "Billed",
                    4 => "Void",
                    _ => "Unknown"
                };
            }
        }
        
        public decimal SubtotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        [StringLength(200)]
        public string CreatedBy { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        [StringLength(200)]
        public string UpdatedBy { get; set; }
        
        public DateTime? ServedAt { get; set; }
        public DateTime? BilledAt { get; set; }
        public DateTime? VoidedAt { get; set; }
        
        [StringLength(500)]
        public string VoidReason { get; set; }
        
        // Calculated field
        public int MinutesSinceCreated { get; set; }
        
        // Navigation
        public List<BeverageOrderTicketItem> Items { get; set; } = new List<BeverageOrderTicketItem>();
    }
    
    /// <summary>
    /// BOT Detail - Individual beverage items in a ticket
    /// Maps to BOT_Detail table
    /// </summary>
    public class BeverageOrderTicketItem
    {
        public int BOT_Detail_ID { get; set; }
        public int BOT_ID { get; set; }
        public int OrderItemId { get; set; }
        public int MenuItemId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string MenuItemName { get; set; }
        
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsAlcoholic { get; set; }
        
        [StringLength(500)]
        public string SpecialInstructions { get; set; }
        
        /// <summary>
        /// 0=New, 1=InProgress, 2=Ready, 3=Served
        /// </summary>
        public int Status { get; set; }
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "New",
                    1 => "In Progress",
                    2 => "Ready",
                    3 => "Served",
                    _ => "Unknown"
                };
            }
        }
        
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        
        [StringLength(500)]
        public string Notes { get; set; }
        
        // Calculated
        public int MinutesCooking { get; set; }
    }
    
    /// <summary>
    /// BOT Audit - Immutable audit trail for compliance
    /// Maps to BOT_Audit table
    /// </summary>
    public class BOTAudit
    {
        public int AuditID { get; set; }
        public int BOT_ID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string BOT_No { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Action { get; set; } // CREATE, UPDATE, PRINT, SERVE, VOID, MERGE, AGE_VERIFY
        
        public int? OldStatus { get; set; }
        public int? NewStatus { get; set; }
        public int? UserId { get; set; }
        
        [StringLength(200)]
        public string UserName { get; set; }
        
        [StringLength(500)]
        public string DeviceInfo { get; set; }
        
        [StringLength(500)]
        public string Reason { get; set; }
        
        public DateTime Timestamp { get; set; }
        
        public string AdditionalData { get; set; } // JSON
    }
    
    /// <summary>
    /// BOT Bill - Separate billing entity for beverages
    /// Maps to BOT_Bills table
    /// </summary>
    public class BOTBill
    {
        public int BillID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string BillNo { get; set; } // Format: BOTBILL-YYYYMMDD-####
        
        public int BOT_ID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string BOT_No { get; set; }
        
        public int OrderId { get; set; }
        
        [StringLength(50)]
        public string OrderNumber { get; set; }
        
        public decimal SubtotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ExciseAmount { get; set; }
        public decimal VATAmount { get; set; }
        public decimal GSTAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        
        /// <summary>
        /// 0=Unpaid, 1=Partial, 2=Paid
        /// </summary>
        public int PaymentStatus { get; set; }
        
        public string PaymentStatusDisplay
        {
            get
            {
                return PaymentStatus switch
                {
                    0 => "Unpaid",
                    1 => "Partial",
                    2 => "Paid",
                    _ => "Unknown"
                };
            }
        }
        
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        [StringLength(200)]
        public string CreatedBy { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        // Navigation
        public List<BOTPayment> Payments { get; set; } = new List<BOTPayment>();
    }
    
    /// <summary>
    /// BOT Payment - Individual payments against a BOT bill
    /// Maps to BOT_Payments table
    /// </summary>
    public class BOTPayment
    {
        public int PaymentID { get; set; }
        public int BillID { get; set; }
        
        [Required]
        [StringLength(50)]
        public string BillNo { get; set; }
        
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } // CASH, CARD, UPI, etc.
        
        public decimal Amount { get; set; }
        
        [StringLength(200)]
        public string TransactionRef { get; set; }
        
        public DateTime PaymentDate { get; set; }
        
        [StringLength(200)]
        public string ReceivedBy { get; set; }
        
        [StringLength(500)]
        public string Notes { get; set; }
    }
    
    /// <summary>
    /// BOT Dashboard Stats - Aggregated metrics
    /// </summary>
    public class BOTDashboardStats
    {
        public int NewBOTsCount { get; set; }
        public int InProgressBOTsCount { get; set; }
        public int ReadyBOTsCount { get; set; }
        public int BilledTodayCount { get; set; }
        public int TotalActiveBOTs { get; set; }
        public double AvgPrepTimeMinutes { get; set; }
        
        // Item counts per status
        public int NewItemsCount { get; set; }
        public int InProgressItemsCount { get; set; }
        public int ReadyItemsCount { get; set; }
    }
}
