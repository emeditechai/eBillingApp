-- =============================================
-- Create Financial Summary Report Stored Procedure
-- =============================================
USE dev_Restaurant;
GO

IF OBJECT_ID('usp_GetFinancialSummary', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetFinancialSummary;
GO

CREATE PROCEDURE usp_GetFinancialSummary
    @StartDate DATETIME = NULL,
    @EndDate DATETIME = NULL,
    @ComparisonPeriodDays INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to last 30 days if no dates provided
    IF @StartDate IS NULL
        SET @StartDate = DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
    
    IF @EndDate IS NULL
        SET @EndDate = CAST(GETDATE() AS DATE);

    -- Ensure start of day for StartDate and end of day for EndDate
    SET @StartDate = CAST(@StartDate AS DATE);
    SET @EndDate = CAST(DATEADD(DAY, 1, CAST(@EndDate AS DATE)) AS DATETIME);
    SET @EndDate = DATEADD(SECOND, -1, @EndDate);

    -- Calculate comparison period dates
    DECLARE @CompStartDate DATETIME = DATEADD(DAY, -@ComparisonPeriodDays, @StartDate);
    DECLARE @CompEndDate DATETIME = DATEADD(DAY, -@ComparisonPeriodDays, @EndDate);

    -- Result Set 1: Summary Statistics
    SELECT
        -- Current Period Metrics
        COUNT(DISTINCT o.Id) AS TotalOrders,
        ISNULL(SUM(o.TotalAmount), 0) AS TotalRevenue,
        ISNULL(SUM(o.Subtotal), 0) AS SubTotal,
        ISNULL(SUM(o.TaxAmount), 0) AS TotalTax,
        ISNULL(SUM(o.TipAmount), 0) AS TotalTips,
        ISNULL(SUM(o.DiscountAmount), 0) AS TotalDiscounts,
        ISNULL(AVG(o.TotalAmount), 0) AS AverageOrderValue,
        ISNULL(SUM(CASE WHEN o.Status = 3 THEN o.TotalAmount ELSE 0 END), 0) AS PaidAmount,
        ISNULL(SUM(CASE WHEN o.Status != 3 THEN o.TotalAmount ELSE 0 END), 0) AS UnpaidAmount,
        
        -- Item and Category Metrics
        COUNT(DISTINCT oi.MenuItemId) AS UniqueItemsSold,
        ISNULL(SUM(oi.Quantity), 0) AS TotalQuantitySold,
        
        -- Payment Method Distribution
        ISNULL(SUM(CASE WHEN pm.Name = 'CASH' THEN p.Amount ELSE 0 END), 0) AS CashPayments,
        ISNULL(SUM(CASE WHEN pm.Name IN ('CREDIT_CARD', 'DEBIT_CARD') THEN p.Amount ELSE 0 END), 0) AS CardPayments,
        ISNULL(SUM(CASE WHEN pm.Name = 'UPI' THEN p.Amount ELSE 0 END), 0) AS UPIPayments,
        ISNULL(SUM(CASE WHEN pm.Name = 'NET_BANKING' THEN p.Amount ELSE 0 END), 0) AS NetBankingPayments,
        ISNULL(SUM(CASE WHEN pm.Name = 'COMPLIMENTARY' THEN p.Amount ELSE 0 END), 0) AS ComplimentaryPayments,
        ISNULL(SUM(CASE WHEN pm.Name NOT IN ('CASH', 'CREDIT_CARD', 'DEBIT_CARD', 'UPI', 'NET_BANKING', 'COMPLIMENTARY') THEN p.Amount ELSE 0 END), 0) AS OtherPayments,
        
        -- Profit Metrics (simplified - actual costs would need inventory/expense tracking)
        ISNULL(SUM(o.TotalAmount) - SUM(o.DiscountAmount), 0) AS NetRevenue,
        CASE 
            WHEN SUM(o.Subtotal) > 0 
            THEN (SUM(o.TotalAmount) - SUM(o.DiscountAmount)) / SUM(o.Subtotal) * 100 
            ELSE 0 
        END AS NetProfitMargin,
        
        -- Date range for reference
        @StartDate AS PeriodStartDate,
        @EndDate AS PeriodEndDate,
        DATEDIFF(DAY, @StartDate, @EndDate) + 1 AS TotalDays
    FROM Orders o
    LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
    LEFT JOIN Payments p ON o.Id = p.OrderId AND p.Status = 1
    LEFT JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3);

    -- Result Set 2: Payment Method Breakdown
    SELECT
        pm.Name AS PaymentMethod,
        pm.DisplayName,
        COUNT(DISTINCT p.Id) AS TransactionCount,
        ISNULL(SUM(p.Amount), 0) AS TotalAmount,
        ISNULL(AVG(p.Amount), 0) AS AverageAmount,
        -- Calculate percentage of total
        CASE 
            WHEN (SELECT SUM(Amount) FROM Payments WHERE Status = 1 
                  AND CreatedAt BETWEEN @StartDate AND @EndDate) > 0
            THEN (ISNULL(SUM(p.Amount), 0) / 
                 (SELECT SUM(Amount) FROM Payments WHERE Status = 1 
                  AND CreatedAt BETWEEN @StartDate AND @EndDate) * 100)
            ELSE 0
        END AS Percentage
    FROM PaymentMethods pm
    LEFT JOIN Payments p ON pm.Id = p.PaymentMethodId 
        AND p.Status = 1
        AND p.CreatedAt BETWEEN @StartDate AND @EndDate
    GROUP BY pm.Id, pm.Name, pm.DisplayName
    HAVING SUM(p.Amount) > 0
    ORDER BY TotalAmount DESC;

    -- Result Set 3: Daily Financial Breakdown
    SELECT
        CAST(o.CreatedAt AS DATE) AS Date,
        DATENAME(WEEKDAY, o.CreatedAt) AS DayOfWeek,
        COUNT(DISTINCT o.Id) AS OrderCount,
        ISNULL(SUM(o.TotalAmount), 0) AS Revenue,
        ISNULL(SUM(o.Subtotal), 0) AS SubTotal,
        ISNULL(SUM(o.TaxAmount), 0) AS Tax,
        ISNULL(SUM(o.TipAmount), 0) AS Tips,
        ISNULL(SUM(o.DiscountAmount), 0) AS Discounts,
        ISNULL(AVG(o.TotalAmount), 0) AS AvgOrderValue,
        -- Net revenue after discounts
        ISNULL(SUM(o.TotalAmount) - SUM(o.DiscountAmount), 0) AS NetRevenue,
        -- Cash vs Card breakdown
        ISNULL(SUM(CASE WHEN pm.Name = 'CASH' THEN p.Amount ELSE 0 END), 0) AS CashAmount,
        ISNULL(SUM(CASE WHEN pm.Name IN ('CREDIT_CARD', 'DEBIT_CARD', 'UPI', 'NET_BANKING') THEN p.Amount ELSE 0 END), 0) AS DigitalAmount
    FROM Orders o
    LEFT JOIN Payments p ON o.Id = p.OrderId AND p.Status = 1
    LEFT JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3)
    GROUP BY CAST(o.CreatedAt AS DATE), DATENAME(WEEKDAY, o.CreatedAt)
    ORDER BY Date DESC;

    -- Result Set 4: Revenue by Category
    SELECT
        ISNULL(c.Name, 'Uncategorized') AS Category,
        COUNT(DISTINCT oi.Id) AS ItemCount,
        ISNULL(SUM(oi.Quantity), 0) AS TotalQuantity,
        ISNULL(SUM(oi.Subtotal), 0) AS TotalRevenue,
        ISNULL(AVG(oi.UnitPrice), 0) AS AvgPrice,
        -- Calculate percentage of total revenue
        CASE 
            WHEN (SELECT SUM(oi2.Subtotal) FROM OrderItems oi2 
                  JOIN Orders o2 ON oi2.OrderId = o2.Id
                  WHERE o2.CreatedAt BETWEEN @StartDate AND @EndDate 
                  AND o2.Status IN (2, 3)) > 0
            THEN (ISNULL(SUM(oi.Subtotal), 0) / 
                 (SELECT SUM(oi2.Subtotal) FROM OrderItems oi2 
                  JOIN Orders o2 ON oi2.OrderId = o2.Id
                  WHERE o2.CreatedAt BETWEEN @StartDate AND @EndDate 
                  AND o2.Status IN (2, 3)) * 100)
            ELSE 0
        END AS RevenuePercentage
    FROM MenuItems mi
    LEFT JOIN Categories c ON mi.CategoryId = c.Id
    INNER JOIN OrderItems oi ON mi.Id = oi.MenuItemId
    INNER JOIN Orders o ON oi.OrderId = o.Id
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3)
    GROUP BY c.Name
    ORDER BY TotalRevenue DESC;

    -- Result Set 5: Top Performing Items
    SELECT TOP 20
        mi.Id AS MenuItemId,
        mi.Name AS ItemName,
        c.Name AS Category,
        mi.Price,
        ISNULL(SUM(oi.Quantity), 0) AS QuantitySold,
        ISNULL(SUM(oi.Subtotal), 0) AS TotalRevenue,
        ISNULL(AVG(oi.Subtotal), 0) AS AvgRevenue,
        COUNT(DISTINCT oi.OrderId) AS OrderCount,
        -- Calculate contribution to total revenue
        CASE 
            WHEN (SELECT SUM(oi2.Subtotal) FROM OrderItems oi2 
                  JOIN Orders o2 ON oi2.OrderId = o2.Id
                  WHERE o2.CreatedAt BETWEEN @StartDate AND @EndDate 
                  AND o2.Status IN (2, 3)) > 0
            THEN (ISNULL(SUM(oi.Subtotal), 0) / 
                 (SELECT SUM(oi2.Subtotal) FROM OrderItems oi2 
                  JOIN Orders o2 ON oi2.OrderId = o2.Id
                  WHERE o2.CreatedAt BETWEEN @StartDate AND @EndDate 
                  AND o2.Status IN (2, 3)) * 100)
            ELSE 0
        END AS RevenueContribution
    FROM MenuItems mi
    LEFT JOIN Categories c ON mi.CategoryId = c.Id
    INNER JOIN OrderItems oi ON mi.Id = oi.MenuItemId
    INNER JOIN Orders o ON oi.OrderId = o.Id
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3)
    GROUP BY mi.Id, mi.Name, c.Name, mi.Price
    ORDER BY TotalRevenue DESC;

    -- Result Set 6: Period Comparison (Current vs Previous Period)
    SELECT
        'Current Period' AS Period,
        COUNT(DISTINCT o.Id) AS Orders,
        ISNULL(SUM(o.TotalAmount), 0) AS Revenue,
        ISNULL(AVG(o.TotalAmount), 0) AS AvgOrderValue,
        ISNULL(SUM(o.DiscountAmount), 0) AS Discounts,
        ISNULL(SUM(o.TaxAmount), 0) AS Tax
    FROM Orders o
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3)
    
    UNION ALL
    
    SELECT
        'Previous Period' AS Period,
        COUNT(DISTINCT o.Id) AS Orders,
        ISNULL(SUM(o.TotalAmount), 0) AS Revenue,
        ISNULL(AVG(o.TotalAmount), 0) AS AvgOrderValue,
        ISNULL(SUM(o.DiscountAmount), 0) AS Discounts,
        ISNULL(SUM(o.TaxAmount), 0) AS Tax
    FROM Orders o
    WHERE o.CreatedAt BETWEEN @CompStartDate AND @CompEndDate
        AND o.Status IN (2, 3)
    ORDER BY Period DESC;

    -- Result Set 7: Hourly Revenue Pattern
    SELECT
        DATEPART(HOUR, o.CreatedAt) AS Hour,
        COUNT(DISTINCT o.Id) AS OrderCount,
        ISNULL(SUM(o.TotalAmount), 0) AS Revenue,
        ISNULL(AVG(o.TotalAmount), 0) AS AvgOrderValue
    FROM Orders o
    WHERE o.CreatedAt BETWEEN @StartDate AND @EndDate
        AND o.Status IN (2, 3)
    GROUP BY DATEPART(HOUR, o.CreatedAt)
    ORDER BY Hour;

END
GO

PRINT 'Financial Summary stored procedure created successfully!';
GO
