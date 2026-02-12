-- Stored procedure to get recent orders for Home Dashboard
-- Returns the latest orders with customer and table information

IF OBJECT_ID('usp_GetRecentOrdersForDashboard', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetRecentOrdersForDashboard
GO

CREATE PROCEDURE usp_GetRecentOrdersForDashboard
    @OrderCount INT = 5, -- Default to 5 recent orders
    @UserId INT = NULL,
    @CanViewAll BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP (@OrderCount)
        o.Id as OrderId,
        ISNULL(NULLIF(o.OrderNumber, ''), 'ORD-' + CAST(o.Id AS VARCHAR(10))) as OrderNumber,
        ISNULL(o.CustomerName, 'Walk-in Customer') as CustomerName,
        CASE 
            WHEN merged.MergedTableNames IS NOT NULL THEN merged.MergedTableNames
            WHEN t.TableNumber IS NOT NULL THEN CAST(t.TableNumber AS VARCHAR(10))
            WHEN o.TableTurnoverId IS NULL THEN 'Takeout'
            ELSE 'Table ' + CAST(ISNULL(t.TableNumber, 'N/A') AS VARCHAR(10))
        END as TableNumber,
        o.TotalAmount,
        CASE o.Status
            WHEN 0 THEN 'Pending'
            WHEN 1 THEN 'In Progress'
            WHEN 2 THEN 'Ready'
            WHEN 3 THEN 'Completed'
            WHEN 4 THEN 'Cancelled'
            ELSE 'Unknown'
        END as Status,
        FORMAT(o.CreatedAt, 'hh:mm tt') as OrderTime
    FROM Orders o
    LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
    LEFT JOIN Tables t ON tt.TableId = t.Id
    LEFT JOIN (
        SELECT 
            ot.OrderId,
            STRING_AGG(t2.TableName, ' + ') WITHIN GROUP (ORDER BY t2.TableName) AS MergedTableNames
        FROM OrderTables ot
        INNER JOIN Tables t2 ON ot.TableId = t2.Id
        GROUP BY ot.OrderId
    ) merged ON o.Id = merged.OrderId
        WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE) -- Today's orders only
            AND o.TotalAmount > 0 -- Exclude incomplete orders
            AND (
                        @CanViewAll = 1 
                        OR (@UserId IS NOT NULL AND o.UserId = @UserId)
                    )
    ORDER BY o.CreatedAt DESC;
        
END
GO