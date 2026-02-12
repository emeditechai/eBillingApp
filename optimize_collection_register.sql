-- Optimize Collection Register Stored Procedure for Performance
-- This script updates the usp_GetCollectionRegister procedure with performance improvements

USE [RestaurantManagement]
GO

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

PRINT 'Collection Register stored procedure optimized successfully.';
PRINT 'Performance improvements:';
PRINT '  - Added CTE with filtered data to reduce processing';
PRINT '  - Changed date filter to use CreatedAt >= and < DATEADD for better index usage';
PRINT '  - Added NOLOCK hints to reduce locking overhead';
PRINT '  - Optimized OrderTables join with subquery to get first table';
PRINT '  - Simplified Details column building with STUFF function';
GO

-- Create recommended indexes if they don't exist
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_CreatedAt_Status_Includes' AND object_id = OBJECT_ID('Payments'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Payments_CreatedAt_Status_Includes
    ON Payments(CreatedAt, Status)
    INCLUDE (OrderId, PaymentMethodId, Amount, TipAmount, DiscAmount, CGSTAmount, SGSTAmount, 
             RoundoffAdjustmentAmt, ProcessedByName, ProcessedBy, LastFourDigits, CardType, ReferenceNumber);
    PRINT 'Created index IX_Payments_CreatedAt_Status_Includes on Payments table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrderTables_OrderId' AND object_id = OBJECT_ID('OrderTables'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrderTables_OrderId
    ON OrderTables(OrderId)
    INCLUDE (TableId);
    PRINT 'Created index IX_OrderTables_OrderId on OrderTables table';
END
GO

PRINT 'Collection Register optimization complete!';
