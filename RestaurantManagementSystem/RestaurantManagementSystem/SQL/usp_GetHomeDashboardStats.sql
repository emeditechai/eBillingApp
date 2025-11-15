-- Stored procedure to get Home Dashboard statistics
-- This procedure returns Today's Sales, Today's Orders, Active Tables, and Upcoming Reservations

IF OBJECT_ID('usp_GetHomeDashboardStats', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetHomeDashboardStats
GO

CREATE PROCEDURE usp_GetHomeDashboardStats
  @UserId INT = NULL,
  @CanViewAll BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TodaySales DECIMAL(18,2) = 0
    DECLARE @TodayOrders INT = 0
    DECLARE @ActiveTables INT = 0
    DECLARE @UpcomingReservations INT = 0
    
    -- Get today's sales (sum of TotalAmount from orders created today)
    -- Use order TotalAmount instead of payments for more reliable calculation
    SELECT @TodaySales = ISNULL(SUM(o.TotalAmount), 0)
    FROM Orders o
    WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
      AND o.TotalAmount > 0
      AND (
            @CanViewAll = 1 
            OR (@UserId IS NOT NULL AND o.UserId = @UserId)
          ); -- Only include orders with actual amounts
    
    -- Get today's orders count
    SELECT @TodayOrders = COUNT(*)
    FROM Orders o
    WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
      AND (
            @CanViewAll = 1 
            OR (@UserId IS NOT NULL AND o.UserId = @UserId)
          );
    
    -- Get active tables (tables that are currently reserved or occupied)
    SELECT @ActiveTables = COUNT(*)
    FROM Tables t
    WHERE t.IsActive = 1 
      AND t.Status IN (1, 2); -- Reserved or Occupied
    
    -- Get upcoming reservations (confirmed reservations for today and next 7 days)
    SELECT @UpcomingReservations = COUNT(*)
    FROM Reservations r
    WHERE r.Status = 1 -- Confirmed
      AND r.ReservationDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
      AND r.ReservationTime > GETDATE(); -- Future reservations only
    
    -- Return all stats as a single result set
    SELECT 
        @TodaySales as TodaySales,
        @TodayOrders as TodayOrders,
        @ActiveTables as ActiveTables,
        @UpcomingReservations as UpcomingReservations;
        
END
GO