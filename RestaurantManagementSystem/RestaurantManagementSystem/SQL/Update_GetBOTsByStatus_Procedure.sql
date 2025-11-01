-- =============================================
-- Update GetBOTsByStatus stored procedure to filter by KitchenStation
-- This ensures Bar Dashboard shows only BAR tickets
-- =============================================

USE [RestaurantManagementDB] -- Change to your database name
GO

-- Drop and recreate GetBOTsByStatus with KitchenStation filter
IF OBJECT_ID('dbo.GetBOTsByStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetBOTsByStatus;
GO

CREATE PROCEDURE dbo.GetBOTsByStatus
    @Status INT = NULL,
    @KitchenStation VARCHAR(50) = 'BAR' -- Default to BAR station
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if KitchenStation column exists
    DECLARE @HasKitchenStationColumn BIT = 0;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BOT_Header') AND name = 'KitchenStation')
        SET @HasKitchenStationColumn = 1;
    
    IF @HasKitchenStationColumn = 1
    BEGIN
        -- Filter by both Status and KitchenStation
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
          AND (@KitchenStation IS NULL OR bh.KitchenStation = @KitchenStation)
        ORDER BY bh.CreatedAt DESC;
    END
    ELSE
    BEGIN
        -- Fallback: filter only by Status (for backward compatibility)
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
END
GO

PRINT 'GetBOTsByStatus stored procedure updated with KitchenStation filter';
GO
