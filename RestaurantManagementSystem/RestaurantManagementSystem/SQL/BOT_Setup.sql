-- =============================================
-- BOT (Beverage Order Ticket) Module Setup
-- Implements BR-BOT-001 through BR-BOT-010
-- =============================================

-- Step 1: Add IsAlcoholic flag to MenuItems if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MenuItems') AND name = 'IsAlcoholic')
BEGIN
    ALTER TABLE dbo.MenuItems ADD IsAlcoholic BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsAlcoholic column to MenuItems';
END
GO

-- Step 2: Create BOT_Header table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BOT_Header')
BEGIN
    CREATE TABLE dbo.BOT_Header (
        BOT_ID INT IDENTITY(1,1) PRIMARY KEY,
        BOT_No VARCHAR(50) NOT NULL UNIQUE, -- Format: BOT-YYYYMM-####
        OrderId INT NOT NULL,
        OrderNumber VARCHAR(50),
        TableName VARCHAR(100),
        GuestName VARCHAR(200),
        ServerName VARCHAR(200),
        Status INT NOT NULL DEFAULT 0, -- 0=New/Open, 1=InProgress, 2=Served/Ready, 3=Billed/Closed, 4=Void
        SubtotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CreatedBy VARCHAR(200),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedBy VARCHAR(200),
        ServedAt DATETIME NULL,
        BilledAt DATETIME NULL,
        VoidedAt DATETIME NULL,
        VoidReason VARCHAR(500) NULL,
        
        CONSTRAINT FK_BOT_Header_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id)
    );
    
    CREATE INDEX IX_BOT_Header_OrderId ON dbo.BOT_Header(OrderId);
    CREATE INDEX IX_BOT_Header_Status ON dbo.BOT_Header(Status);
    CREATE INDEX IX_BOT_Header_CreatedAt ON dbo.BOT_Header(CreatedAt DESC);
    
    PRINT 'Created BOT_Header table';
END
GO

-- Step 3: Create BOT_Detail table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BOT_Detail')
BEGIN
    CREATE TABLE dbo.BOT_Detail (
        BOT_Detail_ID INT IDENTITY(1,1) PRIMARY KEY,
        BOT_ID INT NOT NULL,
        OrderItemId INT NOT NULL,
        MenuItemId INT NOT NULL,
        MenuItemName VARCHAR(200) NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        TaxRate DECIMAL(5,2) NOT NULL DEFAULT 0,
        TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        IsAlcoholic BIT NOT NULL DEFAULT 0,
        SpecialInstructions VARCHAR(500),
        Status INT NOT NULL DEFAULT 0, -- 0=New, 1=InProgress, 2=Ready, 3=Served
        StartTime DATETIME NULL,
        CompletionTime DATETIME NULL,
        Notes VARCHAR(500),
        
        CONSTRAINT FK_BOT_Detail_BOT_Header FOREIGN KEY (BOT_ID) REFERENCES dbo.BOT_Header(BOT_ID) ON DELETE CASCADE,
        CONSTRAINT FK_BOT_Detail_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(Id),
        CONSTRAINT FK_BOT_Detail_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems(Id)
    );
    
    CREATE INDEX IX_BOT_Detail_BOT_ID ON dbo.BOT_Detail(BOT_ID);
    CREATE INDEX IX_BOT_Detail_OrderItemId ON dbo.BOT_Detail(OrderItemId);
    CREATE INDEX IX_BOT_Detail_Status ON dbo.BOT_Detail(Status);
    
    PRINT 'Created BOT_Detail table';
END
GO

-- Step 4: Create BOT_Audit table for compliance
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BOT_Audit')
BEGIN
    CREATE TABLE dbo.BOT_Audit (
        AuditID INT IDENTITY(1,1) PRIMARY KEY,
        BOT_ID INT NOT NULL,
        BOT_No VARCHAR(50) NOT NULL,
        Action VARCHAR(50) NOT NULL, -- CREATE, UPDATE, PRINT, SERVE, VOID, MERGE, AGE_VERIFY
        OldStatus INT NULL,
        NewStatus INT NULL,
        UserId INT NULL,
        UserName VARCHAR(200),
        DeviceInfo VARCHAR(500),
        Reason VARCHAR(500),
        Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
        AdditionalData VARCHAR(MAX) -- JSON for flexible audit data
    );
    
    CREATE INDEX IX_BOT_Audit_BOT_ID ON dbo.BOT_Audit(BOT_ID);
    CREATE INDEX IX_BOT_Audit_Timestamp ON dbo.BOT_Audit(Timestamp DESC);
    
    PRINT 'Created BOT_Audit table';
END
GO

-- Step 5: Create BOT_Bills table (separate billing from food)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BOT_Bills')
BEGIN
    CREATE TABLE dbo.BOT_Bills (
        BillID INT IDENTITY(1,1) PRIMARY KEY,
        BillNo VARCHAR(50) NOT NULL UNIQUE, -- Format: BOTBILL-YYYYMMDD-####
        BOT_ID INT NOT NULL,
        BOT_No VARCHAR(50) NOT NULL,
        OrderId INT NOT NULL,
        OrderNumber VARCHAR(50),
        SubtotalAmount DECIMAL(18,2) NOT NULL,
        TaxAmount DECIMAL(18,2) NOT NULL,
        ExciseAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        VATAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        GSTAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        GrandTotal DECIMAL(18,2) NOT NULL,
        PaymentStatus INT NOT NULL DEFAULT 0, -- 0=Unpaid, 1=Partial, 2=Paid
        PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        RemainingAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CreatedBy VARCHAR(200),
        CompletedAt DATETIME NULL,
        
        CONSTRAINT FK_BOT_Bills_BOT_Header FOREIGN KEY (BOT_ID) REFERENCES dbo.BOT_Header(BOT_ID),
        CONSTRAINT FK_BOT_Bills_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id)
    );
    
    CREATE INDEX IX_BOT_Bills_BOT_ID ON dbo.BOT_Bills(BOT_ID);
    CREATE INDEX IX_BOT_Bills_OrderId ON dbo.BOT_Bills(OrderId);
    CREATE INDEX IX_BOT_Bills_CreatedAt ON dbo.BOT_Bills(CreatedAt DESC);
    CREATE INDEX IX_BOT_Bills_PaymentStatus ON dbo.BOT_Bills(PaymentStatus);
    
    PRINT 'Created BOT_Bills table';
END
GO

-- Step 6: Create BOT_Payments table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BOT_Payments')
BEGIN
    CREATE TABLE dbo.BOT_Payments (
        PaymentID INT IDENTITY(1,1) PRIMARY KEY,
        BillID INT NOT NULL,
        BillNo VARCHAR(50) NOT NULL,
        PaymentMethod VARCHAR(50) NOT NULL, -- CASH, CARD, UPI, etc.
        Amount DECIMAL(18,2) NOT NULL,
        TransactionRef VARCHAR(200),
        PaymentDate DATETIME NOT NULL DEFAULT GETDATE(),
        ReceivedBy VARCHAR(200),
        Notes VARCHAR(500),
        
        CONSTRAINT FK_BOT_Payments_BOT_Bills FOREIGN KEY (BillID) REFERENCES dbo.BOT_Bills(BillID)
    );
    
    CREATE INDEX IX_BOT_Payments_BillID ON dbo.BOT_Payments(BillID);
    CREATE INDEX IX_BOT_Payments_PaymentDate ON dbo.BOT_Payments(PaymentDate DESC);
    
    PRINT 'Created BOT_Payments table';
END
GO

-- =============================================
-- Stored Procedures for BOT Operations
-- =============================================

-- SP: Seat Guests at Table (creates TableTurnover record)
IF OBJECT_ID('dbo.usp_SeatGuests', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_SeatGuests;
GO

CREATE PROCEDURE dbo.usp_SeatGuests
    @TableId INT,
    @GuestName NVARCHAR(100),
    @PartySize INT,
    @UserId INT,
    @ReservationId INT = NULL,
    @WaitlistId INT = NULL,
    @Notes NVARCHAR(500) = NULL,
    @TargetTurnTimeMinutes INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TurnoverId INT;
    
    -- Check if table exists
    IF NOT EXISTS (SELECT 1 FROM Tables WHERE Id = @TableId)
    BEGIN
        RAISERROR('Table does not exist.', 16, 1);
        RETURN -1;
    END
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Create new table turnover record
        INSERT INTO TableTurnovers (
            TableId,
            ReservationId,
            WaitlistId,
            GuestName,
            PartySize,
            SeatedAt,
            Status,
            Notes,
            TargetTurnTimeMinutes
        ) VALUES (
            @TableId,
            @ReservationId,
            @WaitlistId,
            @GuestName,
            @PartySize,
            GETDATE(),
            0, -- Seated
            @Notes,
            @TargetTurnTimeMinutes
        );
        
        SET @TurnoverId = SCOPE_IDENTITY();
        
        -- Update table status to occupied
        UPDATE Tables
        SET Status = 2, -- Occupied
            LastOccupiedAt = GETDATE()
        WHERE Id = @TableId;
        
        -- Update reservation status if provided
        IF @ReservationId IS NOT NULL
        BEGIN
            UPDATE Reservations
            SET Status = 2, -- Seated
                UpdatedAt = GETDATE()
            WHERE Id = @ReservationId;
        END
        
        -- Update waitlist status if provided
        IF @WaitlistId IS NOT NULL
        BEGIN
            UPDATE Waitlist
            SET Status = 2, -- Seated
                SeatedAt = GETDATE()
            WHERE Id = @WaitlistId;
        END
        
        COMMIT TRANSACTION;
        
        -- Return the TurnoverId
        SELECT @TurnoverId AS TurnoverId;
        RETURN @TurnoverId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- SP: Generate next BOT number
IF OBJECT_ID('dbo.GetNextBOTNumber', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetNextBOTNumber;
GO

CREATE PROCEDURE dbo.GetNextBOTNumber
    @OutletCode VARCHAR(10) = 'OUT1'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CurrentMonth VARCHAR(6) = FORMAT(GETDATE(), 'yyyyMM');
    DECLARE @Prefix VARCHAR(20) = 'BOT-' + @CurrentMonth + '-';
    DECLARE @LastNumber INT;
    
    -- Get the last BOT number for current month
    SELECT TOP 1 @LastNumber = CAST(RIGHT(BOT_No, 4) AS INT)
    FROM dbo.BOT_Header
    WHERE BOT_No LIKE @Prefix + '%'
    ORDER BY BOT_ID DESC;
    
    IF @LastNumber IS NULL
        SET @LastNumber = 0;
    
    -- Generate next number with zero padding
    SELECT @Prefix + RIGHT('0000' + CAST(@LastNumber + 1 AS VARCHAR(4)), 4) AS NextBOTNumber;
END
GO

-- SP: Get BOT by Status
IF OBJECT_ID('dbo.GetBOTsByStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetBOTsByStatus;
GO

CREATE PROCEDURE dbo.GetBOTsByStatus
    @Status INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        bh.BOT_ID,
        bh.BOT_No,
        bh.OrderId,
        bh.OrderNumber,
        bh.TableName,
        bh.GuestName,
        bh.ServerName,
        bh.Status,
        bh.SubtotalAmount,
        bh.TaxAmount,
        bh.TotalAmount,
        bh.CreatedAt,
        DATEDIFF(MINUTE, bh.CreatedAt, GETDATE()) AS MinutesSinceCreated
    FROM dbo.BOT_Header bh
    WHERE (@Status IS NULL OR bh.Status = @Status)
    ORDER BY bh.CreatedAt DESC;
END
GO

-- SP: Get BOT Details
IF OBJECT_ID('dbo.GetBOTDetails', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetBOTDetails;
GO

CREATE PROCEDURE dbo.GetBOTDetails
    @BOT_ID INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Header
    SELECT 
        BOT_ID, BOT_No, OrderId, OrderNumber, TableName, GuestName,
        ServerName, Status, SubtotalAmount, TaxAmount, TotalAmount,
        CreatedAt, UpdatedAt, ServedAt, BilledAt, VoidedAt, VoidReason,
        CreatedBy, UpdatedBy,
        DATEDIFF(MINUTE, CreatedAt, GETDATE()) AS MinutesSinceCreated
    FROM dbo.BOT_Header
    WHERE BOT_ID = @BOT_ID;
    
    -- Details
    SELECT 
        bd.BOT_Detail_ID, bd.BOT_ID, bd.OrderItemId, bd.MenuItemId,
        bd.MenuItemName, bd.Quantity, bd.UnitPrice, bd.Amount,
        bd.TaxRate, bd.TaxAmount, bd.IsAlcoholic, bd.SpecialInstructions,
        bd.Status, bd.StartTime, bd.CompletionTime, bd.Notes,
        CASE 
            WHEN bd.CompletionTime IS NOT NULL THEN DATEDIFF(MINUTE, bd.StartTime, bd.CompletionTime)
            WHEN bd.StartTime IS NOT NULL THEN DATEDIFF(MINUTE, bd.StartTime, GETDATE())
            ELSE 0
        END AS MinutesCooking
    FROM dbo.BOT_Detail bd
    WHERE bd.BOT_ID = @BOT_ID
    ORDER BY bd.BOT_Detail_ID;
END
GO

-- SP: Update BOT Status
IF OBJECT_ID('dbo.UpdateBOTStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.UpdateBOTStatus;
GO

CREATE PROCEDURE dbo.UpdateBOTStatus
    @BOT_ID INT,
    @NewStatus INT,
    @UpdatedBy VARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @OldStatus INT;
    DECLARE @BOT_No VARCHAR(50);
    
    SELECT @OldStatus = Status, @BOT_No = BOT_No
    FROM dbo.BOT_Header
    WHERE BOT_ID = @BOT_ID;
    
    UPDATE dbo.BOT_Header
    SET Status = @NewStatus,
        UpdatedAt = GETDATE(),
        UpdatedBy = @UpdatedBy,
        ServedAt = CASE WHEN @NewStatus = 2 THEN GETDATE() ELSE ServedAt END,
        BilledAt = CASE WHEN @NewStatus = 3 THEN GETDATE() ELSE BilledAt END
    WHERE BOT_ID = @BOT_ID;
    
    -- Audit
    INSERT INTO dbo.BOT_Audit (BOT_ID, BOT_No, Action, OldStatus, NewStatus, UserName, Timestamp)
    VALUES (@BOT_ID, @BOT_No, 'STATUS_CHANGE', @OldStatus, @NewStatus, @UpdatedBy, GETDATE());
END
GO

-- SP: Void BOT
IF OBJECT_ID('dbo.VoidBOT', 'P') IS NOT NULL
    DROP PROCEDURE dbo.VoidBOT;
GO

CREATE PROCEDURE dbo.VoidBOT
    @BOT_ID INT,
    @Reason VARCHAR(500),
    @VoidedBy VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @BOT_No VARCHAR(50);
    DECLARE @OldStatus INT;
    
    SELECT @BOT_No = BOT_No, @OldStatus = Status
    FROM dbo.BOT_Header
    WHERE BOT_ID = @BOT_ID;
    
    UPDATE dbo.BOT_Header
    SET Status = 4,
        VoidedAt = GETDATE(),
        VoidReason = @Reason,
        UpdatedBy = @VoidedBy,
        UpdatedAt = GETDATE()
    WHERE BOT_ID = @BOT_ID;
    
    -- Audit
    INSERT INTO dbo.BOT_Audit (BOT_ID, BOT_No, Action, OldStatus, NewStatus, UserName, Reason, Timestamp)
    VALUES (@BOT_ID, @BOT_No, 'VOID', @OldStatus, 4, @VoidedBy, @Reason, GETDATE());
END
GO

-- SP: Get BOT Dashboard Stats
IF OBJECT_ID('dbo.GetBOTDashboardStats', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetBOTDashboardStats;
GO

CREATE PROCEDURE dbo.GetBOTDashboardStats
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewBOTsCount,
        SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS InProgressBOTsCount,
        SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS ReadyBOTsCount,
        SUM(CASE WHEN Status = 3 AND CAST(BilledAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS BilledTodayCount,
        COUNT(CASE WHEN Status < 4 THEN 1 END) AS TotalActiveBOTs,
        AVG(CASE WHEN Status = 2 THEN DATEDIFF(MINUTE, CreatedAt, ServedAt) END) AS AvgPrepTimeMinutes
    FROM dbo.BOT_Header
    WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE);
END
GO

PRINT 'BOT Module Setup Complete - All tables and stored procedures created successfully';
GO
