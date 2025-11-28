using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.ViewModels
{
    public class MailConfigurationViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "SMTP Server is required")]
        [StringLength(200, ErrorMessage = "SMTP Server cannot exceed 200 characters")]
        [Display(Name = "SMTP Server")]
        public string SmtpServer { get; set; }

        [Required(ErrorMessage = "SMTP Port is required")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "SMTP Port")]
        public int SmtpPort { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(200, ErrorMessage = "Username cannot exceed 200 characters")]
        [Display(Name = "Username")]
        public string SmtpUsername { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(500, ErrorMessage = "Password cannot exceed 500 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string SmtpPassword { get; set; }

        [Display(Name = "Enable SSL")]
        public bool EnableSSL { get; set; }

        [Required(ErrorMessage = "From Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(200, ErrorMessage = "From Email cannot exceed 200 characters")]
        [Display(Name = "From Email")]
        public string FromEmail { get; set; }

        [Required(ErrorMessage = "From Name is required")]
        [StringLength(200, ErrorMessage = "From Name cannot exceed 200 characters")]
        [Display(Name = "From Name")]
        public string FromName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(200, ErrorMessage = "Admin Notification Email cannot exceed 200 characters")]
        [Display(Name = "Admin Notification Email")]
        public string AdminNotificationEmail { get; set; }

        [Display(Name = "Activate Email Service")]
        public bool IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Helper property for test email
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Test Email Address")]
        public string TestEmailAddress { get; set; }
    }
}
