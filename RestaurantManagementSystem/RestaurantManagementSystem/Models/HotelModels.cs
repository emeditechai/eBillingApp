using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    /// <summary>
    /// Represents a room type in the hotel
    /// </summary>
    public class RoomType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Room Type Name")]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Base Price")]
        [Range(0, 100000)]
        public decimal BasePrice { get; set; }

        [Display(Name = "Max Occupancy")]
        [Range(1, 20)]
        public int MaxOccupancy { get; set; } = 2;

        [Display(Name = "Bed Type")]
        [StringLength(50)]
        public string BedType { get; set; } = "Queen";

        [Display(Name = "Room Size (sq ft)")]
        public int? RoomSize { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents an amenity available in rooms or hotel
    /// </summary>
    public class HotelAmenity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(200)]
        public string Description { get; set; }

        [StringLength(50)]
        public string Icon { get; set; } = "fa-check";

        [Display(Name = "Category")]
        [StringLength(50)]
        public string Category { get; set; } = "General";

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Represents a room in the hotel
    /// </summary>
    public class HotelRoom
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Room Number")]
        public string RoomNumber { get; set; }

        [Display(Name = "Floor")]
        public int Floor { get; set; } = 1;

        [Required]
        [Display(Name = "Room Type")]
        public int RoomTypeId { get; set; }

        // Navigation property
        public RoomType RoomType { get; set; }

        [Display(Name = "Status")]
        public RoomStatus Status { get; set; } = RoomStatus.Available;

        [StringLength(500)]
        public string Notes { get; set; }

        [Display(Name = "Has View")]
        public bool HasView { get; set; }

        [Display(Name = "View Type")]
        [StringLength(50)]
        public string ViewType { get; set; }

        [Display(Name = "Is Smoking")]
        public bool IsSmoking { get; set; }

        [Display(Name = "Is Accessible")]
        public bool IsAccessible { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Calculated property for display
        public string DisplayName => $"Room {RoomNumber} - {RoomType?.Name ?? "Unknown"}";
    }

    /// <summary>
    /// Room status enumeration
    /// </summary>
    public enum RoomStatus
    {
        Available = 0,
        Occupied = 1,
        Reserved = 2,
        Maintenance = 3,
        Cleaning = 4,
        OutOfOrder = 5
    }

    /// <summary>
    /// Represents a hotel guest
    /// </summary>
    public class HotelGuest
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(100)]
        public string State { get; set; }

        [StringLength(100)]
        public string Country { get; set; } = "India";

        [StringLength(20)]
        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; }

        [StringLength(50)]
        [Display(Name = "ID Type")]
        public string IdType { get; set; }

        [StringLength(100)]
        [Display(Name = "ID Number")]
        public string IdNumber { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string Nationality { get; set; } = "Indian";

        [Display(Name = "VIP Status")]
        public bool IsVip { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Represents a hotel booking
    /// </summary>
    public class HotelBooking
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Booking Number")]
        public string BookingNumber { get; set; }

        [Required]
        [Display(Name = "Room")]
        public int RoomId { get; set; }

        // Navigation property
        public HotelRoom Room { get; set; }

        [Required]
        [Display(Name = "Guest")]
        public int GuestId { get; set; }

        // Navigation property
        public HotelGuest Guest { get; set; }

        [Required]
        [Display(Name = "Check-in Date")]
        [DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; }

        [Required]
        [Display(Name = "Check-out Date")]
        [DataType(DataType.Date)]
        public DateTime CheckOutDate { get; set; }

        [Display(Name = "Actual Check-in")]
        public DateTime? ActualCheckIn { get; set; }

        [Display(Name = "Actual Check-out")]
        public DateTime? ActualCheckOut { get; set; }

        [Required]
        [Display(Name = "Number of Guests")]
        [Range(1, 20)]
        public int NumberOfGuests { get; set; } = 1;

        [Display(Name = "Number of Adults")]
        [Range(1, 10)]
        public int NumberOfAdults { get; set; } = 1;

        [Display(Name = "Number of Children")]
        [Range(0, 10)]
        public int NumberOfChildren { get; set; } = 0;

        [Display(Name = "Room Rate")]
        [Range(0, 100000)]
        public decimal RoomRate { get; set; }

        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Extra Charges")]
        public decimal ExtraCharges { get; set; }

        [Display(Name = "Discount")]
        public decimal Discount { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Advance Payment")]
        public decimal AdvancePayment { get; set; }

        [Display(Name = "Balance Due")]
        public decimal BalanceDue => TotalAmount - AdvancePayment;

        [Display(Name = "Status")]
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        [Display(Name = "Payment Status")]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [StringLength(500)]
        [Display(Name = "Special Requests")]
        public string SpecialRequests { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        [Display(Name = "Source")]
        [StringLength(50)]
        public string BookingSource { get; set; } = "Direct";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Created By")]
        public int? CreatedBy { get; set; }

        // Calculated properties
        [Display(Name = "Number of Nights")]
        public int NumberOfNights => (CheckOutDate - CheckInDate).Days;

        public bool IsCheckedIn => ActualCheckIn.HasValue && !ActualCheckOut.HasValue;
        public bool IsCheckedOut => ActualCheckOut.HasValue;
    }

    /// <summary>
    /// Booking status enumeration
    /// </summary>
    public enum BookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        CheckedIn = 2,
        CheckedOut = 3,
        Cancelled = 4,
        NoShow = 5
    }

    /// <summary>
    /// Payment status enumeration
    /// </summary>
    public enum PaymentStatus
    {
        Pending = 0,
        PartiallyPaid = 1,
        Paid = 2,
        Refunded = 3
    }

    /// <summary>
    /// Room-amenity mapping
    /// </summary>
    public class RoomTypeAmenity
    {
        public int Id { get; set; }
        public int RoomTypeId { get; set; }
        public int AmenityId { get; set; }
    }
}
