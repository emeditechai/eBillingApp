using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    /// <summary>
    /// View model for the hotel dashboard
    /// </summary>
    public class HotelDashboardViewModel
    {
        public int TotalRooms { get; set; }
        public int AvailableRooms { get; set; }
        public int OccupiedRooms { get; set; }
        public int ReservedRooms { get; set; }
        public int MaintenanceRooms { get; set; }

        public int TodayCheckIns { get; set; }
        public int TodayCheckOuts { get; set; }
        public int PendingCheckIns { get; set; }
        public int PendingCheckOuts { get; set; }

        public decimal TodayRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal AverageOccupancyRate { get; set; }

        public List<HotelBooking> RecentBookings { get; set; } = new List<HotelBooking>();
        public List<HotelBooking> TodaysArrivals { get; set; } = new List<HotelBooking>();
        public List<HotelBooking> TodaysDepartures { get; set; } = new List<HotelBooking>();
        public List<HotelRoom> RoomsByFloor { get; set; } = new List<HotelRoom>();

        // Room type distribution
        public Dictionary<string, int> RoomTypeDistribution { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// View model for room search and availability
    /// </summary>
    public class RoomSearchViewModel
    {
        [Required]
        [Display(Name = "Check-in Date")]
        [DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Check-out Date")]
        [DataType(DataType.Date)]
        public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(1);

        [Display(Name = "Number of Adults")]
        [Range(1, 10)]
        public int Adults { get; set; } = 2;

        [Display(Name = "Number of Children")]
        [Range(0, 10)]
        public int Children { get; set; } = 0;

        [Display(Name = "Room Type")]
        public int? RoomTypeId { get; set; }

        public List<RoomAvailabilityResult> AvailableRooms { get; set; } = new List<RoomAvailabilityResult>();
        public List<RoomType> RoomTypes { get; set; } = new List<RoomType>();
    }

    /// <summary>
    /// Room availability result
    /// </summary>
    public class RoomAvailabilityResult
    {
        public HotelRoom Room { get; set; }
        public RoomType RoomType { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal TotalPrice { get; set; }
        public int AvailableCount { get; set; }
        public List<HotelAmenity> Amenities { get; set; } = new List<HotelAmenity>();
    }

    /// <summary>
    /// View model for creating/editing bookings
    /// </summary>
    public class BookingFormViewModel
    {
        public HotelBooking Booking { get; set; } = new HotelBooking();
        public HotelGuest Guest { get; set; } = new HotelGuest();

        public List<HotelRoom> AvailableRooms { get; set; } = new List<HotelRoom>();
        public List<RoomType> RoomTypes { get; set; } = new List<RoomType>();
        public List<HotelGuest> ExistingGuests { get; set; } = new List<HotelGuest>();

        public bool IsNewGuest { get; set; } = true;
        public int? SelectedGuestId { get; set; }
    }

    /// <summary>
    /// View model for room details
    /// </summary>
    public class RoomDetailsViewModel
    {
        public HotelRoom Room { get; set; }
        public RoomType RoomType { get; set; }
        public List<HotelAmenity> Amenities { get; set; } = new List<HotelAmenity>();
        public List<HotelBooking> UpcomingBookings { get; set; } = new List<HotelBooking>();
        public HotelBooking CurrentBooking { get; set; }
    }

    /// <summary>
    /// View model for room type management
    /// </summary>
    public class RoomTypeFormViewModel
    {
        public RoomType RoomType { get; set; } = new RoomType();
        public List<HotelAmenity> AllAmenities { get; set; } = new List<HotelAmenity>();
        public List<int> SelectedAmenityIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// View model for guest management
    /// </summary>
    public class GuestListViewModel
    {
        public List<HotelGuest> Guests { get; set; } = new List<HotelGuest>();
        public string SearchTerm { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// View model for booking list
    /// </summary>
    public class BookingListViewModel
    {
        public List<HotelBooking> Bookings { get; set; } = new List<HotelBooking>();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public BookingStatus? Status { get; set; }
        public string SearchTerm { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// View model for check-in process
    /// </summary>
    public class CheckInViewModel
    {
        public HotelBooking Booking { get; set; }
        public HotelGuest Guest { get; set; }
        public HotelRoom Room { get; set; }

        [Display(Name = "ID Verified")]
        public bool IdVerified { get; set; }

        [Display(Name = "Deposit Collected")]
        public decimal DepositAmount { get; set; }

        [Display(Name = "Key Card Issued")]
        public bool KeyCardIssued { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    /// <summary>
    /// View model for check-out process
    /// </summary>
    public class CheckOutViewModel
    {
        public HotelBooking Booking { get; set; }
        public HotelGuest Guest { get; set; }
        public HotelRoom Room { get; set; }

        public decimal RoomCharges { get; set; }
        public decimal ExtraCharges { get; set; }
        public decimal MinibarCharges { get; set; }
        public decimal LaundryCharges { get; set; }
        public decimal RestaurantCharges { get; set; }
        public decimal OtherCharges { get; set; }
        public decimal TotalCharges { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; }

        [Display(Name = "Room Inspected")]
        public bool RoomInspected { get; set; }

        [Display(Name = "Key Returned")]
        public bool KeyReturned { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    /// <summary>
    /// View model for room floor map
    /// </summary>
    public class FloorMapViewModel
    {
        public int Floor { get; set; }
        public List<HotelRoom> Rooms { get; set; } = new List<HotelRoom>();
    }

    /// <summary>
    /// View model for hotel reports
    /// </summary>
    public class HotelReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public int TotalBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int NoShowBookings { get; set; }

        public decimal TotalRevenue { get; set; }
        public decimal AverageRoomRate { get; set; }
        public decimal OccupancyRate { get; set; }
        public decimal AverageLengthOfStay { get; set; }

        public List<BookingsByRoomType> BookingsByRoomType { get; set; } = new List<BookingsByRoomType>();
        public List<DailyOccupancy> DailyOccupancy { get; set; } = new List<DailyOccupancy>();
    }

    public class BookingsByRoomType
    {
        public string RoomTypeName { get; set; }
        public int BookingCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class DailyOccupancy
    {
        public DateTime Date { get; set; }
        public int OccupiedRooms { get; set; }
        public int TotalRooms { get; set; }
        public decimal OccupancyRate { get; set; }
    }
}
