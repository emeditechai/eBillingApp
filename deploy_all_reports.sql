-- =====================================================================
-- COMPLETE REPORTS DEPLOYMENT SCRIPT
-- Includes: GST Breakup, Collection Register, Discount Report, Sales Report
-- Date: 2025-11-08
-- =====================================================================

PRINT '========================================';
PRINT 'RESTAURANT REPORTS - COMPLETE DEPLOYMENT';
PRINT '========================================';
PRINT '';

-- =====================================================================
-- 1. SALES REPORT with Order Listing
-- =====================================================================

PRINT 'Deploying Sales Report Stored Procedure...';
PRINT '';

IF OBJECT_ID('usp_GetSalesReport', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetSalesReport
GO

CREATE PROCEDURE usp_GetSalesReport
    @StartDate DATE = NULL,
    @EndDate DATE = NULL,
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Set default date range if not provided
    SET @StartDate = ISNULL(@StartDate, CAST(DATEADD(DAY, -30, GETDATE()) AS DATE));
    SET @EndDate = ISNULL(@EndDate, CAST(GETDATE() AS DATE));
    
    DECLARE @StartDateTime DATETIME = CAST(@StartDate AS DATETIME);
    DECLARE @EndDateTime DATETIME = DATEADD(DAY, 1, CAST(@EndDate AS DATETIME));
    
    -- Result Set 1: Summary Statistics
    SELECT 
        TotalOrders = COUNT(*),
        TotalSales = ISNULL(SUM(TotalAmount), 0),
        AverageOrderValue = CASE WHEN COUNT(*) > 0 THEN ISNULL(SUM(TotalAmount), 0) / COUNT(*) ELSE 0 END,
        TotalSubtotal = ISNULL(SUM(Subtotal), 0),
        TotalTax = ISNULL(SUM(TaxAmount), 0),
        TotalTips = ISNULL(SUM(TipAmount), 0),
        TotalDiscounts = ISNULL(SUM(DiscountAmount), 0),
        CompletedOrders = SUM(CASE WHEN Status IN (1, 3) THEN 1 ELSE 0 END),
        CancelledOrders = SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)
    FROM Orders WITH (NOLOCK)
    WHERE CreatedAt >= @StartDateTime 
      AND CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR UserId = @UserId);
    
    -- Result Set 2: Daily Sales Trend
    SELECT 
        SalesDate = CAST(CreatedAt AS DATE),
        OrderCount = COUNT(*),
        DailySales = ISNULL(SUM(TotalAmount), 0),
        AvgOrderValue = CASE WHEN COUNT(*) > 0 THEN ISNULL(SUM(TotalAmount), 0) / COUNT(*) ELSE 0 END
    FROM Orders WITH (NOLOCK)
    WHERE CreatedAt >= @StartDateTime 
      AND CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR UserId = @UserId)
    GROUP BY CAST(CreatedAt AS DATE)
    ORDER BY SalesDate DESC;
    
    -- Result Set 3: Top Menu Items
    SELECT TOP 10
        mi.Id AS MenuItemId,
        ItemName = mi.Name,
        TotalQuantitySold = SUM(oi.Quantity),
        TotalRevenue = SUM(oi.Quantity * oi.UnitPrice),
        AveragePrice = AVG(oi.UnitPrice),
        OrderCount = COUNT(DISTINCT oi.OrderId)
    FROM OrderItems oi WITH (NOLOCK)
    INNER JOIN MenuItems mi WITH (NOLOCK) ON mi.Id = oi.MenuItemId
    INNER JOIN Orders o WITH (NOLOCK) ON o.Id = oi.OrderId
    WHERE o.CreatedAt >= @StartDateTime 
      AND o.CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR o.UserId = @UserId)
    GROUP BY mi.Id, mi.Name
    ORDER BY TotalRevenue DESC;
    
    -- Result Set 4: Server Performance
    SELECT 
        ServerName = ISNULL(u.FirstName + ' ' + u.LastName, u.Username),
        Username = u.Username,
        UserId = u.Id,
        OrderCount = COUNT(o.Id),
        TotalSales = ISNULL(SUM(o.TotalAmount), 0),
        AvgOrderValue = CASE WHEN COUNT(o.Id) > 0 THEN ISNULL(SUM(o.TotalAmount), 0) / COUNT(o.Id) ELSE 0 END,
        TotalTips = ISNULL(SUM(o.TipAmount), 0)
    FROM Users u WITH (NOLOCK)
    INNER JOIN Orders o WITH (NOLOCK) ON o.UserId = u.Id
    WHERE o.CreatedAt >= @StartDateTime 
      AND o.CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR u.Id = @UserId)
    GROUP BY u.Id, u.Username, u.FirstName, u.LastName
    ORDER BY TotalSales DESC;
    
    -- Result Set 5: Order Status Distribution
    SELECT 
        OrderStatus = CASE 
            WHEN Status = 0 THEN 'Pending'
            WHEN Status = 1 THEN 'Paid'
            WHEN Status = 2 THEN 'Cancelled'
            WHEN Status = 3 THEN 'Completed'
            WHEN Status = 4 THEN 'Refunded'
            ELSE 'Unknown'
        END,
        OrderCount = COUNT(*),
        TotalAmount = ISNULL(SUM(TotalAmount), 0),
        Percentage = CASE 
            WHEN (SELECT COUNT(*) FROM Orders WITH (NOLOCK) 
                  WHERE CreatedAt >= @StartDateTime 
                    AND CreatedAt < @EndDateTime
                    AND (@UserId IS NULL OR UserId = @UserId)) > 0
            THEN CAST(COUNT(*) * 100.0 / (
                SELECT COUNT(*) FROM Orders WITH (NOLOCK) 
                WHERE CreatedAt >= @StartDateTime 
                  AND CreatedAt < @EndDateTime
                  AND (@UserId IS NULL OR UserId = @UserId)
            ) AS DECIMAL(5,2))
            ELSE 0
        END
    FROM Orders WITH (NOLOCK)
    WHERE CreatedAt >= @StartDateTime 
      AND CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR UserId = @UserId)
    GROUP BY Status
    ORDER BY OrderCount DESC;
    
    -- Result Set 6: Hourly Sales Pattern
    SELECT 
        HourOfDay = DATEPART(HOUR, CreatedAt),
        OrderCount = COUNT(*),
        HourlySales = ISNULL(SUM(TotalAmount), 0),
        AvgOrderValue = CASE WHEN COUNT(*) > 0 THEN ISNULL(SUM(TotalAmount), 0) / COUNT(*) ELSE 0 END
    FROM Orders WITH (NOLOCK)
    WHERE CreatedAt >= @StartDateTime 
      AND CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR UserId = @UserId)
    GROUP BY DATEPART(HOUR, CreatedAt)
    ORDER BY HourOfDay;
    
    -- Result Set 7: Order Listing (NEW)
    SELECT 
        o.Id AS OrderId,
        o.OrderNumber,
        o.CreatedAt,
        BillValue = ISNULL(o.Subtotal, 0),
        DiscountAmount = ISNULL(o.DiscountAmount, 0),
        NetAmount = ISNULL(o.Subtotal, 0) - ISNULL(o.DiscountAmount, 0),
        TaxAmount = ISNULL(o.TaxAmount, 0),
        TipAmount = ISNULL(o.TipAmount, 0),
        TotalAmount = ISNULL(o.TotalAmount, 0),
        Status = o.Status,
        StatusText = CASE 
            WHEN o.Status = 0 THEN 'Pending'
            WHEN o.Status = 1 THEN 'Paid'
            WHEN o.Status = 2 THEN 'Cancelled'
            WHEN o.Status = 3 THEN 'Completed'
            WHEN o.Status = 4 THEN 'Refunded'
            ELSE 'Unknown'
        END,
        ServerName = ISNULL(u.FirstName + ' ' + u.LastName, u.Username)
    FROM Orders o WITH (NOLOCK)
    LEFT JOIN Users u WITH (NOLOCK) ON u.Id = o.UserId
    WHERE o.CreatedAt >= @StartDateTime 
      AND o.CreatedAt < @EndDateTime
      AND (@UserId IS NULL OR o.UserId = @UserId)
    ORDER BY o.CreatedAt DESC;
END
GO

PRINT '✓ Sales Report stored procedure deployed successfully';
PRINT '  - 7 result sets including new Order Listing';
PRINT '';

-- =====================================================================
-- 2. DISCOUNT REPORT with Status Text
-- =====================================================================

PRINT 'Deploying Discount Report Stored Procedure...';
PRINT '';

IF OBJECT_ID('usp_GetDiscountReport', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetDiscountReport
GO

CREATE PROCEDURE usp_GetDiscountReport
    @StartDate DATE = NULL,
    @EndDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @StartDate = ISNULL(@StartDate, CAST(GETDATE() AS DATE));
    SET @EndDate = ISNULL(@EndDate, CAST(GETDATE() AS DATE));

    DECLARE @StartDateTime DATETIME = CAST(@StartDate AS DATETIME);
    DECLARE @EndDateTime DATETIME = DATEADD(DAY, 1, CAST(@EndDate AS DATETIME));

    /* Summary Metrics */
    ;WITH Discounted AS (
        SELECT o.Id, o.OrderNumber, o.CreatedAt, o.DiscountAmount, o.Subtotal, o.TaxAmount, o.TipAmount, o.TotalAmount, o.Status, o.UserId
        FROM Orders o WITH (NOLOCK)
        WHERE o.CreatedAt >= @StartDateTime AND o.CreatedAt < @EndDateTime
          AND o.DiscountAmount > 0
    )
    SELECT 
        TotalDiscountedOrders = COUNT(*),
        TotalDiscountAmount = ISNULL(SUM(DiscountAmount),0),
        AvgDiscountPerOrder = CASE WHEN COUNT(*)>0 THEN AVG(DiscountAmount) ELSE 0 END,
        MaxDiscount = ISNULL(MAX(DiscountAmount),0),
        MinDiscount = ISNULL(MIN(DiscountAmount),0),
        TotalGrossBeforeDiscount = ISNULL(SUM(Subtotal + TaxAmount + TipAmount),0),
        NetAfterDiscount = ISNULL(SUM(TotalAmount),0)
    FROM Discounted;

    /* Detailed Rows */
    SELECT 
        o.Id AS OrderId,
        o.OrderNumber,
        o.CreatedAt,
        o.DiscountAmount,
        o.Subtotal,
        o.TaxAmount,
        o.TipAmount,
        o.TotalAmount,
        (o.Subtotal + o.TaxAmount + o.TipAmount) AS GrossAmount,
        (o.Subtotal + o.TaxAmount + o.TipAmount) - o.TotalAmount AS DiscountApplied,
        u.Username,
        u.FirstName,
        u.LastName,
        o.Status,
        CASE 
            WHEN o.Status = 0 THEN 'Pending'
            WHEN o.Status = 1 THEN 'Paid'
            WHEN o.Status = 2 THEN 'Cancelled'
            WHEN o.Status = 3 THEN 'Completed'
            WHEN o.Status = 4 THEN 'Refunded'
            ELSE 'Unknown'
        END AS StatusText
    FROM Orders o WITH (NOLOCK)
    LEFT JOIN Users u WITH (NOLOCK) ON u.Id = o.UserId
    WHERE o.CreatedAt >= @StartDateTime AND o.CreatedAt < @EndDateTime
      AND o.DiscountAmount > 0
    ORDER BY o.CreatedAt DESC;
END
GO

PRINT '✓ Discount Report stored procedure deployed successfully';
PRINT '  - StatusText column added for proper status display';
PRINT '';

-- =====================================================================
-- DEPLOYMENT COMPLETE
-- =====================================================================

PRINT '========================================';
PRINT 'DEPLOYMENT COMPLETED SUCCESSFULLY!';
PRINT '========================================';
PRINT '';
PRINT 'Reports Updated:';
PRINT '  1. ✅ Sales Report - Added Order Listing section';
PRINT '  2. ✅ Discount Report - Fixed status display';
PRINT '';
PRINT 'Next Steps:';
PRINT '  - Restart application';
PRINT '  - Test Sales Report: https://localhost:7290/Reports/Sales';
PRINT '  - Test Discount Report: https://localhost:7290/Reports/DiscountReport';
PRINT '';
