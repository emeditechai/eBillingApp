-- Enhanced stored procedure with better performance and additional features
-- This version includes caching hints and better error handling

IF OBJECT_ID('usp_GetHomeDashboardStatsEnhanced', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetHomeDashboardStatsEnhanced
GO

CREATE PROCEDURE usp_GetHomeDashboardStatsEnhanced
  @UserId INT = NULL,
  @CanViewAll BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        DECLARE @TodaySales DECIMAL(18,2) = 0
        DECLARE @TodayOrders INT = 0
        DECLARE @ActiveTables INT = 0
        DECLARE @UpcomingReservations INT = 0
        DECLARE @TodayStart DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME)
        DECLARE @TodayEnd DATETIME = DATEADD(DAY, 1, @TodayStart)
        
        -- Get today's sales with better date filtering
        SELECT @TodaySales = ISNULL(SUM(p.Amount + p.TipAmount), 0)
        FROM Payments p WITH (NOLOCK)
        INNER JOIN Orders o WITH (NOLOCK) ON p.OrderId = o.Id
        WHERE o.CreatedAt >= @TodayStart 
          AND o.CreatedAt < @TodayEnd
          AND p.Status = 1
          AND (
                @CanViewAll = 1 
                OR (@UserId IS NOT NULL AND o.UserId = @UserId)
              ); -- Approved payments only scoped per user unless privileged
        
        -- Get today's orders count
        SELECT @TodayOrders = COUNT(*)
        FROM Orders o WITH (NOLOCK)
        WHERE o.CreatedAt >= @TodayStart 
          AND o.CreatedAt < @TodayEnd
          AND (
                @CanViewAll = 1 
                OR (@UserId IS NOT NULL AND o.UserId = @UserId)
              );
        
        -- Get active tables (reserved or occupied)
        SELECT @ActiveTables = COUNT(*)
        FROM Tables t WITH (NOLOCK)
        WHERE t.IsActive = 1 
          AND t.Status IN (1, 2); -- Reserved or Occupied
        
        -- Get upcoming reservations (next 7 days, confirmed)
        SELECT @UpcomingReservations = COUNT(*)
        FROM Reservations r WITH (NOLOCK)
        WHERE r.Status = 1 -- Confirmed
          AND r.ReservationDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
          AND r.ReservationTime > GETDATE();
        
        -- Return results with additional metadata
        SELECT 
            @TodaySales as TodaySales,
            @TodayOrders as TodayOrders,
            @ActiveTables as ActiveTables,
            @UpcomingReservations as UpcomingReservations,
            GETDATE() as LastUpdated,
            'SUCCESS' as Status;
            
    END TRY
    BEGIN CATCH
        -- Return error information
        SELECT 
            0.00 as TodaySales,
            0 as TodayOrders,
            0 as ActiveTables,
            0 as UpcomingReservations,
            GETDATE() as LastUpdated,
            ERROR_MESSAGE() as Status;
    END CATCH
END
GO