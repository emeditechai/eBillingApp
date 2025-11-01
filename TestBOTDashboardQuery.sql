-- Check BOT Tickets with their items
USE [dev_Restaurant]
GO

PRINT '=== BOT Tickets with Item Counts ==='
SELECT 
    kt.Id,
    kt.TicketNumber,
    kt.KitchenStation,
    kt.Status,
    kt.CreatedAt,
    COUNT(kti.Id) AS ItemCount
FROM KitchenTickets kt
LEFT JOIN KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
WHERE kt.KitchenStation = 'BAR'
   OR kt.TicketNumber LIKE 'BOT-%'
GROUP BY kt.Id, kt.TicketNumber, kt.KitchenStation, kt.Status, kt.CreatedAt
ORDER BY kt.CreatedAt DESC;
GO

PRINT ''
PRINT '=== Check if any Kitchen_TicketItems exist ==='
SELECT COUNT(*) AS Kitchen_TicketItems_Count
FROM Kitchen_TicketItems;
GO

PRINT ''
PRINT '=== Exact query that BOT Dashboard uses (when KitchenStation exists) ==='
DECLARE @Status INT = NULL;

SELECT 
    kt.Id AS BOT_ID,
    kt.TicketNumber AS BOT_No,
    kt.OrderId,
    o.OrderNumber,
    CASE WHEN o.OrderType = 0 THEN t.TableName ELSE NULL END AS TableName,
    CASE WHEN o.OrderType = 0 THEN tt.GuestName ELSE o.CustomerName END AS GuestName,
    CONCAT(u.FirstName, ' ', ISNULL(u.LastName, '')) AS ServerName,
    kt.Status,
    ISNULL(SUM(oi.Quantity * oi.Price), 0) AS SubtotalAmount,
    ISNULL(SUM(oi.Quantity * oi.Price * ISNULL(mi.GST_Perc, 0) / 100), 0) AS TaxAmount,
    ISNULL(SUM(oi.Quantity * oi.Price * (1 + ISNULL(mi.GST_Perc, 0) / 100)), 0) AS TotalAmount,
    kt.CreatedAt,
    DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated
FROM KitchenTickets kt
INNER JOIN Orders o ON kt.OrderId = o.Id
LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
LEFT JOIN Tables t ON tt.TableId = t.Id
LEFT JOIN Users u ON o.UserId = u.Id
LEFT JOIN KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
LEFT JOIN OrderItems oi ON kti.OrderItemId = oi.Id
LEFT JOIN MenuItems mi ON oi.MenuItemId = mi.Id
WHERE kt.KitchenStation = 'BAR' 
  AND kt.TicketNumber LIKE 'BOT-%'
  AND (@Status IS NULL OR kt.Status = @Status)
GROUP BY kt.Id, kt.TicketNumber, kt.OrderId, o.OrderNumber, o.OrderType, 
         t.TableName, tt.GuestName, o.CustomerName, u.FirstName, u.LastName, 
         kt.Status, kt.CreatedAt
ORDER BY kt.CreatedAt DESC;
GO
