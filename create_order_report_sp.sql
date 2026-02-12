-- Create comprehensive Order Report stored procedure
-- This stored procedure provides detailed order reporting with filters and analytics

IF OBJECT_ID('usp_GetOrderReport', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetOrderReport
GO

CREATE PROCEDURE usp_GetOrderReport
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @UserId INT = NULL,
    @Status INT = NULL,
    @OrderType INT = NULL,
    @SearchTerm NVARCHAR(100) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;

    -- Set default date range if not provided
    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE)
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE)
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    -- Result Set 1: Summary Statistics
    SELECT 
        COUNT(*) as TotalOrders,
        COUNT(CASE WHEN o.Status = 0 THEN 1 END) as PendingOrders,
        COUNT(CASE WHEN o.Status = 2 THEN 1 END) as InProgressOrders,
        COUNT(CASE WHEN o.Status = 3 THEN 1 END) as CompletedOrders,
        COUNT(CASE WHEN o.Status = 4 THEN 1 END) as CancelledOrders,
        COALESCE(SUM(o.TotalAmount), 0) as TotalRevenue,
        COALESCE(AVG(o.TotalAmount), 0) as AverageOrderValue,
        COUNT(CASE WHEN o.OrderType = 0 THEN 1 END) as DineInOrders,
        COUNT(CASE WHEN o.OrderType = 1 THEN 1 END) as TakeoutOrders,
        COUNT(CASE WHEN o.OrderType = 2 THEN 1 END) as DeliveryOrders
    FROM Orders o
    LEFT JOIN Users u ON o.UserId = u.Id
    WHERE 
        CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
        AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
        AND (@UserId IS NULL OR o.UserId = @UserId)
        AND (@Status IS NULL OR o.Status = @Status)
        AND (@OrderType IS NULL OR o.OrderType = @OrderType)
        AND (
            @SearchTerm IS NULL 
            OR o.OrderNumber LIKE '%' + @SearchTerm + '%'
            OR o.CustomerName LIKE '%' + @SearchTerm + '%'
            OR o.CustomerPhone LIKE '%' + @SearchTerm + '%'
            OR u.FirstName LIKE '%' + @SearchTerm + '%'
            OR u.LastName LIKE '%' + @SearchTerm + '%'
        );

    -- Result Set 2: Order Details (Paginated)
    SELECT 
        o.Id,
        o.OrderNumber,
        o.CustomerName,
        o.CustomerPhone,
        COALESCE(u.FirstName + ' ' + u.LastName, 'Unknown') as WaiterName,
        o.OrderType,
        CASE o.OrderType 
            WHEN 0 THEN 'Dine-In'
            WHEN 1 THEN 'Takeout'
            WHEN 2 THEN 'Delivery'
            ELSE 'Unknown'
        END as OrderTypeName,
        o.Status,
        CASE o.Status 
            WHEN 0 THEN 'New Order'
            WHEN 1 THEN 'Pending'
            WHEN 2 THEN 'In Progress'
            WHEN 3 THEN 'Completed'
            WHEN 4 THEN 'Cancelled'
            ELSE 'Unknown'
        END as StatusName,
        o.Subtotal,
        o.TaxAmount,
        o.TipAmount,
        o.DiscountAmount,
        o.TotalAmount,
        o.SpecialInstructions,
        o.CreatedAt,
        o.CompletedAt,
        -- Calculate order preparation time in minutes
        CASE 
            WHEN o.CompletedAt IS NOT NULL 
            THEN DATEDIFF(MINUTE, o.CreatedAt, o.CompletedAt)
            ELSE NULL
        END as PreparationTimeMinutes,
        -- Count of items in order
        COALESCE(oi.ItemCount, 0) as ItemCount,
        -- Total quantity of items
        COALESCE(oi.TotalQuantity, 0) as TotalQuantity
    FROM Orders o
    LEFT JOIN Users u ON o.UserId = u.Id
    LEFT JOIN (
        SELECT 
            OrderId,
            COUNT(*) as ItemCount,
            SUM(Quantity) as TotalQuantity
        FROM OrderItems 
        GROUP BY OrderId
    ) oi ON o.Id = oi.OrderId
    WHERE 
        CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
        AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
        AND (@UserId IS NULL OR o.UserId = @UserId)
        AND (@Status IS NULL OR o.Status = @Status)
        AND (@OrderType IS NULL OR o.OrderType = @OrderType)
        AND (
            @SearchTerm IS NULL 
            OR o.OrderNumber LIKE '%' + @SearchTerm + '%'
            OR o.CustomerName LIKE '%' + @SearchTerm + '%'
            OR o.CustomerPhone LIKE '%' + @SearchTerm + '%'
            OR u.FirstName LIKE '%' + @SearchTerm + '%'
            OR u.LastName LIKE '%' + @SearchTerm + '%'
        )
    ORDER BY o.CreatedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    -- Result Set 3: Total Count for Pagination
    SELECT COUNT(*) as TotalCount
    FROM Orders o
    LEFT JOIN Users u ON o.UserId = u.Id
    WHERE 
        CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
        AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
        AND (@UserId IS NULL OR o.UserId = @UserId)
        AND (@Status IS NULL OR o.Status = @Status)
        AND (@OrderType IS NULL OR o.OrderType = @OrderType)
        AND (
            @SearchTerm IS NULL 
            OR o.OrderNumber LIKE '%' + @SearchTerm + '%'
            OR o.CustomerName LIKE '%' + @SearchTerm + '%'
            OR o.CustomerPhone LIKE '%' + @SearchTerm + '%'
            OR u.FirstName LIKE '%' + @SearchTerm + '%'
            OR u.LastName LIKE '%' + @SearchTerm + '%'
        );

    -- Result Set 4: Available Users for Filter Dropdown
    SELECT DISTINCT
        u.Id,
        u.FirstName + ' ' + u.LastName as FullName,
        u.FirstName,
        u.LastName
    FROM Users u
    INNER JOIN Orders o ON u.Id = o.UserId
    WHERE u.IsActive = 1
            AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
    ORDER BY u.FirstName, u.LastName;

    -- Result Set 5: Hourly Order Distribution (for charts)
    SELECT 
        DATEPART(HOUR, o.CreatedAt) as Hour,
        COUNT(*) as OrderCount,
        SUM(o.TotalAmount) as HourlyRevenue
    FROM Orders o
    WHERE 
        CAST(o.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
        AND NULLIF(LTRIM(RTRIM(o.OrderNumber)), '') IS NOT NULL
        AND (@UserId IS NULL OR o.UserId = @UserId)
        AND (@Status IS NULL OR o.Status = @Status)
        AND (@OrderType IS NULL OR o.OrderType = @OrderType)
    GROUP BY DATEPART(HOUR, o.CreatedAt)
    ORDER BY Hour;

END
GO

PRINT 'Order Report stored procedure created successfully!'