-- Update the stored procedure for Recent Orders to fix display issues
-- Run this in SQL Server Management Studio or Azure Data Studio

IF OBJECT_ID('usp_GetRecentOrdersForDashboard', 'P') IS NOT NULL
    DROP PROCEDURE usp_GetRecentOrdersForDashboard
GO

CREATE PROCEDURE usp_GetRecentOrdersForDashboard
    @OrderCount INT = 5 -- Default to 5 recent orders
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT TOP (@OrderCount)
        o.Id as OrderId,
        ISNULL(NULLIF(o.OrderNumber, ''), 'ORD-' + CAST(o.Id AS VARCHAR(10))) as OrderNumber,
        ISNULL(o.CustomerName, 'Walk-in Customer') as CustomerName,
        CASE 
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
    WHERE CAST(o.CreatedAt AS DATE) = CAST(GETDATE() AS DATE) -- Today's orders only
      AND o.TotalAmount > 0 -- Exclude incomplete orders
    ORDER BY o.CreatedAt DESC;
        
END
GO

-- Test the stored procedure
EXEC usp_GetRecentOrdersForDashboard @OrderCount = 5;