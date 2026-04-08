-- Hotel Booking Module - Database Tables and Stored Procedures
-- Run this script to set up the hotel booking functionality

-- =============================================
-- Table: HotelRoomTypes
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HotelRoomTypes')
BEGIN
    CREATE TABLE HotelRoomTypes (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500),
        BasePrice DECIMAL(10,2) NOT NULL DEFAULT 0,
        MaxOccupancy INT NOT NULL DEFAULT 2,
        BedType NVARCHAR(50) DEFAULT 'Queen',
        RoomSize INT,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Created table: HotelRoomTypes';
END
GO

-- =============================================
-- Table: HotelAmenities
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HotelAmenities')
BEGIN
    CREATE TABLE HotelAmenities (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(200),
        Icon NVARCHAR(50) DEFAULT 'fa-check',
        Category NVARCHAR(50) DEFAULT 'General',
        IsActive BIT NOT NULL DEFAULT 1
    );
    PRINT 'Created table: HotelAmenities';
END
GO

-- =============================================
-- Table: HotelRooms
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HotelRooms')
BEGIN
    CREATE TABLE HotelRooms (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RoomNumber NVARCHAR(20) NOT NULL,
        Floor INT NOT NULL DEFAULT 1,
        RoomTypeId INT NOT NULL,
        Status INT NOT NULL DEFAULT 0, -- 0=Available, 1=Occupied, 2=Reserved, 3=Maintenance, 4=Cleaning, 5=OutOfOrder
        Notes NVARCHAR(500),
        HasView BIT DEFAULT 0,
        ViewType NVARCHAR(50),
        IsSmoking BIT DEFAULT 0,
        IsAccessible BIT DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_HotelRooms_RoomType FOREIGN KEY (RoomTypeId) REFERENCES HotelRoomTypes(Id)
    );
    PRINT 'Created table: HotelRooms';
END
GO

-- =============================================
-- Table: HotelGuests
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HotelGuests')
BEGIN
    CREATE TABLE HotelGuests (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Phone NVARCHAR(20) NOT NULL,
        Address NVARCHAR(500),
        City NVARCHAR(100),
        State NVARCHAR(100),
        Country NVARCHAR(100) DEFAULT 'India',
        PostalCode NVARCHAR(20),
        IdType NVARCHAR(50),
        IdNumber NVARCHAR(100),
        DateOfBirth DATE,
        Nationality NVARCHAR(20) DEFAULT 'Indian',
        IsVip BIT DEFAULT 0,
        Notes NVARCHAR(500),
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Created table: HotelGuests';
END
GO

-- =============================================
-- Table: HotelBookings
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HotelBookings')
BEGIN
    CREATE TABLE HotelBookings (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BookingNumber NVARCHAR(20) NOT NULL UNIQUE,
        RoomId INT NOT NULL,
        GuestId INT NOT NULL,
        CheckInDate DATE NOT NULL,
        CheckOutDate DATE NOT NULL,
        ActualCheckIn DATETIME,
        ActualCheckOut DATETIME,
        NumberOfGuests INT NOT NULL DEFAULT 1,
        NumberOfAdults INT NOT NULL DEFAULT 1,
        NumberOfChildren INT DEFAULT 0,
        RoomRate DECIMAL(10,2) NOT NULL DEFAULT 0,
        TaxAmount DECIMAL(10,2) DEFAULT 0,
        ExtraCharges DECIMAL(10,2) DEFAULT 0,
        Discount DECIMAL(10,2) DEFAULT 0,
        TotalAmount DECIMAL(10,2) NOT NULL DEFAULT 0,
        AdvancePayment DECIMAL(10,2) DEFAULT 0,
        Status INT NOT NULL DEFAULT 0, -- 0=Pending, 1=Confirmed, 2=CheckedIn, 3=CheckedOut, 4=Cancelled, 5=NoShow
        PaymentStatus INT NOT NULL DEFAULT 0, -- 0=Pending, 1=PartiallyPaid, 2=Paid, 3=Refunded
        SpecialRequests NVARCHAR(500),
        Notes NVARCHAR(500),
        BookingSource NVARCHAR(50) DEFAULT 'Direct',
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CreatedBy INT,
        CONSTRAINT FK_HotelBookings_Room FOREIGN KEY (RoomId) REFERENCES HotelRooms(Id),
        CONSTRAINT FK_HotelBookings_Guest FOREIGN KEY (GuestId) REFERENCES HotelGuests(Id)
    );
    PRINT 'Created table: HotelBookings';
END
GO

-- =============================================
-- Table: RoomTypeAmenities
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomTypeAmenities')
BEGIN
    CREATE TABLE RoomTypeAmenities (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RoomTypeId INT NOT NULL,
        AmenityId INT NOT NULL,
        CONSTRAINT FK_RoomTypeAmenities_RoomType FOREIGN KEY (RoomTypeId) REFERENCES HotelRoomTypes(Id),
        CONSTRAINT FK_RoomTypeAmenities_Amenity FOREIGN KEY (AmenityId) REFERENCES HotelAmenities(Id)
    );
    PRINT 'Created table: RoomTypeAmenities';
END
GO

-- =============================================
-- Insert Default Room Types
-- =============================================
IF NOT EXISTS (SELECT 1 FROM HotelRoomTypes)
BEGIN
    INSERT INTO HotelRoomTypes (Name, Description, BasePrice, MaxOccupancy, BedType, RoomSize)
    VALUES 
        ('Standard Room', 'Comfortable standard room with essential amenities', 2500.00, 2, 'Queen', 250),
        ('Deluxe Room', 'Spacious deluxe room with premium amenities', 4000.00, 2, 'King', 350),
        ('Superior Room', 'Large superior room with city view', 5500.00, 3, 'King', 450),
        ('Suite', 'Luxurious suite with separate living area', 8000.00, 4, 'King', 600),
        ('Presidential Suite', 'Ultimate luxury with panoramic views and butler service', 15000.00, 4, 'King', 1000);
    PRINT 'Inserted default room types';
END
GO

-- =============================================
-- Insert Default Amenities
-- =============================================
IF NOT EXISTS (SELECT 1 FROM HotelAmenities)
BEGIN
    INSERT INTO HotelAmenities (Name, Description, Icon, Category)
    VALUES 
        ('Wi-Fi', 'High-speed wireless internet', 'fa-wifi', 'Technology'),
        ('Air Conditioning', 'Climate controlled room', 'fa-snowflake', 'Comfort'),
        ('TV', 'Flat screen television', 'fa-tv', 'Entertainment'),
        ('Mini Bar', 'In-room mini bar', 'fa-wine-bottle', 'Food & Beverage'),
        ('Room Service', '24-hour room service', 'fa-bell-concierge', 'Service'),
        ('Safe', 'In-room electronic safe', 'fa-lock', 'Security'),
        ('Coffee Maker', 'Tea/Coffee making facilities', 'fa-mug-hot', 'Comfort'),
        ('Hair Dryer', 'Hair dryer in bathroom', 'fa-wind', 'Bathroom'),
        ('Bathrobe', 'Complimentary bathrobe', 'fa-shirt', 'Bathroom'),
        ('Balcony', 'Private balcony', 'fa-door-open', 'Features'),
        ('Work Desk', 'Work desk with ergonomic chair', 'fa-desktop', 'Business'),
        ('Parking', 'Free parking', 'fa-car', 'Service'),
        ('Pool Access', 'Swimming pool access', 'fa-person-swimming', 'Recreation'),
        ('Gym Access', 'Fitness center access', 'fa-dumbbell', 'Recreation'),
        ('Spa Access', 'Spa & wellness center access', 'fa-spa', 'Recreation');
    PRINT 'Inserted default amenities';
END
GO

-- =============================================
-- Link Default Amenities to Room Types
-- =============================================
IF NOT EXISTS (SELECT 1 FROM RoomTypeAmenities)
BEGIN
    -- Standard Room: Basic amenities
    INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId)
    SELECT 1, Id FROM HotelAmenities WHERE Name IN ('Wi-Fi', 'Air Conditioning', 'TV', 'Safe', 'Hair Dryer');

    -- Deluxe Room: More amenities
    INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId)
    SELECT 2, Id FROM HotelAmenities WHERE Name IN ('Wi-Fi', 'Air Conditioning', 'TV', 'Safe', 'Hair Dryer', 'Coffee Maker', 'Room Service', 'Mini Bar');

    -- Superior Room: Premium amenities
    INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId)
    SELECT 3, Id FROM HotelAmenities WHERE Name IN ('Wi-Fi', 'Air Conditioning', 'TV', 'Safe', 'Hair Dryer', 'Coffee Maker', 'Room Service', 'Mini Bar', 'Bathrobe', 'Work Desk');

    -- Suite: All standard amenities
    INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId)
    SELECT 4, Id FROM HotelAmenities WHERE Name IN ('Wi-Fi', 'Air Conditioning', 'TV', 'Safe', 'Hair Dryer', 'Coffee Maker', 'Room Service', 'Mini Bar', 'Bathrobe', 'Work Desk', 'Balcony', 'Pool Access', 'Gym Access');

    -- Presidential Suite: All amenities
    INSERT INTO RoomTypeAmenities (RoomTypeId, AmenityId)
    SELECT 5, Id FROM HotelAmenities;

    PRINT 'Linked amenities to room types';
END
GO

-- =============================================
-- Insert Sample Rooms
-- =============================================
IF NOT EXISTS (SELECT 1 FROM HotelRooms)
BEGIN
    -- Ground Floor (Standard Rooms)
    INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible)
    VALUES 
        ('101', 1, 1, 0, NULL, 0, 1),
        ('102', 1, 1, 0, NULL, 0, 1),
        ('103', 1, 1, 0, 'Garden', 0, 0),
        ('104', 1, 1, 0, 'Garden', 0, 0);

    -- First Floor (Deluxe Rooms)
    INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible)
    VALUES 
        ('201', 2, 2, 1, 'City', 0, 0),
        ('202', 2, 2, 1, 'City', 0, 0),
        ('203', 2, 2, 1, 'Pool', 0, 0),
        ('204', 2, 2, 1, 'Pool', 0, 0);

    -- Second Floor (Superior Rooms)
    INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible)
    VALUES 
        ('301', 3, 3, 1, 'City', 0, 0),
        ('302', 3, 3, 1, 'City', 0, 0),
        ('303', 3, 3, 1, 'Mountain', 0, 0);

    -- Third Floor (Suites)
    INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible)
    VALUES 
        ('401', 4, 4, 1, 'Panoramic', 0, 0),
        ('402', 4, 4, 1, 'Panoramic', 0, 0);

    -- Top Floor (Presidential Suite)
    INSERT INTO HotelRooms (RoomNumber, Floor, RoomTypeId, HasView, ViewType, IsSmoking, IsAccessible)
    VALUES 
        ('501', 5, 5, 1, 'Panoramic', 0, 0);

    PRINT 'Inserted sample rooms';
END
GO

-- =============================================
-- Stored Procedure: Get Hotel Dashboard Stats
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_GetHotelDashboardStats]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_GetHotelDashboardStats]
GO

CREATE PROCEDURE [dbo].[usp_GetHotelDashboardStats]
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    
    -- Room Statistics
    SELECT 
        COUNT(*) as TotalRooms,
        SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) as AvailableRooms,
        SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) as OccupiedRooms,
        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) as ReservedRooms,
        SUM(CASE WHEN Status IN (3, 4, 5) THEN 1 ELSE 0 END) as MaintenanceRooms
    FROM HotelRooms
    WHERE IsActive = 1;
    
    -- Today's Check-ins and Check-outs
    SELECT 
        (SELECT COUNT(*) FROM HotelBookings WHERE CheckInDate = @Today AND Status IN (1, 2)) as TodayCheckIns,
        (SELECT COUNT(*) FROM HotelBookings WHERE CheckOutDate = @Today AND Status IN (1, 2)) as TodayCheckOuts,
        (SELECT COUNT(*) FROM HotelBookings WHERE CheckInDate = @Today AND Status = 1 AND ActualCheckIn IS NULL) as PendingCheckIns,
        (SELECT COUNT(*) FROM HotelBookings WHERE CheckOutDate = @Today AND Status = 2 AND ActualCheckOut IS NULL) as PendingCheckOuts;
    
    -- Revenue Statistics
    SELECT 
        ISNULL(SUM(CASE WHEN CAST(ActualCheckOut AS DATE) = @Today THEN TotalAmount ELSE 0 END), 0) as TodayRevenue,
        ISNULL(SUM(CASE WHEN MONTH(ActualCheckOut) = MONTH(@Today) AND YEAR(ActualCheckOut) = YEAR(@Today) THEN TotalAmount ELSE 0 END), 0) as MonthlyRevenue
    FROM HotelBookings
    WHERE Status = 3;
END
GO

-- =============================================
-- Stored Procedure: Search Available Rooms
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_SearchAvailableRooms]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_SearchAvailableRooms]
GO

CREATE PROCEDURE [dbo].[usp_SearchAvailableRooms]
    @CheckInDate DATE,
    @CheckOutDate DATE,
    @Adults INT = 2,
    @Children INT = 0,
    @RoomTypeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TotalGuests INT = @Adults + @Children;
    
    SELECT 
        r.Id,
        r.RoomNumber,
        r.Floor,
        r.RoomTypeId,
        r.HasView,
        r.ViewType,
        r.IsSmoking,
        r.IsAccessible,
        rt.Name as RoomTypeName,
        rt.Description as RoomTypeDescription,
        rt.BasePrice,
        rt.MaxOccupancy,
        rt.BedType,
        rt.RoomSize
    FROM HotelRooms r
    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
    WHERE r.IsActive = 1
        AND r.Status = 0 -- Available
        AND rt.IsActive = 1
        AND rt.MaxOccupancy >= @TotalGuests
        AND (@RoomTypeId IS NULL OR r.RoomTypeId = @RoomTypeId)
        AND r.Id NOT IN (
            SELECT RoomId 
            FROM HotelBookings 
            WHERE Status IN (1, 2) -- Confirmed or CheckedIn
                AND NOT (CheckOutDate <= @CheckInDate OR CheckInDate >= @CheckOutDate)
        )
    ORDER BY rt.BasePrice, r.Floor, r.RoomNumber;
END
GO

-- =============================================
-- Stored Procedure: Generate Booking Number
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_GenerateBookingNumber]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_GenerateBookingNumber]
GO

CREATE PROCEDURE [dbo].[usp_GenerateBookingNumber]
    @BookingNumber NVARCHAR(20) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Prefix NVARCHAR(10) = 'HB';
    DECLARE @DatePart NVARCHAR(8) = FORMAT(GETDATE(), 'yyyyMMdd');
    DECLARE @SeqNumber INT;
    
    SELECT @SeqNumber = ISNULL(MAX(CAST(RIGHT(BookingNumber, 4) AS INT)), 0) + 1
    FROM HotelBookings
    WHERE BookingNumber LIKE @Prefix + @DatePart + '%';
    
    SET @BookingNumber = @Prefix + @DatePart + RIGHT('0000' + CAST(@SeqNumber AS NVARCHAR(4)), 4);
END
GO

-- =============================================
-- Stored Procedure: Create Booking
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_CreateHotelBooking]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_CreateHotelBooking]
GO

CREATE PROCEDURE [dbo].[usp_CreateHotelBooking]
    @RoomId INT,
    @GuestId INT,
    @CheckInDate DATE,
    @CheckOutDate DATE,
    @NumberOfAdults INT = 1,
    @NumberOfChildren INT = 0,
    @SpecialRequests NVARCHAR(500) = NULL,
    @BookingSource NVARCHAR(50) = 'Direct',
    @CreatedBy INT = NULL,
    @BookingId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @BookingNumber NVARCHAR(20);
    DECLARE @RoomRate DECIMAL(10,2);
    DECLARE @Nights INT;
    DECLARE @TaxRate DECIMAL(5,2) = 0.18; -- 18% GST
    DECLARE @TaxAmount DECIMAL(10,2);
    DECLARE @TotalAmount DECIMAL(10,2);
    
    -- Get room rate
    SELECT @RoomRate = rt.BasePrice
    FROM HotelRooms r
    INNER JOIN HotelRoomTypes rt ON r.RoomTypeId = rt.Id
    WHERE r.Id = @RoomId;
    
    -- Calculate nights
    SET @Nights = DATEDIFF(DAY, @CheckInDate, @CheckOutDate);
    
    -- Calculate amounts
    DECLARE @SubTotal DECIMAL(10,2) = @RoomRate * @Nights;
    SET @TaxAmount = @SubTotal * @TaxRate;
    SET @TotalAmount = @SubTotal + @TaxAmount;
    
    -- Generate booking number
    EXEC usp_GenerateBookingNumber @BookingNumber OUTPUT;
    
    -- Insert booking
    INSERT INTO HotelBookings (
        BookingNumber, RoomId, GuestId, CheckInDate, CheckOutDate,
        NumberOfGuests, NumberOfAdults, NumberOfChildren,
        RoomRate, TaxAmount, TotalAmount,
        Status, PaymentStatus, SpecialRequests, BookingSource, CreatedBy
    )
    VALUES (
        @BookingNumber, @RoomId, @GuestId, @CheckInDate, @CheckOutDate,
        @NumberOfAdults + @NumberOfChildren, @NumberOfAdults, @NumberOfChildren,
        @RoomRate, @TaxAmount, @TotalAmount,
        1, 0, @SpecialRequests, @BookingSource, @CreatedBy
    );
    
    SET @BookingId = SCOPE_IDENTITY();
    
    -- Update room status to Reserved
    UPDATE HotelRooms SET Status = 2, UpdatedAt = GETDATE() WHERE Id = @RoomId;
END
GO

-- =============================================
-- Stored Procedure: Check In Guest
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_CheckInGuest]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_CheckInGuest]
GO

CREATE PROCEDURE [dbo].[usp_CheckInGuest]
    @BookingId INT,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RoomId INT;
    
    -- Get room ID
    SELECT @RoomId = RoomId FROM HotelBookings WHERE Id = @BookingId;
    
    -- Update booking
    UPDATE HotelBookings 
    SET Status = 2, -- CheckedIn
        ActualCheckIn = GETDATE(),
        Notes = ISNULL(@Notes, Notes),
        UpdatedAt = GETDATE()
    WHERE Id = @BookingId;
    
    -- Update room status to Occupied
    UPDATE HotelRooms SET Status = 1, UpdatedAt = GETDATE() WHERE Id = @RoomId;
END
GO

-- =============================================
-- Stored Procedure: Check Out Guest
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_CheckOutGuest]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_CheckOutGuest]
GO

CREATE PROCEDURE [dbo].[usp_CheckOutGuest]
    @BookingId INT,
    @ExtraCharges DECIMAL(10,2) = 0,
    @PaymentReceived DECIMAL(10,2) = 0,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RoomId INT;
    DECLARE @TotalAmount DECIMAL(10,2);
    DECLARE @AdvancePayment DECIMAL(10,2);
    
    -- Get booking details
    SELECT @RoomId = RoomId, @TotalAmount = TotalAmount, @AdvancePayment = AdvancePayment
    FROM HotelBookings WHERE Id = @BookingId;
    
    -- Update amounts
    SET @TotalAmount = @TotalAmount + @ExtraCharges;
    
    -- Determine payment status
    DECLARE @PaymentStatus INT;
    SET @PaymentStatus = CASE 
        WHEN @AdvancePayment + @PaymentReceived >= @TotalAmount THEN 2 -- Paid
        WHEN @AdvancePayment + @PaymentReceived > 0 THEN 1 -- PartiallyPaid
        ELSE 0 -- Pending
    END;
    
    -- Update booking
    UPDATE HotelBookings 
    SET Status = 3, -- CheckedOut
        ActualCheckOut = GETDATE(),
        ExtraCharges = @ExtraCharges,
        TotalAmount = @TotalAmount,
        AdvancePayment = @AdvancePayment + @PaymentReceived,
        PaymentStatus = @PaymentStatus,
        Notes = ISNULL(@Notes, Notes),
        UpdatedAt = GETDATE()
    WHERE Id = @BookingId;
    
    -- Update room status to Cleaning
    UPDATE HotelRooms SET Status = 4, UpdatedAt = GETDATE() WHERE Id = @RoomId;
END
GO

PRINT 'Hotel booking database setup completed successfully!';
GO
