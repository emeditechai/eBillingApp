using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantManagementSystem.ViewModels
{
    public class CollectionRegisterViewModel
    {
        public CollectionRegisterFilter Filter { get; set; } = new CollectionRegisterFilter();
        public List<SelectListItem> PaymentMethods { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Counters { get; set; } = new List<SelectListItem>();
        public List<CollectionRegisterRow> Rows { get; set; } = new List<CollectionRegisterRow>();
        public CollectionRegisterSummary Summary { get; set; } = new CollectionRegisterSummary();
    }

    public class CollectionRegisterFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? PaymentMethodId { get; set; }
        public string PaymentMethodName { get; set; } = "ALL";
        public int? CounterId { get; set; }
        public string CounterName { get; set; } = "ALL";
        public int? UserId { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
    }

    public class CollectionRegisterRow
    {
        public string OrderNo { get; set; }
        public string TableNo { get; set; }
        public string Username { get; set; }
        public int? CounterId { get; set; }
        public string CounterName { get; set; }
        public decimal ActualBillAmount { get; set; } // Subtotal - Discount (before GST)
        public decimal DiscountAmount { get; set; }
        public decimal GSTAmount { get; set; } // CGST + SGST
        public decimal RoundOffAmount { get; set; }
        public decimal ReceiptAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string Details { get; set; }
        public DateTime PaymentDate { get; set; }
        public int PaymentStatus { get; set; } // 1=Approved, 3=Void/Refund
    }

    public class CollectionRegisterSummary
    {
        public int TotalTransactions { get; set; }
        public decimal TotalActualAmount { get; set; } // Sum of (Subtotal - Discount)
        public decimal TotalDiscount { get; set; }
        public decimal TotalGST { get; set; } // Sum of GST amounts
        public decimal TotalRoundOff { get; set; }
        public decimal TotalReceiptAmount { get; set; }
    }
}
