using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class PosOrderCreateViewModel
    {
        [Required]
        [Range(1, 2, ErrorMessage = "POS Order supports only Takeout (1) or Delivery (2).")]
        public int OrderType { get; set; } = 1; // 1=Takeout, 2=Delivery

        [Display(Name = "Customer Name")]
        [StringLength(100)]
        public string? CustomerName { get; set; }

        [Display(Name = "Customer Phone")]
        [StringLength(30)]
        public string? CustomerPhone { get; set; }

        [Display(Name = "Customer Email")]
        [EmailAddress]
        [StringLength(200)]
        public string? CustomerEmailId { get; set; }

        [Display(Name = "Address")]
        [StringLength(500)]
        public string? CustomerAddress { get; set; }

        [Display(Name = "Special Instructions")]
        [StringLength(500)]
        public string? SpecialInstructions { get; set; }
    }

    public class PosOrderPageViewModel
    {
        public int? OrderId { get; set; }
        public string? OrderNumber { get; set; }

        public PosOrderCreateViewModel Create { get; set; } = new PosOrderCreateViewModel();

        public OrderViewModel? Order { get; set; }

        // POS inline payment panel (posts to existing PaymentController via AJAX)
        public ProcessPaymentViewModel Payment { get; set; } = new ProcessPaymentViewModel();
    }
}
