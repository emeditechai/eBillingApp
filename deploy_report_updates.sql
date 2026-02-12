-- =====================================================================
-- Deploy Report Updates Script
-- Date: 2025-11-08
-- Description: Updates GST Breakup and Collection Register reports
-- =====================================================================

PRINT '========================================';
PRINT 'Starting Report Updates Deployment';
PRINT 'Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
PRINT '';

-- =====================================================================
-- 1. UPDATE GST BREAKUP REPORT
-- =====================================================================
PRINT '1. Updating GST Breakup Report Stored Procedure...';
PRINT '';

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

PRINT '✓ GST Breakup Report stored procedure updated successfully';
PRINT '  - Taxable Value = Orders.Subtotal - Orders.DiscountAmount';
PRINT '  - GST percentages use persisted Orders.GSTPercentage (BAR=20%, Foods=10%)';
PRINT '  - Table number retrieved via TableTurnovers → Tables joins';
PRINT '';

-- =====================================================================
-- 2. UPDATE COLLECTION REGISTER REPORT
-- =====================================================================
PRINT '2. Updating Collection Register Stored Procedure...';
PRINT '';

IF OBJECT_ID('dbo.usp_GetCollectionRegister', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetCollectionRegister;
GO

CREATE PROCEDURE dbo.usp_GetCollectionRegister
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @PaymentMethodId INT = NULL,  -- NULL means ALL payment methods
    @UserId INT = NULL,           -- NULL means all users
    @CounterId INT = NULL         -- NULL means all counters (ignored if Orders counter column is missing)
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to today if no dates provided
    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    -- Ensure FromDate <= ToDate
    IF @FromDate > @ToDate
    BEGIN
        DECLARE @Temp DATE = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @Temp;
    END;

    -- Detect Orders counter column (supports common variants)
    DECLARE @CounterColumn SYSNAME = NULL;
    IF COL_LENGTH('dbo.Orders', 'CounterId') IS NOT NULL
        SET @CounterColumn = 'CounterId';
    ELSE IF COL_LENGTH('dbo.Orders', 'CounterID') IS NOT NULL
        SET @CounterColumn = 'CounterID';

    DECLARE @HasCountersTable BIT = 0;
    IF OBJECT_ID('dbo.Counters', 'U') IS NOT NULL
        SET @HasCountersTable = 1;

    DECLARE @CounterSelect NVARCHAR(MAX) = N'CAST(NULL AS INT) AS CounterId, CAST('''' AS NVARCHAR(200)) AS CounterName,';
    DECLARE @CounterJoin NVARCHAR(MAX) = N'';
    IF @CounterColumn IS NOT NULL
    BEGIN
        SET @CounterSelect = N'o.' + QUOTENAME(@CounterColumn) + N' AS CounterId, ';
        IF @HasCountersTable = 1
        BEGIN
            SET @CounterSelect += N'NULLIF(LTRIM(RTRIM(COALESCE(NULLIF(c.CounterCode, '''') + '' - '' + NULLIF(c.CounterName, ''''), NULLIF(c.CounterName, ''''), NULLIF(c.CounterCode, ''''), ''''))), '''') AS CounterName,';
            SET @CounterJoin = N'LEFT JOIN dbo.Counters c WITH (NOLOCK) ON c.Id = o.' + QUOTENAME(@CounterColumn) + CHAR(10);
        END
        ELSE
        BEGIN
            SET @CounterSelect += N'CAST('''' AS NVARCHAR(200)) AS CounterName,';
        END
    END

    DECLARE @Sql NVARCHAR(MAX) = N'
    ;WITH FilteredPayments AS (
        SELECT 
            p.Id,
            p.OrderId,
            p.PaymentMethodId,
            p.Amount,
            p.TipAmount,
            p.DiscAmount,
            p.CGSTAmount,
            p.SGSTAmount,
            p.RoundoffAdjustmentAmt,
            p.ProcessedByName,
            p.LastFourDigits,
            p.CardType,
            p.ReferenceNumber,
            p.CreatedAt,
            p.Status
        FROM Payments p WITH (NOLOCK)
        WHERE p.CreatedAt >= @FromDate 
            AND p.CreatedAt < DATEADD(DAY, 1, @ToDate)
            AND p.Status IN (1, 3)
            AND (@PaymentMethodId IS NULL OR p.PaymentMethodId = @PaymentMethodId)
            AND (@UserId IS NULL OR p.ProcessedBy = @UserId)
    )
    SELECT 
        o.OrderNumber AS OrderNo,
        ISNULL(t.TableName, ''N/A'') AS TableNo,
        ISNULL(p.ProcessedByName, ''System'') AS Username,
        ' + @CounterSelect + N'
        CASE WHEN p.Status = 3 THEN -(ISNULL(o.Subtotal, 0) - ISNULL(p.DiscAmount, 0)) 
             ELSE ISNULL(o.Subtotal, 0) - ISNULL(p.DiscAmount, 0) 
        END AS ActualBillAmount,
        CASE WHEN p.Status = 3 THEN -ISNULL(p.DiscAmount, 0) 
             ELSE ISNULL(p.DiscAmount, 0) 
        END AS DiscountAmount,
        CASE WHEN p.Status = 3 THEN -(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0))
             ELSE ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0)
        END AS GSTAmount,
        CASE WHEN p.Status = 3 THEN -ISNULL(p.RoundoffAdjustmentAmt, 0)
             ELSE ISNULL(p.RoundoffAdjustmentAmt, 0)
        END AS RoundOffAmount,
        CASE WHEN p.Status = 3 THEN -(p.Amount + ISNULL(p.TipAmount, 0) + ISNULL(p.RoundoffAdjustmentAmt, 0))
             ELSE p.Amount + ISNULL(p.TipAmount, 0) + ISNULL(p.RoundoffAdjustmentAmt, 0)
        END AS ReceiptAmount,
        CASE WHEN p.Status = 3 THEN pm.Name + '' (REFUND)''
             ELSE pm.Name
        END AS PaymentMethod,
        CASE WHEN p.Status = 3 THEN ''🔴 REFUND - Payment voided'' ELSE '''' END +
        STUFF(
            CASE WHEN p.DiscAmount > 0 THEN '' | Discount: ₹'' + CAST(p.DiscAmount AS VARCHAR(20)) ELSE '''' END +
            CASE WHEN ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) > 0 
                 THEN '' | GST: ₹'' + CAST(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) AS VARCHAR(20)) ELSE '''' END +
            CASE WHEN ISNULL(p.LastFourDigits, '''') <> '''' 
                 THEN '' | Card: '' + p.CardType + '' *'' + p.LastFourDigits ELSE '''' END +
            CASE WHEN ISNULL(p.ReferenceNumber, '''') <> '''' 
                 THEN '' | Ref: '' + p.ReferenceNumber ELSE '''' END +
            CASE WHEN ISNULL(p.TipAmount, 0) > 0 
                 THEN '' | Tip: ₹'' + CAST(p.TipAmount AS VARCHAR(20)) ELSE '''' END,
            1, 3, ''''
        ) AS Details,
        p.CreatedAt AS PaymentDate,
        p.Status AS PaymentStatus
    FROM FilteredPayments p
    INNER JOIN Orders o WITH (NOLOCK) ON p.OrderId = o.Id
    ' + @CounterJoin + N'
    INNER JOIN PaymentMethods pm WITH (NOLOCK) ON p.PaymentMethodId = pm.Id
    LEFT JOIN (
        SELECT OrderId, MIN(TableId) AS TableId
        FROM OrderTables WITH (NOLOCK)
        GROUP BY OrderId
    ) ot ON o.Id = ot.OrderId
    LEFT JOIN Tables t WITH (NOLOCK) ON ot.TableId = t.Id
    ';

    IF @CounterId IS NOT NULL AND @CounterColumn IS NOT NULL
        SET @Sql += N'WHERE o.' + QUOTENAME(@CounterColumn) + N' = @CounterId ' + CHAR(10);

    SET @Sql += N'ORDER BY p.CreatedAt DESC, o.OrderNumber;';

    EXEC sp_executesql
        @Sql,
        N'@FromDate DATE, @ToDate DATE, @PaymentMethodId INT, @UserId INT, @CounterId INT',
        @FromDate = @FromDate,
        @ToDate = @ToDate,
        @PaymentMethodId = @PaymentMethodId,
        @UserId = @UserId,
        @CounterId = @CounterId;
END
GO

PRINT '✓ Collection Register stored procedure updated successfully';
PRINT '  - Actual Bill Amount = Orders.Subtotal - Discount';
PRINT '  - GST Amount column added (CGST + SGST)';
PRINT '  - Details field includes GST information';
PRINT '';

-- =====================================================================
-- DEPLOYMENT SUMMARY
-- =====================================================================
PRINT '========================================';
PRINT 'Deployment Summary';
PRINT '========================================';
PRINT '✓ GST Breakup Report - Updated';
PRINT '✓ Collection Register Report - Updated';
PRINT '';
PRINT 'Key Changes:';
PRINT '1. Taxable Value/Actual Bill = Subtotal - Discount (consistent formula)';
PRINT '2. GST Amount displayed in Collection Register';
PRINT '3. GST Breakup uses persisted GST percentages from Orders';
PRINT '4. Both reports show accurate tax compliance data';
PRINT '';
PRINT 'Deployment completed successfully!';
PRINT 'Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
