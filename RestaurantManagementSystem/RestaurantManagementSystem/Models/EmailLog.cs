using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Models
{
    [Table("tbl_EmailLog")]
    public class EmailLog
    {
        [Key]
        public int EmailLogID { get; set; }

        // Email Details
        [Required]
        [StringLength(255)]
        public string FromEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string ToEmail { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Subject { get; set; }

        public string? EmailBody { get; set; }

        // SMTP Configuration Used
        [Required]
        [StringLength(255)]
        public string SmtpServer { get; set; } = string.Empty;

        [Required]
        public int SmtpPort { get; set; }

        [Required]
        public bool EnableSSL { get; set; }

        [Required]
        [StringLength(255)]
        public string SmtpUsername { get; set; } = string.Empty;

        // Status and Error Information
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty; // "Success" or "Failed"

        public string? ErrorMessage { get; set; }

        [StringLength(50)]
        public string? ErrorCode { get; set; }

        // Metadata
        [Required]
        public DateTime SentAt { get; set; } = DateTime.Now;

        public int? ProcessingTimeMs { get; set; }

        [StringLength(50)]
        public string? IPAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        // Audit Fields
        public int? CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("CreatedBy")]
        public virtual User? CreatedByUser { get; set; }
    }
}
