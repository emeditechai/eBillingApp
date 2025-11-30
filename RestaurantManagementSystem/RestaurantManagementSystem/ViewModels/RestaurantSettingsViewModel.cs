using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RestaurantManagementSystem.ViewModels
{
    public class RestaurantSettingsViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Restaurant name is required")]
        [StringLength(100, ErrorMessage = "Restaurant name cannot exceed 100 characters")]
        [Display(Name = "Restaurant Name")]
        public string RestaurantName { get; set; }

        [Required(ErrorMessage = "Street address is required")]
        [StringLength(200, ErrorMessage = "Street address cannot exceed 200 characters")]
        [Display(Name = "Street Address")]
        public string StreetAddress { get; set; }

        [Required(ErrorMessage = "City is required")]
        [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
        public string City { get; set; }

        [Required(ErrorMessage = "State is required")]
        [StringLength(50, ErrorMessage = "State cannot exceed 50 characters")]
        public string State { get; set; }

        [Required(ErrorMessage = "Pincode is required")]
        [StringLength(10, ErrorMessage = "Pincode cannot exceed 10 characters")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be a 6-digit number")]
        public string Pincode { get; set; }

        [Required(ErrorMessage = "Country is required")]
        [StringLength(50, ErrorMessage = "Country cannot exceed 50 characters")]
        public string Country { get; set; }

        [Required(ErrorMessage = "GST code is required")]
        [StringLength(15, ErrorMessage = "GST code cannot exceed 15 characters")]
        [RegularExpression(@"^\d{2}[A-Z]{5}\d{4}[A-Z]{1}[A-Z\d]{1}[Z]{1}[A-Z\d]{1}$", ErrorMessage = "Invalid GST code format")]
        [Display(Name = "GST Code")]
        public string GSTCode { get; set; }

        [Phone]
        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        [RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Invalid phone number format")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [EmailAddress]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Url]
        [StringLength(100, ErrorMessage = "Website URL cannot exceed 100 characters")]
        [Display(Name = "Website")]
        public string Website { get; set; }

        // Added to handle file upload for logo
        [Display(Name = "Restaurant Logo")]
        public IFormFile LogoFile { get; set; }

        [StringLength(200)]
        [Display(Name = "Logo Path")]
        public string LogoPath { get; set; }

    [StringLength(32)]
    [Display(Name = "FSSAI No")]
    public string FssaiNo { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Currency symbol cannot exceed 50 characters")]
        [Display(Name = "Currency Symbol")]
        public string CurrencySymbol { get; set; } = "₹";

        [Required]
        [Range(0, 100, ErrorMessage = "Default GST percentage must be between 0 and 100")]
        [Display(Name = "Default GST Percentage")]
        public decimal DefaultGSTPercentage { get; set; } = 5.00m;

    [Required]
    [Range(0, 100, ErrorMessage = "Take Away GST percentage must be between 0 and 100")]
    [Display(Name = "Take Away GST Percentage")]
    public decimal TakeAwayGSTPercentage { get; set; } = 5.00m;

    [Required]
    [Range(0, 100, ErrorMessage = "Bar GST percentage must be between 0 and 100")]
    [Display(Name = "Bar GST Percentage")]
    public decimal BarGSTPerc { get; set; } = 5.00m;

        // Parameter Setup Section
        [Display(Name = "Is Default GST Required")]
        public bool IsDefaultGSTRequired { get; set; } = true;
        
        [Display(Name = "Is Take Away GST Required")]
        public bool IsTakeAwayGSTRequired { get; set; } = true;

            [Display(Name = "Take Away GST Included Required")]
            public bool IsTakeawayIncludedGSTReq { get; set; } = false;

    [Display(Name = "Is Discount Approval Required")]
    public bool IsDiscountApprovalRequired { get; set; } = false;

    [Display(Name = "Card Payment Approval Required")]
    public bool IsCardPaymentApprovalRequired { get; set; } = false;

    [Display(Name = "KOT Bill Print Required")]
    public bool IsKOTBillPrintRequired { get; set; } = false;

    [Display(Name = "Automatic Bill Send Email")]
    public bool IsReqAutoSentbillEmail { get; set; } = false;
        
        [Required(ErrorMessage = "Bill Format is required")]
        [Display(Name = "Bill Format")]
        public string BillFormat { get; set; } = "A4";

        // Additional display-only information
        [Display(Name = "Created On")]
        public string CreatedAt { get; set; }

        [Display(Name = "Last Updated")]
        public string UpdatedAt { get; set; }
    }
}