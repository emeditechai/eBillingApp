-- Simple query to check BOT tickets
USE [dev_Restaurant]
GO

-- Check all tickets from today
SELECT 
    Id,
    TicketNumber,
    KitchenStation,
    OrderId,
    Status,
    CreatedAt
FROM KitchenTickets
WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY CreatedAt DESC;
GO

-- Check specifically for BAR station tickets
SELECT 
    Id,
    TicketNumber,
    KitchenStation,
    OrderId,
    Status,
    CreatedAt
FROM KitchenTickets
WHERE KitchenStation = 'BAR'
ORDER BY CreatedAt DESC;
GO

-- Count by station
SELECT 
    ISNULL(KitchenStation, 'NULL') AS KitchenStation,
    COUNT(*) AS Count
FROM KitchenTickets
WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
GROUP BY KitchenStation;
GO
