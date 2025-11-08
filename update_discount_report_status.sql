-- =====================================================================
-- Updated Discount Report Stored Procedure
-- Adds StatusText column to show proper status names instead of numbers
-- Date: 2025-11-08
-- =====================================================================

PRINT 'Updating Discount Report Stored Procedure...';
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
    DECLARE @EndDateTime DATETIME = DATEADD(DAY, 1, CAST(@EndDate AS DATETIME)); -- exclusive

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

PRINT '✓ Discount Report stored procedure updated successfully';
PRINT '  - StatusText column added (Pending/Paid/Cancelled/Completed/Refunded)';
PRINT '  - Status badges will now show proper text instead of numbers';
PRINT '';
PRINT 'Deployment completed successfully!';
PRINT '========================================';
