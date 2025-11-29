using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    // View Models for UC-004: Process Payments
    
    public class PaymentViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string TableName { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TipAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public int OrderStatus { get; set; }
        public string OrderStatusDisplay { get; set; }
        public int OrderType { get; set; } // 0=Dine-In, 1=Takeout, 2=Delivery, 3=Online
        public string OrderTypeDisplay { get; set; }
        public string CustomerEmailId { get; set; }
        public string CustomerName { get; set; }
        
        // GST breakdown (dynamic based on Default GST % setting)
        public decimal GSTPercentage { get; set; }
        public decimal CGSTAmount { get; set; }
        public decimal SGSTAmount { get; set; }
        
        public List<PaymentMethodViewModel> AvailablePaymentMethods { get; set; } = new List<PaymentMethodViewModel>();
        public List<Payment> Payments { get; set; } = new List<Payment>();
        public List<OrderItemViewModel> OrderItems { get; set; } = new List<OrderItemViewModel>();
        public List<SplitBill> SplitBills { get; set; } = new List<SplitBill>();
        
        // Sum of roundoff adjustments across payments for display on index/print
        public decimal TotalRoundoff { get; set; }
        
        // UPI QR Code properties
        public bool UPIEnabled { get; set; }
        public string UPIQRCodeDataUrl { get; set; }
        public string UPIId { get; set; }
        public string UPIPayeeName { get; set; }
    }
    
    public class PaymentMethodViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public bool RequiresCardInfo { get; set; }
        public bool RequiresCardPresent { get; set; }
        public bool RequiresApproval { get; set; }
    }
    
    public class ProcessPaymentViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        // New: underlying order subtotal (before GST) so discount percent is correctly applied
        public decimal Subtotal { get; set; }
        // New: GST percentage (e.g. 5.00) to recompute GST after discount
        public decimal GSTPercentage { get; set; }
        
        [Required]
        [Display(Name = "Payment Method")]
        public int PaymentMethodId { get; set; }
        
        [Required]
        [Display(Name = "Amount")]
        [Range(0.01, 10000)]
        public decimal Amount { get; set; }

    // Original calculated amount before rounding (sent to DB as the payment amount)
    [Display(Name = "Original Amount")]
    public decimal OriginalAmount { get; set; }
        
        [Display(Name = "Tip Amount")]
        [Range(0, 10000)]
        public decimal TipAmount { get; set; }
        
        [Display(Name = "Card Number (last 4 digits)")]
        [StringLength(4)]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "Please enter the last 4 digits of the card number.")]
        public string LastFourDigits { get; set; }
        
        [Display(Name = "Card Type")]
        public string CardType { get; set; }
        
        [Display(Name = "Authorization Code")]
        [StringLength(50)]
        public string AuthorizationCode { get; set; }
        
        [Display(Name = "Reference Number")]
        [StringLength(100)]
        public string ReferenceNumber { get; set; }

    [Display(Name = "UPI Reference")] 
    [StringLength(100)]
    public string UPIReference { get; set; }

    [Display(Name = "Discount Amount")]
    [Range(0, 10000)]
    public decimal DiscountAmount { get; set; }
        
        [Display(Name = "Notes")]
        [StringLength(500)]
        public string Notes { get; set; }
        
        // Navigation properties
        public List<SelectListItem> AvailablePaymentMethods { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CardTypes { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "VISA", Text = "Visa" },
            new SelectListItem { Value = "MASTERCARD", Text = "MasterCard" },
            new SelectListItem { Value = "AMEX", Text = "American Express" },
            new SelectListItem { Value = "DISCOVER", Text = "Discover" },
            new SelectListItem { Value = "OTHER", Text = "Other" }
        };
        
        // Helper properties
        public bool IsCardPayment { get; set; }
        public bool IsUPIPayment { get; set; }
        
        // Roundoff adjustment (client-calculated, submitted with form)
        [Display(Name = "Roundoff Adjustment")]
        public decimal RoundoffAdjustmentAmt { get; set; }
    }

    // Split payment input line
    public class SplitPaymentItemInput
    {
        [Required]
        public int PaymentMethodId { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal Amount { get; set; }

        [Range(0, 1000000)]
        public decimal TipAmount { get; set; }

        [StringLength(4)]
        public string LastFourDigits { get; set; }

        public string CardType { get; set; }

        [StringLength(50)]
        public string AuthorizationCode { get; set; }

        [StringLength(100)]
        public string ReferenceNumber { get; set; }

        [StringLength(100)]
        public string UPIReference { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public decimal RoundoffAdjustmentAmt { get; set; }

        // Optional: if client computes a canonical pre-round value
        public decimal OriginalAmount { get; set; }
    }

    // ViewModel for processing multiple payments at once
    public class ProcessSplitPaymentsViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal Subtotal { get; set; }
        public decimal GSTPercentage { get; set; }
        public decimal RemainingAmount { get; set; }

        // Discount to apply once for the order (amount or percent)
        public decimal DiscountAmount { get; set; }
        public string DiscountType { get; set; } // "amount" | "percent"

        public List<SplitPaymentItemInput> Items { get; set; } = new List<SplitPaymentItemInput>();
    }
    
    public class VoidPaymentViewModel
    {
        public int PaymentId { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal PaymentAmount { get; set; }
        public decimal TipAmount { get; set; }
        public string PaymentMethodDisplay { get; set; }
        public DateTime PaymentDate { get; set; }
        
        [Required]
        [Display(Name = "Reason for Voiding")]
        [StringLength(500)]
        public string Reason { get; set; }
    }
    
    public class CreateSplitBillViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        
        [Display(Name = "Notes")]
        [StringLength(500)]
        public string Notes { get; set; }
        
        public List<SplitBillItemViewModel> AvailableItems { get; set; } = new List<SplitBillItemViewModel>();
        public List<SplitBillItemViewModel> SelectedItems { get; set; } = new List<SplitBillItemViewModel>();
        
        public decimal SelectedItemsTotal { get; set; }
        public decimal SelectedItemsTax { get; set; }
        public decimal SelectedItemsGrandTotal { get; set; }
    }
    
    public class SplitBillItemViewModel
    {
        public int OrderItemId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int AvailableQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsSelected { get; set; }
        public int SelectedQuantity { get; set; }
    }

    // Payment Dashboard View Models
    public class PaymentDashboardViewModel
    {
        // Today's Analytics Cards
        public decimal TodayTotalPayments { get; set; }
        public decimal TodayTotalGST { get; set; }
        public decimal TodayTotalTips { get; set; }
        
        // Payment Method Breakdown
        public List<PaymentMethodBreakdown> PaymentMethodBreakdowns { get; set; } = new List<PaymentMethodBreakdown>();
        
        // Filter Parameters
        public DateTime FromDate { get; set; } = DateTime.Today;
        public DateTime ToDate { get; set; } = DateTime.Today;
        // Order type filter: All | Foods | Bar
        public string OrderType { get; set; } = "All";
        
        // Payment History
        public List<PaymentHistoryItem> PaymentHistory { get; set; } = new List<PaymentHistoryItem>();
        
        // Pending Payments for Approval
        public List<PendingPaymentItem> PendingPayments { get; set; } = new List<PendingPaymentItem>();
    }

    public class PaymentHistoryItem
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string TableName { get; set; }
        public decimal TotalPayable { get; set; } // Total amount including GST
        public decimal TotalPaid { get; set; }
        public decimal DueAmount { get; set; }
        public decimal GSTAmount { get; set; } // GST amount for the payment
        public DateTime PaymentDate { get; set; }
        public int OrderStatus { get; set; }
        public string OrderStatusDisplay { get; set; }
    }

    public class PaymentMethodBreakdown
    {
        public int PaymentMethodId { get; set; }
        public string PaymentMethodName { get; set; }
        public string PaymentMethodDisplayName { get; set; }
        public decimal TotalAmount { get; set; }
        // New: total GST collected via this payment method (CGST + SGST)
        public decimal TotalGST { get; set; }
        public int TransactionCount { get; set; }
    }
    
    public class PendingPaymentItem
    {
        public int PaymentId { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string TableName { get; set; }
        public string PaymentMethodName { get; set; }
        public string PaymentMethodDisplay { get; set; }
        public decimal Amount { get; set; }
        public decimal TipAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ProcessedByName { get; set; }
        public string ReferenceNumber { get; set; }
        public string LastFourDigits { get; set; }
        public string CardType { get; set; }
        public string Notes { get; set; }
        
        // Enhanced discount information display
        public decimal DiscountAmount { get; set; }
        public decimal OriginalAmount { get; set; } // Amount before discount (Amount + DiscountAmount)
        public bool HasDiscount => DiscountAmount > 0;
        public string DiscountDisplay => HasDiscount ? $"₹{DiscountAmount:F2}" : "No Discount";
        public string OriginalAmountDisplay => HasDiscount ? $"₹{OriginalAmount:F2}" : "-";
    }
}
