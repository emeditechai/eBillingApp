using System;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.ViewModels
{
    public class EmailServicesViewModel
    {
        public List<BirthdayGuestViewModel> TodayBirthdays { get; set; } = new();
        public List<AnniversaryGuestViewModel> TodayAnniversaries { get; set; } = new();
        public List<GuestEmailViewModel> AllGuests { get; set; } = new();
        public List<EmailTemplateViewModel> CustomTemplates { get; set; } = new();
    }

    public class BirthdayGuestViewModel
    {
        public int GuestId { get; set; }
        public string GuestName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public int Age { get; set; }
        public bool AlreadySent { get; set; }
        public DateTime? LastSentDate { get; set; }
    }

    public class AnniversaryGuestViewModel
    {
        public int GuestId { get; set; }
        public string GuestName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime? AnniversaryDate { get; set; }
        public int Years { get; set; }
        public bool AlreadySent { get; set; }
        public DateTime? LastSentDate { get; set; }
    }

    public class GuestEmailViewModel
    {
        public int GuestId { get; set; }
        public string GuestName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime? LastVisitDate { get; set; }
        public int TotalVisits { get; set; }
    }

    public class EmailTemplateViewModel
    {
        public int EmailTemplateID { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }

    public class SendCustomEmailRequest
    {
        [Required]
        public List<int> GuestIds { get; set; } = new();

        [Required]
        public int? TemplateId { get; set; }

        [StringLength(500)]
        public string? CustomSubject { get; set; }

        public string? CustomBody { get; set; }
    }

    public class AutoFireEmailRequest
    {
        [Required]
        public string EmailType { get; set; } = string.Empty; // "Birthday" or "Anniversary"

        [Required]
        public List<int> GuestIds { get; set; } = new();
    }

    public class EmailCampaignResultViewModel
    {
        public int TotalAttempted { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }
}
