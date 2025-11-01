-- Test the EXACT BOT Dashboard query with fixes
USE [dev_Restaurant]
GO

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
    ISNULL(SUM(oi.Subtotal), 0) AS SubtotalAmount,
    ISNULL(SUM(oi.Subtotal * ISNULL(mi.GST_Perc, 0) / 100), 0) AS TaxAmount,
    ISNULL(SUM(oi.Subtotal * (1 + ISNULL(mi.GST_Perc, 0) / 100)), 0) AS TotalAmount,
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
