using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RestaurantManagementSystem.Controllers
{
    public class HotelController : Controller
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;

        public HotelController(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = _config.GetConnectionString("DefaultConnection");
        }

        #region Dashboard

        // GET: Hotel Dashboard
        public IActionResult Dashboard()
        {
            var viewModel = new HotelDashboardViewModel();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Get room statistics
                    var rooms = GetAllRooms();
                    viewModel.TotalRooms = rooms.Count;
                    viewModel.AvailableRooms = rooms.Count(r => r.Status == RoomStatus.Available);
                    viewModel.OccupiedRooms = rooms.Count(r => r.Status == RoomStatus.Occupied);
                    viewModel.ReservedRooms = rooms.Count(r => r.Status == RoomStatus.Reserved);
                    viewModel.MaintenanceRooms = rooms.Count(r => r.Status == RoomStatus.Maintenance || 
                                                                   r.Status == RoomStatus.Cleaning || 
                                                                   r.Status == RoomStatus.OutOfOrder);

                    // Get today's arrivals and departures
                    var today = DateTime.Today;
                    var allBookings = GetBookingsByDateRange(today.AddDays(-30), today.AddDays(30));
                    
                    viewModel.TodaysArrivals = allBookings
                        .Where(b => b.CheckInDate == today && b.Status == BookingStatus.Confirmed)
                        .ToList();
                    viewModel.TodaysDepartures = allBookings
                        .Where(b => b.CheckOutDate == today && b.Status == BookingStatus.CheckedIn)
                        .ToList();
                    viewModel.TodayCheckIns = viewModel.TodaysArrivals.Count;
                    viewModel.TodayCheckOuts = viewModel.TodaysDepartures.Count;
                    viewModel.PendingCheckIns = viewModel.TodaysArrivals.Count(b => !b.ActualCheckIn.HasValue);
                    viewModel.PendingCheckOuts = viewModel.TodaysDepartures.Count(b => !b.ActualCheckOut.HasValue);

                    // Recent bookings
                    viewModel.RecentBookings = allBookings
                        .OrderByDescending(b => b.CreatedAt)
                        .Take(10)
                        .ToList();

                    // Rooms by floor
                    viewModel.RoomsByFloor = rooms;

                    // Room type distribution
                    var roomTypes = GetAllRoomTypes();
                    foreach (var roomType in roomTypes)
                    {
                        viewModel.RoomTypeDistribution[roomType.Name] = rooms.Count(r => r.RoomTypeId == roomType.Id);
                    }

                    // Calculate occupancy rate
                    if (viewModel.TotalRooms > 0)
                    {
                        viewModel.AverageOccupancyRate = (decimal)(viewModel.OccupiedRooms + viewModel.ReservedRooms) / viewModel.TotalRooms * 100;
                    }

                    // Calculate revenue (from completed bookings this month)
                    var completedThisMonth = allBookings
                        .Where(b => b.Status == BookingStatus.CheckedOut && 
                                   b.ActualCheckOut.HasValue &&
                                   b.ActualCheckOut.Value.Month == today.Month &&
                                   b.ActualCheckOut.Value.Year == today.Year)
                        .ToList();
                    viewModel.MonthlyRevenue = completedThisMonth.Sum(b => b.TotalAmount);
                    viewModel.TodayRevenue = completedThisMonth
                        .Where(b => b.ActualCheckOut.Value.Date == today)
                        .Sum(b => b.TotalAmount);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading dashboard: {ex.Message}";
            }

            return View(viewModel);
        }

        #endregion

        #region Rooms

        // GET: Room List
        public IActionResult Rooms(int? floor = null, int? roomTypeId = null)
        {
            var rooms = GetAllRooms();
            
            if (floor.HasValue)
            {
                rooms = rooms.Where(r => r.Floor == floor.Value).ToList();
                ViewBag.SelectedFloor = floor.Value;
            }

            if (roomTypeId.HasValue)
            {
                rooms = rooms.Where(r => r.RoomTypeId == roomTypeId.Value).ToList();
                ViewBag.SelectedRoomTypeId = roomTypeId.Value;
            }

            ViewBag.RoomTypes = GetAllRoomTypes();
            ViewBag.Floors = rooms.Select(r => r.Floor).Distinct().OrderBy(f => f).ToList();

            return View(rooms);
        }

        // GET: Room Details
        public IActionResult RoomDetails(int id)
        {
            var room = GetRoomById(id);
            if (room == null)
            {
                TempData["ErrorMessage"] = "Room not found.";
                return RedirectToAction("Rooms");
            }

            var viewModel = new RoomDetailsViewModel
            {
                Room = room,
                RoomType = GetRoomTypeById(room.RoomTypeId),
                Amenities = GetAmenitiesByRoomTypeId(room.RoomTypeId),
                UpcomingBookings = GetUpcomingBookingsForRoom(id),
                CurrentBooking = GetCurrentBookingForRoom(id)
            };

            return View(viewModel);
        }

        // GET: Create Room
        public IActionResult CreateRoom()
        {
            ViewBag.RoomTypes = GetAllRoomTypes();
            return View(new HotelRoom());
        }

        // POST: Create Room
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateRoom(HotelRoom room)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                            INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible, Notes)
                            VALUES (@RoomNumber, @Floor, @RoomTypeId, @HasView, @ViewType, @IsSmoking, @IsAccessible, @Notes)", conn);

                        cmd.Parameters.AddWithValue("@RoomNumber", room.RoomNumber);
                        cmd.Parameters.AddWithValue("@Floor", room.Floor);
                        cmd.Parameters.AddWithValue("@RoomTypeId", room.RoomTypeId);
                        cmd.Parameters.AddWithValue("@HasView", room.HasView);
                        cmd.Parameters.AddWithValue("@ViewType", (object)room.ViewType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsSmoking", room.IsSmoking);
                        cmd.Parameters.AddWithValue("@IsAccessible", room.IsAccessible);
                        cmd.Parameters.AddWithValue("@Notes", (object)room.Notes ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }

                    TempData["SuccessMessage"] = "Room created successfully.";
                    return RedirectToAction("Rooms");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating room: {ex.Message}";
                }
            }

            ViewBag.RoomTypes = GetAllRoomTypes();
            return View(room);
        }

        // GET: Edit Room
        public IActionResult EditRoom(int id)
        {
            var room = GetRoomById(id);
            if (room == null)
            {
                TempData["ErrorMessage"] = "Room not found.";
                return RedirectToAction("Rooms");
            }

            ViewBag.RoomTypes = GetAllRoomTypes();
            return View(room);
        }

        // POST: Edit Room
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditRoom(HotelRoom room)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                            UPDATE HotelRooms 
                            SET RoomNumber = @RoomNumber, Floor = @Floor, RoomTypeId = @RoomTypeId,
                                HasView = @HasView, ViewType = @ViewType, IsSmoking = @IsSmoking, 
                                IsAccessible = @IsAccessible, Notes = @Notes, Status = @Status,
                                UpdatedAt = GETDATE()
                            WHERE Id = @Id", conn);

                        cmd.Parameters.AddWithValue("@Id", room.Id);
                        cmd.Parameters.AddWithValue("@RoomNumber", room.RoomNumber);
                        cmd.Parameters.AddWithValue("@Floor", room.Floor);
                        cmd.Parameters.AddWithValue("@RoomTypeId", room.RoomTypeId);
                        cmd.Parameters.AddWithValue("@HasView", room.HasView);
                        cmd.Parameters.AddWithValue("@ViewType", (object)room.ViewType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IsSmoking", room.IsSmoking);
                        cmd.Parameters.AddWithValue("@IsAccessible", room.IsAccessible);
                        cmd.Parameters.AddWithValue("@Notes", (object)room.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", (int)room.Status);

                        cmd.ExecuteNonQuery();
                    }

                    TempData["SuccessMessage"] = "Room updated successfully.";
                    return RedirectToAction("Rooms");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating room: {ex.Message}";
                }
            }

            ViewBag.RoomTypes = GetAllRoomTypes();
            return View(room);
        }

        // POST: Update Room Status
        [HttpPost]
        public IActionResult UpdateRoomStatus(int id, RoomStatus status)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE HotelRooms SET Status = @Status, UpdatedAt = GETDATE() WHERE Id = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@Status", (int)status);
                    cmd.ExecuteNonQuery();
                }

                return Json(new { success = true, message = "Room status updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Room Types

        // GET: Room Types
        public IActionResult RoomTypes()
        {
            var roomTypes = GetAllRoomTypes();
            return View(roomTypes);
        }

        // GET: Create Room Type
        public IActionResult CreateRoomType()
        {
            var viewModel = new RoomTypeFormViewModel
            {
                AllAmenities = GetAllAmenities()
            };
            return View(viewModel);
        }

        // POST: Create Room Type
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateRoomType(RoomTypeFormViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            var cmd = new SqlCommand(@"
                                INSERT INTO HotelRoomTypes (Name, Description, BasePrice, MaxOccupancy, BedType, RoomSize)
                                OUTPUT INSERTED.Id
                                VALUES (@Name, @Description, @BasePrice, @MaxOccupancy, @BedType, @RoomSize)", conn, transaction);

                            cmd.Parameters.AddWithValue("@Name", viewModel.RoomType.Name);
                            cmd.Parameters.AddWithValue("@Description", (object)viewModel.RoomType.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BasePrice", viewModel.RoomType.BasePrice);
                            cmd.Parameters.AddWithValue("@MaxOccupancy", viewModel.RoomType.MaxOccupancy);
                            cmd.Parameters.AddWithValue("@BedType", (object)viewModel.RoomType.BedType ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RoomSize", (object)viewModel.RoomType.RoomSize ?? DBNull.Value);

                            var roomTypeId = (int)cmd.ExecuteScalar();

                            // Add amenities
                            if (viewModel.SelectedAmenityIds?.Any() == true)
                            {
                                foreach (var amenityId in viewModel.SelectedAmenityIds)
                                {
                                    var amenityCmd = new SqlCommand(
                                        "INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId) VALUES (@RoomTypeId, @AmenityId)",
                                        conn, transaction);
                                    amenityCmd.Parameters.AddWithValue("@RoomTypeId", roomTypeId);
                                    amenityCmd.Parameters.AddWithValue("@AmenityId", amenityId);
                                    amenityCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                        }
                    }

                    TempData["SuccessMessage"] = "Room type created successfully.";
                    return RedirectToAction("RoomTypes");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating room type: {ex.Message}";
                }
            }

            viewModel.AllAmenities = GetAllAmenities();
            return View(viewModel);
        }

        // GET: Edit Room Type
        public IActionResult EditRoomType(int id)
        {
            var roomType = GetRoomTypeById(id);
            if (roomType == null)
            {
                TempData["ErrorMessage"] = "Room type not found.";
                return RedirectToAction("RoomTypes");
            }

            var viewModel = new RoomTypeFormViewModel
            {
                RoomType = roomType,
                AllAmenities = GetAllAmenities(),
                SelectedAmenityIds = GetAmenityIdsByRoomTypeId(id)
            };

            return View(viewModel);
        }

        // POST: Edit Room Type
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditRoomType(RoomTypeFormViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            var cmd = new SqlCommand(@"
                                UPDATE HotelRoomTypes 
                                SET Name = @Name, Description = @Description, BasePrice = @BasePrice,
                                    MaxOccupancy = @MaxOccupancy, BedType = @BedType, RoomSize = @RoomSize,
                                    UpdatedAt = GETDATE()
                                WHERE Id = @Id", conn, transaction);

                            cmd.Parameters.AddWithValue("@Id", viewModel.RoomType.Id);
                            cmd.Parameters.AddWithValue("@Name", viewModel.RoomType.Name);
                            cmd.Parameters.AddWithValue("@Description", (object)viewModel.RoomType.Description ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BasePrice", viewModel.RoomType.BasePrice);
                            cmd.Parameters.AddWithValue("@MaxOccupancy", viewModel.RoomType.MaxOccupancy);
                            cmd.Parameters.AddWithValue("@BedType", (object)viewModel.RoomType.BedType ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@RoomSize", (object)viewModel.RoomType.RoomSize ?? DBNull.Value);

                            cmd.ExecuteNonQuery();

                            // Update amenities
                            var deleteCmd = new SqlCommand("DELETE FROM RoomTypeAmenities WHERE RoomTypeId = @RoomTypeId", conn, transaction);
                            deleteCmd.Parameters.AddWithValue("@RoomTypeId", viewModel.RoomType.Id);
                            deleteCmd.ExecuteNonQuery();

                            if (viewModel.SelectedAmenityIds?.Any() == true)
                            {
                                foreach (var amenityId in viewModel.SelectedAmenityIds)
                                {
                                    var amenityCmd = new SqlCommand(
                                        "INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId) VALUES (@RoomTypeId, @AmenityId)",
                                        conn, transaction);
                                    amenityCmd.Parameters.AddWithValue("@RoomTypeId", viewModel.RoomType.Id);
                                    amenityCmd.Parameters.AddWithValue("@AmenityId", amenityId);
                                    amenityCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                        }
                    }

                    TempData["SuccessMessage"] = "Room type updated successfully.";
                    return RedirectToAction("RoomTypes");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating room type: {ex.Message}";
                }
            }

            viewModel.AllAmenities = GetAllAmenities();
            return View(viewModel);
        }

        #endregion

        #region Bookings

        // GET: Booking List
        public IActionResult Bookings(DateTime? fromDate = null, DateTime? toDate = null, BookingStatus? status = null, string search = null)
        {
            var viewModel = new BookingListViewModel
            {
                FromDate = fromDate ?? DateTime.Today.AddDays(-7),
                ToDate = toDate ?? DateTime.Today.AddDays(30),
                Status = status,
                SearchTerm = search
            };

            var bookings = GetBookingsByDateRange(viewModel.FromDate.Value, viewModel.ToDate.Value);

            if (status.HasValue)
            {
                bookings = bookings.Where(b => b.Status == status.Value).ToList();
            }

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                bookings = bookings.Where(b =>
                    b.BookingNumber.ToLower().Contains(search) ||
                    b.Guest?.FullName?.ToLower().Contains(search) == true ||
                    b.Guest?.Email?.ToLower().Contains(search) == true ||
                    b.Room?.RoomNumber?.ToLower().Contains(search) == true
                ).ToList();
            }

            viewModel.Bookings = bookings.OrderByDescending(b => b.CheckInDate).ToList();
            viewModel.TotalCount = viewModel.Bookings.Count;

            return View(viewModel);
        }

        // GET: Search Available Rooms
        public IActionResult SearchRooms(DateTime? checkIn = null, DateTime? checkOut = null, int adults = 2, int children = 0, int? roomTypeId = null)
        {
            var viewModel = new RoomSearchViewModel
            {
                CheckInDate = checkIn ?? DateTime.Today,
                CheckOutDate = checkOut ?? DateTime.Today.AddDays(1),
                Adults = adults,
                Children = children,
                RoomTypeId = roomTypeId,
                RoomTypes = GetAllRoomTypes()
            };

            if (checkIn.HasValue && checkOut.HasValue && checkOut > checkIn)
            {
                viewModel.AvailableRooms = SearchAvailableRooms(viewModel.CheckInDate, viewModel.CheckOutDate, 
                    viewModel.Adults, viewModel.Children, viewModel.RoomTypeId);
            }

            return View(viewModel);
        }

        // GET: Create Booking
        public IActionResult CreateBooking(int? roomId = null, DateTime? checkIn = null, DateTime? checkOut = null)
        {
            var viewModel = new BookingFormViewModel
            {
                Booking = new HotelBooking
                {
                    CheckInDate = checkIn ?? DateTime.Today,
                    CheckOutDate = checkOut ?? DateTime.Today.AddDays(1),
                    RoomId = roomId ?? 0
                },
                AvailableRooms = roomId.HasValue 
                    ? new List<HotelRoom> { GetRoomById(roomId.Value) } 
                    : GetAllRooms().Where(r => r.Status == RoomStatus.Available).ToList(),
                RoomTypes = GetAllRoomTypes(),
                ExistingGuests = GetAllGuests()
            };

            return View(viewModel);
        }

        // POST: Create Booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateBooking(BookingFormViewModel viewModel)
        {
            try
            {
                int guestId;

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        // Create or use existing guest
                        if (viewModel.IsNewGuest)
                        {
                            var guestCmd = new SqlCommand(@"
                                INSERT INTO HotelGuests (FirstName, LastName, Email, Phone, Address, City, Country, IdType, IdNumber)
                                OUTPUT INSERTED.Id
                                VALUES (@FirstName, @LastName, @Email, @Phone, @Address, @City, @Country, @IdType, @IdNumber)", 
                                conn, transaction);

                            guestCmd.Parameters.AddWithValue("@FirstName", viewModel.Guest.FirstName);
                            guestCmd.Parameters.AddWithValue("@LastName", viewModel.Guest.LastName);
                            guestCmd.Parameters.AddWithValue("@Email", viewModel.Guest.Email);
                            guestCmd.Parameters.AddWithValue("@Phone", viewModel.Guest.Phone);
                            guestCmd.Parameters.AddWithValue("@Address", (object)viewModel.Guest.Address ?? DBNull.Value);
                            guestCmd.Parameters.AddWithValue("@City", (object)viewModel.Guest.City ?? DBNull.Value);
                            guestCmd.Parameters.AddWithValue("@Country", viewModel.Guest.Country ?? "India");
                            guestCmd.Parameters.AddWithValue("@IdType", (object)viewModel.Guest.IdType ?? DBNull.Value);
                            guestCmd.Parameters.AddWithValue("@IdNumber", (object)viewModel.Guest.IdNumber ?? DBNull.Value);

                            guestId = (int)guestCmd.ExecuteScalar();
                        }
                        else
                        {
                            guestId = viewModel.SelectedGuestId.Value;
                        }

                        // Calculate room rate and totals
                        var roomType = GetRoomTypeByRoomId(viewModel.Booking.RoomId);
                        var nights = (viewModel.Booking.CheckOutDate - viewModel.Booking.CheckInDate).Days;
                        var roomRate = roomType?.BasePrice ?? 0;
                        var subTotal = roomRate * nights;
                        var taxAmount = subTotal * 0.18m; // 18% GST
                        var totalAmount = subTotal + taxAmount;

                        // Generate booking number
                        var bookingNumber = GenerateBookingNumber(conn, transaction);

                        // Create booking
                        var bookingCmd = new SqlCommand(@"
                            INSERT INTO HotelBookings (BookingNumber, RoomId, GuestId, CheckInDate, CheckOutDate,
                                NumberOfGuests, NumberOfAdults, NumberOfChildren, RoomRate, TaxAmount, TotalAmount,
                                Status, PaymentStatus, SpecialRequests, BookingSource)
                            OUTPUT INSERTED.Id
                            VALUES (@BookingNumber, @RoomId, @GuestId, @CheckInDate, @CheckOutDate,
                                @NumberOfGuests, @NumberOfAdults, @NumberOfChildren, @RoomRate, @TaxAmount, @TotalAmount,
                                @Status, @PaymentStatus, @SpecialRequests, @BookingSource)", 
                            conn, transaction);

                        bookingCmd.Parameters.AddWithValue("@BookingNumber", bookingNumber);
                        bookingCmd.Parameters.AddWithValue("@RoomId", viewModel.Booking.RoomId);
                        bookingCmd.Parameters.AddWithValue("@GuestId", guestId);
                        bookingCmd.Parameters.AddWithValue("@CheckInDate", viewModel.Booking.CheckInDate);
                        bookingCmd.Parameters.AddWithValue("@CheckOutDate", viewModel.Booking.CheckOutDate);
                        bookingCmd.Parameters.AddWithValue("@NumberOfGuests", viewModel.Booking.NumberOfAdults + viewModel.Booking.NumberOfChildren);
                        bookingCmd.Parameters.AddWithValue("@NumberOfAdults", viewModel.Booking.NumberOfAdults);
                        bookingCmd.Parameters.AddWithValue("@NumberOfChildren", viewModel.Booking.NumberOfChildren);
                        bookingCmd.Parameters.AddWithValue("@RoomRate", roomRate);
                        bookingCmd.Parameters.AddWithValue("@TaxAmount", taxAmount);
                        bookingCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        bookingCmd.Parameters.AddWithValue("@Status", (int)BookingStatus.Confirmed);
                        bookingCmd.Parameters.AddWithValue("@PaymentStatus", (int)PaymentStatus.Pending);
                        bookingCmd.Parameters.AddWithValue("@SpecialRequests", (object)viewModel.Booking.SpecialRequests ?? DBNull.Value);
                        bookingCmd.Parameters.AddWithValue("@BookingSource", viewModel.Booking.BookingSource ?? "Direct");

                        var bookingId = (int)bookingCmd.ExecuteScalar();

                        // Update room status to Reserved
                        var updateRoomCmd = new SqlCommand("UPDATE HotelRooms SET Status = 2 WHERE Id = @RoomId", conn, transaction);
                        updateRoomCmd.Parameters.AddWithValue("@RoomId", viewModel.Booking.RoomId);
                        updateRoomCmd.ExecuteNonQuery();

                        transaction.Commit();

                        TempData["SuccessMessage"] = $"Booking {bookingNumber} created successfully.";
                        return RedirectToAction("BookingDetails", new { id = bookingId });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error creating booking: {ex.Message}";
            }

            viewModel.AvailableRooms = GetAllRooms().Where(r => r.Status == RoomStatus.Available).ToList();
            viewModel.RoomTypes = GetAllRoomTypes();
            viewModel.ExistingGuests = GetAllGuests();
            return View(viewModel);
        }

        // GET: Booking Details
        public IActionResult BookingDetails(int id)
        {
            var booking = GetBookingById(id);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Bookings");
            }

            return View(booking);
        }

        // GET: Check In
        public IActionResult CheckIn(int id)
        {
            var booking = GetBookingById(id);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Bookings");
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                TempData["ErrorMessage"] = "Only confirmed bookings can be checked in.";
                return RedirectToAction("BookingDetails", new { id });
            }

            var viewModel = new CheckInViewModel
            {
                Booking = booking,
                Guest = booking.Guest,
                Room = booking.Room
            };

            return View(viewModel);
        }

        // POST: Check In
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckIn(int id, CheckInViewModel viewModel)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        // Update booking
                        var bookingCmd = new SqlCommand(@"
                            UPDATE HotelBookings 
                            SET Status = 2, ActualCheckIn = GETDATE(), 
                                AdvancePayment = AdvancePayment + @DepositAmount,
                                Notes = @Notes, UpdatedAt = GETDATE()
                            WHERE Id = @Id", conn, transaction);

                        bookingCmd.Parameters.AddWithValue("@Id", id);
                        bookingCmd.Parameters.AddWithValue("@DepositAmount", viewModel.DepositAmount);
                        bookingCmd.Parameters.AddWithValue("@Notes", (object)viewModel.Notes ?? DBNull.Value);
                        bookingCmd.ExecuteNonQuery();

                        // Update payment status if deposit received
                        if (viewModel.DepositAmount > 0)
                        {
                            var paymentCmd = new SqlCommand(@"
                                UPDATE HotelBookings SET PaymentStatus = 1 WHERE Id = @Id", conn, transaction);
                            paymentCmd.Parameters.AddWithValue("@Id", id);
                            paymentCmd.ExecuteNonQuery();
                        }

                        // Get RoomId within the transaction
                        var getRoomCmd = new SqlCommand("SELECT RoomId FROM HotelBookings WHERE Id = @Id", conn, transaction);
                        getRoomCmd.Parameters.AddWithValue("@Id", id);
                        var roomId = (int)getRoomCmd.ExecuteScalar();

                        // Update room status to Occupied
                        var roomCmd = new SqlCommand("UPDATE HotelRooms SET Status = 1 WHERE Id = @RoomId", conn, transaction);
                        roomCmd.Parameters.AddWithValue("@RoomId", roomId);
                        roomCmd.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }

                TempData["SuccessMessage"] = "Guest checked in successfully.";
                return RedirectToAction("BookingDetails", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error during check-in: {ex.Message}";
                return RedirectToAction("CheckIn", new { id });
            }
        }

        // GET: Check Out
        public IActionResult CheckOut(int id)
        {
            var booking = GetBookingById(id);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Bookings");
            }

            if (booking.Status != BookingStatus.CheckedIn)
            {
                TempData["ErrorMessage"] = "Only checked-in bookings can be checked out.";
                return RedirectToAction("BookingDetails", new { id });
            }

            var viewModel = new CheckOutViewModel
            {
                Booking = booking,
                Guest = booking.Guest,
                Room = booking.Room,
                RoomCharges = booking.TotalAmount,
                AmountPaid = booking.AdvancePayment,
                BalanceDue = booking.TotalAmount - booking.AdvancePayment
            };

            return View(viewModel);
        }

        // POST: Check Out
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckOut(int id, CheckOutViewModel viewModel)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        var extraCharges = viewModel.MinibarCharges + viewModel.LaundryCharges + 
                                          viewModel.RestaurantCharges + viewModel.OtherCharges;

                        // Update booking
                        var bookingCmd = new SqlCommand(@"
                            UPDATE HotelBookings 
                            SET Status = 3, ActualCheckOut = GETDATE(),
                                ExtraCharges = @ExtraCharges,
                                TotalAmount = TotalAmount + @ExtraCharges,
                                PaymentStatus = @PaymentStatus,
                                Notes = @Notes, UpdatedAt = GETDATE()
                            WHERE Id = @Id", conn, transaction);

                        bookingCmd.Parameters.AddWithValue("@Id", id);
                        bookingCmd.Parameters.AddWithValue("@ExtraCharges", extraCharges);
                        bookingCmd.Parameters.AddWithValue("@PaymentStatus", (int)PaymentStatus.Paid);
                        bookingCmd.Parameters.AddWithValue("@Notes", (object)viewModel.Notes ?? DBNull.Value);
                        bookingCmd.ExecuteNonQuery();

                        // Get RoomId within the transaction
                        var getRoomCmd = new SqlCommand("SELECT RoomId FROM HotelBookings WHERE Id = @Id", conn, transaction);
                        getRoomCmd.Parameters.AddWithValue("@Id", id);
                        var roomId = (int)getRoomCmd.ExecuteScalar();

                        // Update room status to Cleaning
                        var roomCmd = new SqlCommand("UPDATE HotelRooms SET Status = 4 WHERE Id = @RoomId", conn, transaction);
                        roomCmd.Parameters.AddWithValue("@RoomId", roomId);
                        roomCmd.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }

                TempData["SuccessMessage"] = "Guest checked out successfully.";
                return RedirectToAction("BookingDetails", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error during check-out: {ex.Message}";
                return RedirectToAction("CheckOut", new { id });
            }
        }

        // POST: Cancel Booking
        [HttpPost]
        public IActionResult CancelBooking(int id)
        {
            try
            {
                var booking = GetBookingById(id);
                if (booking == null)
                {
                    return Json(new { success = false, message = "Booking not found." });
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        var bookingCmd = new SqlCommand(@"
                            UPDATE HotelBookings SET Status = 4, UpdatedAt = GETDATE() WHERE Id = @Id", conn, transaction);
                        bookingCmd.Parameters.AddWithValue("@Id", id);
                        bookingCmd.ExecuteNonQuery();

                        // Update room status to Available if it was reserved
                        if (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Pending)
                        {
                            var roomCmd = new SqlCommand("UPDATE HotelRooms SET Status = 0 WHERE Id = @RoomId", conn, transaction);
                            roomCmd.Parameters.AddWithValue("@RoomId", booking.RoomId);
                            roomCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                return Json(new { success = true, message = "Booking cancelled successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Guests

        // GET: Guest List
        public IActionResult Guests(string search = null)
        {
            var viewModel = new GuestListViewModel
            {
                SearchTerm = search,
                Guests = GetAllGuests()
            };

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                viewModel.Guests = viewModel.Guests.Where(g =>
                    g.FullName.ToLower().Contains(search) ||
                    g.Email.ToLower().Contains(search) ||
                    g.Phone.Contains(search)
                ).ToList();
            }

            viewModel.TotalCount = viewModel.Guests.Count;
            return View(viewModel);
        }

        // GET: Guest Details
        public IActionResult GuestDetails(int id)
        {
            var guest = GetGuestById(id);
            if (guest == null)
            {
                TempData["ErrorMessage"] = "Guest not found.";
                return RedirectToAction("Guests");
            }

            ViewBag.BookingHistory = GetBookingsByGuestId(id);
            return View(guest);
        }

        // GET: Create Guest
        public IActionResult CreateGuest()
        {
            return View(new HotelGuest());
        }

        // POST: Create Guest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateGuest(HotelGuest guest)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                            INSERT INTO HotelGuests (FirstName, LastName, Email, Phone, Address, City, State, Country, 
                                PostalCode, IdType, IdNumber, DateOfBirth, Nationality, IsVip, Notes)
                            VALUES (@FirstName, @LastName, @Email, @Phone, @Address, @City, @State, @Country,
                                @PostalCode, @IdType, @IdNumber, @DateOfBirth, @Nationality, @IsVip, @Notes)", conn);

                        cmd.Parameters.AddWithValue("@FirstName", guest.FirstName);
                        cmd.Parameters.AddWithValue("@LastName", guest.LastName);
                        cmd.Parameters.AddWithValue("@Email", guest.Email);
                        cmd.Parameters.AddWithValue("@Phone", guest.Phone);
                        cmd.Parameters.AddWithValue("@Address", (object)guest.Address ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@City", (object)guest.City ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@State", (object)guest.State ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Country", guest.Country ?? "India");
                        cmd.Parameters.AddWithValue("@PostalCode", (object)guest.PostalCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdType", (object)guest.IdType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdNumber", (object)guest.IdNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateOfBirth", (object)guest.DateOfBirth ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nationality", guest.Nationality ?? "Indian");
                        cmd.Parameters.AddWithValue("@IsVip", guest.IsVip);
                        cmd.Parameters.AddWithValue("@Notes", (object)guest.Notes ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }

                    TempData["SuccessMessage"] = "Guest created successfully.";
                    return RedirectToAction("Guests");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error creating guest: {ex.Message}";
                }
            }

            return View(guest);
        }

        // GET: Edit Guest
        public IActionResult EditGuest(int id)
        {
            var guest = GetGuestById(id);
            if (guest == null)
            {
                TempData["ErrorMessage"] = "Guest not found.";
                return RedirectToAction("Guests");
            }

            return View(guest);
        }

        // POST: Edit Guest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditGuest(HotelGuest guest)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqlCommand(@"
                            UPDATE HotelGuests 
                            SET FirstName = @FirstName, LastName = @LastName, Email = @Email, Phone = @Phone,
                                Address = @Address, City = @City, State = @State, Country = @Country,
                                PostalCode = @PostalCode, IdType = @IdType, IdNumber = @IdNumber,
                                DateOfBirth = @DateOfBirth, Nationality = @Nationality, IsVip = @IsVip, 
                                Notes = @Notes, UpdatedAt = GETDATE()
                            WHERE Id = @Id", conn);

                        cmd.Parameters.AddWithValue("@Id", guest.Id);
                        cmd.Parameters.AddWithValue("@FirstName", guest.FirstName);
                        cmd.Parameters.AddWithValue("@LastName", guest.LastName);
                        cmd.Parameters.AddWithValue("@Email", guest.Email);
                        cmd.Parameters.AddWithValue("@Phone", guest.Phone);
                        cmd.Parameters.AddWithValue("@Address", (object)guest.Address ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@City", (object)guest.City ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@State", (object)guest.State ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Country", guest.Country ?? "India");
                        cmd.Parameters.AddWithValue("@PostalCode", (object)guest.PostalCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdType", (object)guest.IdType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IdNumber", (object)guest.IdNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@DateOfBirth", (object)guest.DateOfBirth ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nationality", guest.Nationality ?? "Indian");
                        cmd.Parameters.AddWithValue("@IsVip", guest.IsVip);
                        cmd.Parameters.AddWithValue("@Notes", (object)guest.Notes ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }

                    TempData["SuccessMessage"] = "Guest updated successfully.";
                    return RedirectToAction("Guests");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error updating guest: {ex.Message}";
                }
            }

            return View(guest);
        }

        #endregion

        #region Helper Methods

        private string GenerateBookingNumber(SqlConnection conn, SqlTransaction transaction)
        {
            var prefix = "HB";
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            
            var cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(CAST(RIGHT(BookingNumber, 4) AS INT)), 0) + 1
                FROM HotelBookings
                WHERE BookingNumber LIKE @Prefix + @DatePart + '%'", conn, transaction);
            
            cmd.Parameters.AddWithValue("@Prefix", prefix);
            cmd.Parameters.AddWithValue("@DatePart", datePart);
            
            var seqNumber = (int)cmd.ExecuteScalar();
            return $"{prefix}{datePart}{seqNumber:D4}";
        }

        private List<HotelRoom> GetAllRooms()
        {
            var rooms = new List<HotelRoom>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT r.*, rt.Name as RoomTypeName, rt.BasePrice, rt.MaxOccupancy, rt.BedType
                    FROM HotelRooms r
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE r.IsActive = 1
                    ORDER BY r.Floor, r.RoomNumber", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rooms.Add(MapRoom(reader));
                    }
                }
            }

            return rooms;
        }

        private HotelRoom GetRoomById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT r.*, rt.Name as RoomTypeName, rt.BasePrice, rt.MaxOccupancy, rt.BedType
                    FROM HotelRooms r
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE r.Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapRoom(reader);
                    }
                }
            }

            return null;
        }

        private HotelRoom MapRoom(SqlDataReader reader)
        {
            return new HotelRoom
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                RoomNumber = reader.GetString(reader.GetOrdinal("RoomNumber")),
                Floor = reader.GetInt32(reader.GetOrdinal("Floor")),
                RoomTypeId = reader.GetInt32(reader.GetOrdinal("RoomTypeId")),
                Status = (RoomStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                HasView = reader.GetBoolean(reader.GetOrdinal("HasView")),
                ViewType = reader.IsDBNull(reader.GetOrdinal("ViewType")) ? null : reader.GetString(reader.GetOrdinal("ViewType")),
                IsSmoking = reader.GetBoolean(reader.GetOrdinal("IsSmoking")),
                IsAccessible = reader.GetBoolean(reader.GetOrdinal("IsAccessible")),
                RoomType = new RoomType
                {
                    Name = reader.GetString(reader.GetOrdinal("RoomTypeName")),
                    BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                    MaxOccupancy = reader.GetInt32(reader.GetOrdinal("MaxOccupancy")),
                    BedType = reader.IsDBNull(reader.GetOrdinal("BedType")) ? null : reader.GetString(reader.GetOrdinal("BedType"))
                }
            };
        }

        private List<RoomType> GetAllRoomTypes()
        {
            var roomTypes = new List<RoomType>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM HotelRoomTypes WHERE IsActive = 1 ORDER BY BasePrice", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        roomTypes.Add(new RoomType
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                            MaxOccupancy = reader.GetInt32(reader.GetOrdinal("MaxOccupancy")),
                            BedType = reader.IsDBNull(reader.GetOrdinal("BedType")) ? null : reader.GetString(reader.GetOrdinal("BedType")),
                            RoomSize = reader.IsDBNull(reader.GetOrdinal("RoomSize")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("RoomSize"))
                        });
                    }
                }
            }

            return roomTypes;
        }

        private RoomType GetRoomTypeById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM HotelRoomTypes WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new RoomType
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                            MaxOccupancy = reader.GetInt32(reader.GetOrdinal("MaxOccupancy")),
                            BedType = reader.IsDBNull(reader.GetOrdinal("BedType")) ? null : reader.GetString(reader.GetOrdinal("BedType")),
                            RoomSize = reader.IsDBNull(reader.GetOrdinal("RoomSize")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("RoomSize"))
                        };
                    }
                }
            }

            return null;
        }

        private RoomType GetRoomTypeByRoomId(int roomId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT rt.* FROM HotelRoomTypes rt
                    INNER JOIN HotelRooms r ON r.RoomTypeId = rt.Id
                    WHERE r.Id = @RoomId", conn);
                cmd.Parameters.AddWithValue("@RoomId", roomId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new RoomType
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                            MaxOccupancy = reader.GetInt32(reader.GetOrdinal("MaxOccupancy"))
                        };
                    }
                }
            }

            return null;
        }

        private List<HotelAmenity> GetAllAmenities()
        {
            var amenities = new List<HotelAmenity>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM HotelAmenities WHERE IsActive = 1 ORDER BY Category, Name", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        amenities.Add(new HotelAmenity
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Icon = reader.IsDBNull(reader.GetOrdinal("Icon")) ? "fa-check" : reader.GetString(reader.GetOrdinal("Icon")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "General" : reader.GetString(reader.GetOrdinal("Category"))
                        });
                    }
                }
            }

            return amenities;
        }

        private List<HotelAmenity> GetAmenitiesByRoomTypeId(int roomTypeId)
        {
            var amenities = new List<HotelAmenity>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT a.* FROM HotelAmenities a
                    INNER JOIN RoomTypeAmenities rta ON a.Id = rta.AmenityId
                    WHERE rta.RoomTypeId = @RoomTypeId AND a.IsActive = 1
                    ORDER BY a.Category, a.Name", conn);
                cmd.Parameters.AddWithValue("@RoomTypeId", roomTypeId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        amenities.Add(new HotelAmenity
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            Icon = reader.IsDBNull(reader.GetOrdinal("Icon")) ? "fa-check" : reader.GetString(reader.GetOrdinal("Icon")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? "General" : reader.GetString(reader.GetOrdinal("Category"))
                        });
                    }
                }
            }

            return amenities;
        }

        private List<int> GetAmenityIdsByRoomTypeId(int roomTypeId)
        {
            var ids = new List<int>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT AmenityId FROM RoomTypeAmenities WHERE RoomTypeId = @RoomTypeId", conn);
                cmd.Parameters.AddWithValue("@RoomTypeId", roomTypeId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ids.Add(reader.GetInt32(0));
                    }
                }
            }

            return ids;
        }

        private List<RoomAvailabilityResult> SearchAvailableRooms(DateTime checkIn, DateTime checkOut, int adults, int children, int? roomTypeId)
        {
            var results = new List<RoomAvailabilityResult>();
            var totalGuests = adults + children;
            var nights = (checkOut - checkIn).Days;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT r.*, rt.Name as RoomTypeName, rt.Description as RoomTypeDescription, 
                           rt.BasePrice, rt.MaxOccupancy, rt.BedType, rt.RoomSize
                    FROM HotelRooms r
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE r.IsActive = 1 AND r.Status = 0 AND rt.IsActive = 1
                        AND rt.MaxOccupancy >= @TotalGuests
                        AND (@RoomTypeId IS NULL OR r.RoomTypeId = @RoomTypeId)
                        AND r.Id NOT IN (
                            SELECT RoomId FROM HotelBookings 
                            WHERE Status IN (1, 2)
                                AND NOT (CheckOutDate <= @CheckIn OR CheckInDate >= @CheckOut)
                        )
                    ORDER BY rt.BasePrice, r.Floor, r.RoomNumber", conn);

                cmd.Parameters.AddWithValue("@TotalGuests", totalGuests);
                cmd.Parameters.AddWithValue("@RoomTypeId", (object)roomTypeId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CheckIn", checkIn);
                cmd.Parameters.AddWithValue("@CheckOut", checkOut);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var roomType = new RoomType
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("RoomTypeId")),
                            Name = reader.GetString(reader.GetOrdinal("RoomTypeName")),
                            Description = reader.IsDBNull(reader.GetOrdinal("RoomTypeDescription")) ? null : reader.GetString(reader.GetOrdinal("RoomTypeDescription")),
                            BasePrice = reader.GetDecimal(reader.GetOrdinal("BasePrice")),
                            MaxOccupancy = reader.GetInt32(reader.GetOrdinal("MaxOccupancy")),
                            BedType = reader.IsDBNull(reader.GetOrdinal("BedType")) ? null : reader.GetString(reader.GetOrdinal("BedType")),
                            RoomSize = reader.IsDBNull(reader.GetOrdinal("RoomSize")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("RoomSize"))
                        };

                        results.Add(new RoomAvailabilityResult
                        {
                            Room = MapRoom(reader),
                            RoomType = roomType,
                            PricePerNight = roomType.BasePrice,
                            TotalPrice = roomType.BasePrice * nights
                        });
                    }
                }
            }

            return results;
        }

        private List<HotelBooking> GetBookingsByDateRange(DateTime fromDate, DateTime toDate)
        {
            var bookings = new List<HotelBooking>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT b.*, 
                           g.FirstName, g.LastName, g.Email as GuestEmail, g.Phone as GuestPhone,
                           r.RoomNumber, r.Floor,
                           rt.Name as RoomTypeName, rt.BasePrice as RoomTypePrice
                    FROM HotelBookings b
                    INNER JOIN HotelGuests g ON b.GuestId = g.Id
                    INNER JOIN HotelRooms r ON b.RoomId = r.Id
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE (b.CheckInDate BETWEEN @FromDate AND @ToDate)
                       OR (b.CheckOutDate BETWEEN @FromDate AND @ToDate)
                       OR (b.CheckInDate <= @FromDate AND b.CheckOutDate >= @ToDate)
                    ORDER BY b.CheckInDate DESC", conn);

                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                cmd.Parameters.AddWithValue("@ToDate", toDate);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bookings.Add(MapBooking(reader));
                    }
                }
            }

            return bookings;
        }

        private HotelBooking GetBookingById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT b.*, 
                           g.FirstName, g.LastName, g.Email as GuestEmail, g.Phone as GuestPhone,
                           g.Address as GuestAddress, g.City as GuestCity, g.IdType, g.IdNumber,
                           r.RoomNumber, r.Floor, r.RoomTypeId,
                           rt.Name as RoomTypeName, rt.BasePrice as RoomTypePrice
                    FROM HotelBookings b
                    INNER JOIN HotelGuests g ON b.GuestId = g.Id
                    INNER JOIN HotelRooms r ON b.RoomId = r.Id
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE b.Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapBooking(reader);
                    }
                }
            }

            return null;
        }

        private HotelBooking MapBooking(SqlDataReader reader)
        {
            var booking = new HotelBooking
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                BookingNumber = reader.GetString(reader.GetOrdinal("BookingNumber")),
                RoomId = reader.GetInt32(reader.GetOrdinal("RoomId")),
                GuestId = reader.GetInt32(reader.GetOrdinal("GuestId")),
                CheckInDate = reader.GetDateTime(reader.GetOrdinal("CheckInDate")),
                CheckOutDate = reader.GetDateTime(reader.GetOrdinal("CheckOutDate")),
                ActualCheckIn = reader.IsDBNull(reader.GetOrdinal("ActualCheckIn")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ActualCheckIn")),
                ActualCheckOut = reader.IsDBNull(reader.GetOrdinal("ActualCheckOut")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ActualCheckOut")),
                NumberOfGuests = reader.GetInt32(reader.GetOrdinal("NumberOfGuests")),
                NumberOfAdults = reader.GetInt32(reader.GetOrdinal("NumberOfAdults")),
                NumberOfChildren = reader.GetInt32(reader.GetOrdinal("NumberOfChildren")),
                RoomRate = reader.GetDecimal(reader.GetOrdinal("RoomRate")),
                TaxAmount = reader.GetDecimal(reader.GetOrdinal("TaxAmount")),
                ExtraCharges = reader.GetDecimal(reader.GetOrdinal("ExtraCharges")),
                Discount = reader.GetDecimal(reader.GetOrdinal("Discount")),
                TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                AdvancePayment = reader.GetDecimal(reader.GetOrdinal("AdvancePayment")),
                Status = (BookingStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                PaymentStatus = (PaymentStatus)reader.GetInt32(reader.GetOrdinal("PaymentStatus")),
                SpecialRequests = reader.IsDBNull(reader.GetOrdinal("SpecialRequests")) ? null : reader.GetString(reader.GetOrdinal("SpecialRequests")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                BookingSource = reader.IsDBNull(reader.GetOrdinal("BookingSource")) ? "Direct" : reader.GetString(reader.GetOrdinal("BookingSource")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Guest = new HotelGuest
                {
                    Id = reader.GetInt32(reader.GetOrdinal("GuestId")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    Email = reader.GetString(reader.GetOrdinal("GuestEmail")),
                    Phone = reader.GetString(reader.GetOrdinal("GuestPhone"))
                },
                Room = new HotelRoom
                {
                    Id = reader.GetInt32(reader.GetOrdinal("RoomId")),
                    RoomNumber = reader.GetString(reader.GetOrdinal("RoomNumber")),
                    Floor = reader.GetInt32(reader.GetOrdinal("Floor")),
                    RoomType = new RoomType
                    {
                        Name = reader.GetString(reader.GetOrdinal("RoomTypeName")),
                        BasePrice = reader.GetDecimal(reader.GetOrdinal("RoomTypePrice"))
                    }
                }
            };

            return booking;
        }

        private List<HotelBooking> GetUpcomingBookingsForRoom(int roomId)
        {
            var bookings = new List<HotelBooking>();
            var today = DateTime.Today;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT b.*, 
                           g.FirstName, g.LastName, g.Email as GuestEmail, g.Phone as GuestPhone,
                           r.RoomNumber, r.Floor,
                           rt.Name as RoomTypeName, rt.BasePrice as RoomTypePrice
                    FROM HotelBookings b
                    INNER JOIN HotelGuests g ON b.GuestId = g.Id
                    INNER JOIN HotelRooms r ON b.RoomId = r.Id
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE b.RoomId = @RoomId AND b.CheckInDate >= @Today AND b.Status IN (0, 1)
                    ORDER BY b.CheckInDate", conn);

                cmd.Parameters.AddWithValue("@RoomId", roomId);
                cmd.Parameters.AddWithValue("@Today", today);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bookings.Add(MapBooking(reader));
                    }
                }
            }

            return bookings;
        }

        private HotelBooking GetCurrentBookingForRoom(int roomId)
        {
            var today = DateTime.Today;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT b.*, 
                           g.FirstName, g.LastName, g.Email as GuestEmail, g.Phone as GuestPhone,
                           r.RoomNumber, r.Floor,
                           rt.Name as RoomTypeName, rt.BasePrice as RoomTypePrice
                    FROM HotelBookings b
                    INNER JOIN HotelGuests g ON b.GuestId = g.Id
                    INNER JOIN HotelRooms r ON b.RoomId = r.Id
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE b.RoomId = @RoomId AND b.Status = 2
                    ORDER BY b.CheckInDate DESC", conn);

                cmd.Parameters.AddWithValue("@RoomId", roomId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapBooking(reader);
                    }
                }
            }

            return null;
        }

        private List<HotelGuest> GetAllGuests()
        {
            var guests = new List<HotelGuest>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM HotelGuests ORDER BY FirstName, LastName", conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        guests.Add(MapGuest(reader));
                    }
                }
            }

            return guests;
        }

        private HotelGuest GetGuestById(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM HotelGuests WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapGuest(reader);
                    }
                }
            }

            return null;
        }

        private HotelGuest MapGuest(SqlDataReader reader)
        {
            return new HotelGuest
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
                Phone = reader.GetString(reader.GetOrdinal("Phone")),
                Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? null : reader.GetString(reader.GetOrdinal("Address")),
                City = reader.IsDBNull(reader.GetOrdinal("City")) ? null : reader.GetString(reader.GetOrdinal("City")),
                State = reader.IsDBNull(reader.GetOrdinal("State")) ? null : reader.GetString(reader.GetOrdinal("State")),
                Country = reader.IsDBNull(reader.GetOrdinal("Country")) ? "India" : reader.GetString(reader.GetOrdinal("Country")),
                PostalCode = reader.IsDBNull(reader.GetOrdinal("PostalCode")) ? null : reader.GetString(reader.GetOrdinal("PostalCode")),
                IdType = reader.IsDBNull(reader.GetOrdinal("IdType")) ? null : reader.GetString(reader.GetOrdinal("IdType")),
                IdNumber = reader.IsDBNull(reader.GetOrdinal("IdNumber")) ? null : reader.GetString(reader.GetOrdinal("IdNumber")),
                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("DateOfBirth")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateOfBirth")),
                Nationality = reader.IsDBNull(reader.GetOrdinal("Nationality")) ? "Indian" : reader.GetString(reader.GetOrdinal("Nationality")),
                IsVip = reader.GetBoolean(reader.GetOrdinal("IsVip")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
            };
        }

        private List<HotelBooking> GetBookingsByGuestId(int guestId)
        {
            var bookings = new List<HotelBooking>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT b.*, 
                           g.FirstName, g.LastName, g.Email as GuestEmail, g.Phone as GuestPhone,
                           r.RoomNumber, r.Floor,
                           rt.Name as RoomTypeName, rt.BasePrice as RoomTypePrice
                    FROM HotelBookings b
                    INNER JOIN HotelGuests g ON b.GuestId = g.Id
                    INNER JOIN HotelRooms r ON b.RoomId = r.Id
                    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
                    WHERE b.GuestId = @GuestId
                    ORDER BY b.CheckInDate DESC", conn);

                cmd.Parameters.AddWithValue("@GuestId", guestId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bookings.Add(MapBooking(reader));
                    }
                }
            }

            return bookings;
        }

        #endregion
    }
}
