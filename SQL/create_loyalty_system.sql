-- ================================================
-- Guest Loyalty & Points Redemption System
-- Database Schema Creation
-- ================================================

-- 1. Guest Loyalty Master Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestLoyaltyMaster')
BEGIN
    CREATE TABLE GuestLoyaltyMaster (
        CardNo VARCHAR(20) PRIMARY KEY,
        GuestName VARCHAR(100) NOT NULL,
        Phone VARCHAR(15),
        Email VARCHAR(100),
        JoinDate DATE DEFAULT GETDATE(),
        TotalPoints DECIMAL(10,2) DEFAULT 0,
        Status VARCHAR(20) DEFAULT 'ACTIVE' CHECK (Status IN ('ACTIVE','BLOCKED','EXPIRED')),
        CreatedDate DATETIME DEFAULT GETDATE(),
        LastModifiedDate DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_GuestLoyalty_Phone UNIQUE (Phone)
    );
    PRINT 'Table GuestLoyaltyMaster created successfully';
END
ELSE
    PRINT 'Table GuestLoyaltyMaster already exists';
GO

-- 2. Guest Loyalty Transaction Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestLoyaltyTransaction')
BEGIN
    CREATE TABLE GuestLoyaltyTransaction (
        TxnId INT IDENTITY(1,1) PRIMARY KEY,
        CardNo VARCHAR(20) NOT NULL,
        BillNo VARCHAR(50),
        OutletType VARCHAR(20) CHECK (OutletType IN ('RESTAURANT','BAR')),
        TxnType VARCHAR(20) CHECK (TxnType IN ('EARN','REDEEM','ADJUSTMENT','EXPIRY')),
        TxnPoints DECIMAL(10,2) NOT NULL,
        ValueAmount DECIMAL(10,2),
        TxnDate DATETIME DEFAULT GETDATE(),
        ExpiryDate DATE,
        IsExpired BIT DEFAULT 0,
        Remarks VARCHAR(255),
        CreatedBy VARCHAR(50),
        FOREIGN KEY (CardNo) REFERENCES GuestLoyaltyMaster(CardNo)
    );
    PRINT 'Table GuestLoyaltyTransaction created successfully';
END
ELSE
    PRINT 'Table GuestLoyaltyTransaction already exists';
GO

-- 3. Loyalty Configuration Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyConfig')
BEGIN
    CREATE TABLE LoyaltyConfig (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OutletType VARCHAR(20) CHECK (OutletType IN ('RESTAURANT','BAR')),
        EarnRate DECIMAL(10,2) NOT NULL,
        RedemptionValue DECIMAL(10,2) NOT NULL DEFAULT 1,
        MinBillToEarn DECIMAL(10,2) NOT NULL DEFAULT 0,
        MaxPointsPerBill DECIMAL(10,2) NOT NULL DEFAULT 9999,
        ExpiryDays INT NOT NULL DEFAULT 365,
        EligiblePaymentModes VARCHAR(200),
        IsActive BIT DEFAULT 1,
        CreatedDate DATETIME DEFAULT GETDATE(),
        LastModifiedDate DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_LoyaltyConfig_OutletType UNIQUE (OutletType)
    );
    PRINT 'Table LoyaltyConfig created successfully';
END
ELSE
    PRINT 'Table LoyaltyConfig already exists';
GO

-- 4. Insert Default Configuration for Restaurant
-- Payment Method IDs: 1=CASH, 2=CREDIT_CARD, 3=DEBIT_CARD, 7=UPI
IF NOT EXISTS (SELECT * FROM LoyaltyConfig WHERE OutletType = 'RESTAURANT')
BEGIN
    INSERT INTO LoyaltyConfig (OutletType, EarnRate, RedemptionValue, MinBillToEarn, MaxPointsPerBill, ExpiryDays, EligiblePaymentModes)
    VALUES ('RESTAURANT', 100, 1, 300, 500, 365, '1,2,3,7');
    PRINT 'Default Restaurant loyalty config inserted';
END
ELSE
BEGIN
    UPDATE LoyaltyConfig 
    SET EligiblePaymentModes = '1,2,3,7'
    WHERE OutletType = 'RESTAURANT';
    PRINT 'Restaurant loyalty config updated with payment method IDs';
END
GO

-- 5. Insert Default Configuration for Bar
-- Payment Method IDs: 1=CASH, 2=CREDIT_CARD, 3=DEBIT_CARD
IF NOT EXISTS (SELECT * FROM LoyaltyConfig WHERE OutletType = 'BAR')
BEGIN
    INSERT INTO LoyaltyConfig (OutletType, EarnRate, RedemptionValue, MinBillToEarn, MaxPointsPerBill, ExpiryDays, EligiblePaymentModes)
    VALUES ('BAR', 200, 1, 500, 300, 365, '1,2,3');
    PRINT 'Default Bar loyalty config inserted';
END
ELSE
BEGIN
    UPDATE LoyaltyConfig 
    SET EligiblePaymentModes = '1,2,3'
    WHERE OutletType = 'BAR';
    PRINT 'Bar loyalty config updated with payment method IDs';
END
GO

-- 6. Create Index for Performance
CREATE NONCLUSTERED INDEX IX_GuestLoyaltyTransaction_CardNo 
ON GuestLoyaltyTransaction(CardNo, TxnDate DESC);
GO

CREATE NONCLUSTERED INDEX IX_GuestLoyaltyTransaction_BillNo 
ON GuestLoyaltyTransaction(BillNo);
GO

-- 7. Stored Procedure: Get Guest Loyalty Details
CREATE OR ALTER PROCEDURE sp_GetGuestLoyaltyDetails
    @SearchTerm VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        CardNo,
        GuestName,
        Phone,
        Email,
        JoinDate,
        TotalPoints,
        Status,
        DATEDIFF(DAY, CreatedDate, GETDATE()) AS DaysSinceJoined
    FROM GuestLoyaltyMaster
    WHERE Status = 'ACTIVE'
    AND (
        CardNo LIKE '%' + @SearchTerm + '%'
        OR Phone LIKE '%' + @SearchTerm + '%'
        OR GuestName LIKE '%' + @SearchTerm + '%'
    )
    ORDER BY GuestName;
END
GO

-- 8. Stored Procedure: Calculate Points to Earn
-- @PaymentMode should now be PaymentMethod ID (e.g., '1' for CASH, '2' for CREDIT_CARD)
CREATE OR ALTER PROCEDURE sp_CalculatePointsToEarn
    @BillAmount DECIMAL(10,2),
    @OutletType VARCHAR(20),
    @PaymentMethodId VARCHAR(50),
    @PointsEarned DECIMAL(10,2) OUTPUT,
    @IsEligible BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @EarnRate DECIMAL(10,2);
    DECLARE @MinBill DECIMAL(10,2);
    DECLARE @MaxPoints DECIMAL(10,2);
    DECLARE @EligibleModes VARCHAR(200);
    
    -- Get configuration
    SELECT 
        @EarnRate = EarnRate,
        @MinBill = MinBillToEarn,
        @MaxPoints = MaxPointsPerBill,
        @EligibleModes = EligiblePaymentModes
    FROM LoyaltyConfig
    WHERE OutletType = @OutletType AND IsActive = 1;
    
    -- Check eligibility: Bill amount and payment method ID
    IF @BillAmount < @MinBill OR CHARINDEX(@PaymentMethodId, @EligibleModes) = 0
    BEGIN
        SET @IsEligible = 0;
        SET @PointsEarned = 0;
        RETURN;
    END
    
    -- Calculate points
    SET @PointsEarned = FLOOR(@BillAmount / @EarnRate);
    
    -- Apply cap
    IF @PointsEarned > @MaxPoints
        SET @PointsEarned = @MaxPoints;
    
    SET @IsEligible = 1;
END
GO

-- 9. Stored Procedure: Redeem Points
CREATE OR ALTER PROCEDURE sp_RedeemLoyaltyPoints
    @CardNo VARCHAR(20),
    @PointsToRedeem DECIMAL(10,2),
    @BillNo VARCHAR(50),
    @OutletType VARCHAR(20),
    @CreatedBy VARCHAR(50),
    @RedemptionValue DECIMAL(10,2) OUTPUT,
    @RemainingPoints DECIMAL(10,2) OUTPUT,
    @ErrorMessage VARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @AvailablePoints DECIMAL(10,2);
        DECLARE @ConfigRedemptionValue DECIMAL(10,2);
        
        -- Get available points
        SELECT @AvailablePoints = TotalPoints 
        FROM GuestLoyaltyMaster 
        WHERE CardNo = @CardNo AND Status = 'ACTIVE';
        
        -- Check if card exists
        IF @AvailablePoints IS NULL
        BEGIN
            SET @ErrorMessage = 'Invalid or inactive loyalty card';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Check sufficient points
        IF @PointsToRedeem > @AvailablePoints
        BEGIN
            SET @ErrorMessage = 'Insufficient points. Available: ' + CAST(@AvailablePoints AS VARCHAR(20));
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Get redemption value from config
        SELECT @ConfigRedemptionValue = RedemptionValue 
        FROM LoyaltyConfig 
        WHERE OutletType = @OutletType AND IsActive = 1;
        
        -- Calculate redemption amount
        SET @RedemptionValue = @PointsToRedeem * @ConfigRedemptionValue;
        
        -- Insert redemption transaction
        INSERT INTO GuestLoyaltyTransaction (CardNo, BillNo, OutletType, TxnType, TxnPoints, ValueAmount, Remarks, CreatedBy)
        VALUES (@CardNo, @BillNo, @OutletType, 'REDEEM', -@PointsToRedeem, @RedemptionValue, 'Points redeemed at billing', @CreatedBy);
        
        -- Update master balance
        UPDATE GuestLoyaltyMaster 
        SET TotalPoints = TotalPoints - @PointsToRedeem,
            LastModifiedDate = GETDATE()
        WHERE CardNo = @CardNo;
        
        -- Get remaining points
        SELECT @RemainingPoints = TotalPoints 
        FROM GuestLoyaltyMaster 
        WHERE CardNo = @CardNo;
        
        SET @ErrorMessage = NULL;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
GO

-- 10. Stored Procedure: Earn Points on Bill Payment
-- @PaymentMethodId should be PaymentMethod ID (e.g., '1' for CASH)
CREATE OR ALTER PROCEDURE sp_EarnLoyaltyPoints
    @CardNo VARCHAR(20),
    @BillNo VARCHAR(50),
    @BillAmount DECIMAL(10,2),
    @OutletType VARCHAR(20),
    @PaymentMethodId VARCHAR(50),
    @CreatedBy VARCHAR(50),
    @PointsEarned DECIMAL(10,2) OUTPUT,
    @ErrorMessage VARCHAR(255) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @IsEligible BIT;
        DECLARE @ExpiryDays INT;
        DECLARE @ExpiryDate DATE;
        
        -- Calculate points
        EXEC sp_CalculatePointsToEarn 
            @BillAmount, 
            @OutletType, 
            @PaymentMethodId, 
            @PointsEarned OUTPUT, 
            @IsEligible OUTPUT;
        
        IF @IsEligible = 0 OR @PointsEarned = 0
        BEGIN
            SET @ErrorMessage = 'Bill does not qualify for loyalty points';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Get expiry configuration
        SELECT @ExpiryDays = ExpiryDays 
        FROM LoyaltyConfig 
        WHERE OutletType = @OutletType AND IsActive = 1;
        
        SET @ExpiryDate = DATEADD(DAY, @ExpiryDays, GETDATE());
        
        -- Check if card exists
        IF NOT EXISTS (SELECT 1 FROM GuestLoyaltyMaster WHERE CardNo = @CardNo AND Status = 'ACTIVE')
        BEGIN
            SET @ErrorMessage = 'Invalid or inactive loyalty card';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Check if points already earned for this bill
        IF EXISTS (SELECT 1 FROM GuestLoyaltyTransaction WHERE BillNo = @BillNo AND TxnType = 'EARN')
        BEGIN
            SET @ErrorMessage = 'Points already earned for this bill';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        -- Insert earn transaction
        INSERT INTO GuestLoyaltyTransaction 
            (CardNo, BillNo, OutletType, TxnType, TxnPoints, ValueAmount, ExpiryDate, Remarks, CreatedBy)
        VALUES 
            (@CardNo, @BillNo, @OutletType, 'EARN', @PointsEarned, @BillAmount, @ExpiryDate, 
             'Points earned on bill payment', @CreatedBy);
        
        -- Update master balance
        UPDATE GuestLoyaltyMaster 
        SET TotalPoints = TotalPoints + @PointsEarned,
            LastModifiedDate = GETDATE()
        WHERE CardNo = @CardNo;
        
        SET @ErrorMessage = NULL;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
GO

-- 11. Stored Procedure: Get Loyalty Transaction History
CREATE OR ALTER PROCEDURE sp_GetLoyaltyTransactionHistory
    @CardNo VARCHAR(20),
    @StartDate DATE = NULL,
    @EndDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        TxnId,
        BillNo,
        OutletType,
        TxnType,
        TxnPoints,
        ValueAmount,
        TxnDate,
        ExpiryDate,
        IsExpired,
        Remarks,
        CreatedBy
    FROM GuestLoyaltyTransaction
    WHERE CardNo = @CardNo
    AND (@StartDate IS NULL OR CAST(TxnDate AS DATE) >= @StartDate)
    AND (@EndDate IS NULL OR CAST(TxnDate AS DATE) <= @EndDate)
    ORDER BY TxnDate DESC;
END
GO

-- 12. Job: Expire Old Points (Run Daily)
CREATE OR ALTER PROCEDURE sp_ExpireLoyaltyPoints
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Mark expired transactions
        UPDATE GuestLoyaltyTransaction
        SET IsExpired = 1
        WHERE ExpiryDate < CAST(GETDATE() AS DATE)
        AND TxnType = 'EARN'
        AND IsExpired = 0;
        
        -- Deduct expired points from master
        DECLARE @ExpiredPoints TABLE (CardNo VARCHAR(20), ExpiredAmount DECIMAL(10,2));
        
        INSERT INTO @ExpiredPoints
        SELECT 
            CardNo,
            SUM(TxnPoints) AS ExpiredAmount
        FROM GuestLoyaltyTransaction
        WHERE ExpiryDate < CAST(GETDATE() AS DATE)
        AND TxnType = 'EARN'
        AND IsExpired = 1
        GROUP BY CardNo;
        
        -- Update master balances
        UPDATE m
        SET m.TotalPoints = m.TotalPoints - ep.ExpiredAmount,
            m.LastModifiedDate = GETDATE()
        FROM GuestLoyaltyMaster m
        INNER JOIN @ExpiredPoints ep ON m.CardNo = ep.CardNo;
        
        -- Log expiry transactions
        INSERT INTO GuestLoyaltyTransaction (CardNo, OutletType, TxnType, TxnPoints, Remarks)
        SELECT 
            CardNo,
            'RESTAURANT',
            'EXPIRY',
            -ExpiredAmount,
            'Points expired automatically'
        FROM @ExpiredPoints
        WHERE ExpiredAmount > 0;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        PRINT ERROR_MESSAGE();
    END CATCH
END
GO

PRINT '============================================';
PRINT 'Loyalty Points System Schema Created Successfully';
PRINT '============================================';
