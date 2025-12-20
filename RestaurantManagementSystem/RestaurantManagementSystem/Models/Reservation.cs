using System;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Customer Name")]
        public string GuestName { get; set; }

        // Property that controller expects 
        public string CustomerName 
        { 
            get { return GuestName; }
            set { GuestName = value; }
        }
        
        // Property for full name that some controllers expect
        public string FullName
        {
            get { return GuestName; }
            set { GuestName = value; }
        }

        [Required]
        [Phone]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email Address")]
        public string? EmailAddress { get; set; }

        // Property that controller expects
        public string? Email
        {
            get { return EmailAddress; }
            set { EmailAddress = value; }
        }

        [Required]
        [Display(Name = "Party Size")]
        [Range(1, 50, ErrorMessage = "Party size must be between 1 and 50")]
        public int PartySize { get; set; }

        [Required]
        [Display(Name = "Reservation Date")]
        [DataType(DataType.Date)]
        public DateTime ReservationDate { get; set; }

        [Required]
        [Display(Name = "Reservation Time")]
        [DataType(DataType.Time)]
        public DateTime ReservationTime { get; set; }

        [StringLength(200)]
        public string? SpecialRequests { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? TableId { get; set; }
        
        [Display(Name = "Table Number")]
        public string? TableNumber { get; set; }

        [Display(Name = "Section")]
        public string? TableSection { get; set; }

        [Display(Name = "Status")]
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Updated At")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Reminder Sent")]
        public bool ReminderSent { get; set; }

        [Display(Name = "No-Show")]
        public bool NoShow { get; set; }

        // This calculated property helps with displaying the full date and time in one field
        [Display(Name = "Reservation Date & Time")]
        public DateTime ReservationDateTime
        {
            get
            {
                // Combine the date and time into a single DateTime
                return ReservationDate.Date.Add(ReservationTime.TimeOfDay);
            }
        }

        // Returns true if the reservation is for today
        public bool IsToday
        {
            get
            {
                return ReservationDate.Date == DateTime.Today;
            }
        }

        // Helper method to check if a reservation is upcoming
        public bool IsUpcoming
        {
            get
            {
                return ReservationDateTime > DateTime.Now;
            }
        }

        // Helper method to check if a guest is currently seated
        public bool IsCurrentlySeated
        {
            get
            {
                return Status == ReservationStatus.Seated &&
                       ReservationDateTime.Date == DateTime.Today;
            }
        }
    }

    public enum ReservationStatus
    {
        Pending = 0,
        Confirmed = 1,
        Seated = 2,
        Completed = 3,
        Cancelled = 4,
        NoShow = 5,
        Waitlisted = 6
    }
}
