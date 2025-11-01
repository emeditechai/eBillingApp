-- =============================================
-- Update GetBOTDashboardStats stored procedure to filter by KitchenStation
-- This ensures Bar Dashboard stats show only BAR tickets
-- =============================================

USE [RestaurantManagementDB] -- Change to your database name
GO

-- Drop and recreate GetBOTDashboardStats with KitchenStation filter
IF OBJECT_ID('dbo.GetBOTDashboardStats', 'P') IS NOT NULL
    DROP PROCEDURE dbo.GetBOTDashboardStats;
GO

CREATE PROCEDURE dbo.GetBOTDashboardStats
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
        -- Filter by KitchenStation
        SELECT 
            SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS NewBOTsCount,
            SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS InProgressBOTsCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS ReadyBOTsCount,
            SUM(CASE WHEN Status = 3 AND CAST(BilledAt AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS BilledTodayCount,
            COUNT(CASE WHEN Status < 4 THEN 1 END) AS TotalActiveBOTs,
            AVG(CASE WHEN Status = 2 THEN DATEDIFF(MINUTE, CreatedAt, ServedAt) END) AS AvgPrepTimeMinutes
        FROM dbo.BOT_Header
        WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
          AND (@KitchenStation IS NULL OR KitchenStation = @KitchenStation);
    END
    ELSE
    BEGIN
        -- Fallback: no KitchenStation filter (for backward compatibility)
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
END
GO

PRINT 'GetBOTDashboardStats stored procedure updated with KitchenStation filter';
GO
