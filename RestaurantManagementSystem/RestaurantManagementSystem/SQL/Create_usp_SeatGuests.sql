-- =============================================
-- Create usp_SeatGuests Stored Procedure
-- Required for Bar Order Create functionality
-- =============================================

USE [YourDatabaseName] -- Change this to your actual database name
GO

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

PRINT 'Successfully created usp_SeatGuests stored procedure';
GO
