using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Models
{
    [Table("tbl_EmailTemplates")]
    public class EmailTemplate
    {
        [Key]
        public int EmailTemplateID { get; set; }

        [Required]
        [StringLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string TemplateType { get; set; } = string.Empty; // Birthday, Anniversary, Custom, Promotional

        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string BodyHtml { get; set; } = string.Empty;

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public bool IsDefault { get; set; } = false;

        public int? CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("CreatedBy")]
        public virtual User? CreatedByUser { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual User? UpdatedByUser { get; set; }
    }

    [Table("tbl_EmailCampaignHistory")]
    public class EmailCampaignHistory
    {
        [Key]
        public int CampaignHistoryID { get; set; }

        [Required]
        [StringLength(50)]
        public string CampaignType { get; set; } = string.Empty;

        [Required]
        public int GuestId { get; set; }

        [Required]
        [StringLength(255)]
        public string GuestName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string GuestEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string EmailSubject { get; set; } = string.Empty;

        [Required]
        public string EmailBody { get; set; } = string.Empty;

        [Required]
        public DateTime SentAt { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty; // Success, Failed

        public string? ErrorMessage { get; set; }

        public int? ProcessingTimeMs { get; set; }

        public int? SentBy { get; set; }

        // Navigation Properties
        [ForeignKey("SentBy")]
        public virtual User? SentByUser { get; set; }
    }
}
