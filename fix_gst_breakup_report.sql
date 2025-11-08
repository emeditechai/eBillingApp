-- Updated GST Breakup Report Stored Procedure
-- Fixes taxable value calculation to use Orders.Subtotal - Orders.DiscountAmount
-- Adds Indian GST compliance features
-- Date: 2025-11-08

IF OBJECT_ID('dbo.usp_GetGSTBreakupReport', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetGSTBreakupReport;
GO

CREATE PROCEDURE dbo.usp_GetGSTBreakupReport
    @StartDate DATE = NULL,
    @EndDate   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Normalize dates: if only one provided treat both as same day
    IF @StartDate IS NULL AND @EndDate IS NULL
    BEGIN
        SET @StartDate = CAST(GETDATE() AS DATE);
        SET @EndDate = @StartDate;
    END
    ELSE IF @StartDate IS NULL SET @StartDate = @EndDate;
    ELSE IF @EndDate IS NULL SET @EndDate = @StartDate;

    -- Aggregate payments by order to handle split payments correctly
    -- Multiple payment rows per order should be treated as one invoice
    -- CRITICAL FIX: Use Orders.Subtotal and Orders.DiscountAmount for accurate taxable value
    IF OBJECT_ID('tempdb..#OrderGST') IS NOT NULL DROP TABLE #OrderGST;
    
    SELECT 
        o.Id AS OrderId,
        o.OrderNumber,
        MIN(p.CreatedAt) AS PaymentDate,
        
        -- CORRECTED: Taxable Value = Order Subtotal - Order Discount
        -- This is the base amount on which GST is calculated
        ISNULL(o.Subtotal, 0) - ISNULL(o.DiscountAmount, 0) AS TaxableValue,
        
        -- Discount from order (may be split across multiple payments)
        ISNULL(o.DiscountAmount, 0) AS DiscountAmount,
        
        -- GST percentages from persisted order data (BAR = 20%, Foods = 10%)
        ISNULL(o.GSTPercentage, 
            ISNULL((SELECT MAX(p2.GST_Perc) FROM Payments p2 WHERE p2.OrderId = o.Id AND p2.Status = 1), 5.0)
        ) AS GSTPercentage,
        
        ISNULL(o.CGSTPercentage, 
            ISNULL((SELECT MAX(p2.CGST_Perc) FROM Payments p2 WHERE p2.OrderId = o.Id AND p2.Status = 1), 2.5)
        ) AS CGSTPerc,
        
        ISNULL(o.SGSTPercentage, 
            ISNULL((SELECT MAX(p2.SGST_Perc) FROM Payments p2 WHERE p2.OrderId = o.Id AND p2.Status = 1), 2.5)
        ) AS SGSTPerc,
        
        -- GST amounts from order (more accurate than summing payment GST which may have rounding)
        ISNULL(o.GSTAmount, SUM(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0))) AS TotalGST,
        ISNULL(o.CGSTAmount, SUM(ISNULL(p.CGSTAmount, 0))) AS CGSTAmount,
        ISNULL(o.SGSTAmount, SUM(ISNULL(p.SGSTAmount, 0))) AS SGSTAmount,
        
        -- Invoice Total = Taxable Value + Total GST
        -- Or use order's TotalAmount (Subtotal - Discount + GST + Tip)
        ISNULL(o.TotalAmount, 
            (ISNULL(o.Subtotal, 0) - ISNULL(o.DiscountAmount, 0) + 
             ISNULL(o.GSTAmount, SUM(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0))))
        ) AS InvoiceTotal,
        
        -- Indian GST Compliance Fields
        ISNULL(o.OrderKitchenType, 'Foods') AS OrderType, -- BAR or Foods
        -- Get table number through TableTurnovers → Tables relationship
        ISNULL(t.TableName, COALESCE(t.TableNumber, 'N/A')) AS TableNumber,
        o.CreatedAt AS OrderCreatedAt
        
    INTO #OrderGST
    FROM Orders o
    INNER JOIN Payments p ON o.Id = p.OrderId
    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
    LEFT JOIN Tables t ON tt.TableId = t.Id
    WHERE CAST(p.CreatedAt AS DATE) BETWEEN @StartDate AND @EndDate
      AND p.Status = 1 -- Only approved/completed payments
    GROUP BY 
        o.Id, 
        o.OrderNumber, 
        o.Subtotal, 
        o.DiscountAmount,
        o.GSTPercentage,
        o.CGSTPercentage,
        o.SGSTPercentage,
        o.GSTAmount,
        o.CGSTAmount,
        o.SGSTAmount,
        o.TotalAmount,
        o.OrderKitchenType,
        t.TableName,
        t.TableNumber,
        o.CreatedAt;

    -- Summary: Aggregated totals for the period (first result set)
    SELECT 
        COUNT(*) AS InvoiceCount,
        SUM(TaxableValue) AS TotalTaxableValue,
        SUM(DiscountAmount) AS TotalDiscount,
        SUM(CGSTAmount) AS TotalCGST,
        SUM(SGSTAmount) AS TotalSGST,
        SUM(TotalGST) AS TotalGST,
        SUM(InvoiceTotal) AS NetAmount,
        CASE WHEN COUNT(*) > 0 THEN SUM(TaxableValue) / COUNT(*) ELSE 0 END AS AverageTaxablePerInvoice,
        CASE WHEN COUNT(*) > 0 THEN SUM(TotalGST) / COUNT(*) ELSE 0 END AS AverageGSTPerInvoice
    FROM #OrderGST;

    -- Detail rows: One row per order/invoice (second result set)
    SELECT 
        PaymentDate,
        OrderNumber,
        TaxableValue,
        DiscountAmount,
        GSTPercentage,
        CGSTPerc AS CGSTPercentage,
        CGSTAmount,
        SGSTPerc AS SGSTPercentage,
        SGSTAmount,
        TotalGST,
        InvoiceTotal,
        OrderType,
        TableNumber
    FROM #OrderGST
    ORDER BY PaymentDate ASC, OrderNumber ASC;
    
    DROP TABLE #OrderGST;
END
GO

PRINT 'GST Breakup Report stored procedure updated successfully.';
PRINT 'Taxable Value now correctly calculated as Orders.Subtotal - Orders.DiscountAmount';
PRINT 'GST percentages now use persisted Orders.GSTPercentage (BAR=20%, Foods=10%)';
GO
