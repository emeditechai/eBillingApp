-- Diagnostic Query for BOT Tickets Issue
-- Run this to check what's in your KitchenTickets table

USE [dev_Restaurant]
GO

PRINT '=== Checking KitchenStation Column ==='
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KitchenTickets') AND name = 'KitchenStation')
    PRINT 'KitchenStation column EXISTS'
ELSE
    PRINT 'KitchenStation column DOES NOT EXIST - Run Add_KitchenStation_To_KitchenTickets.sql'
GO

PRINT ''
PRINT '=== All Recent Tickets (Last 20) ==='
SELECT TOP 20
    Id,
    TicketNumber,
    KitchenStation,
    OrderId,
    Status,
    CreatedAt,
    UpdatedAt
FROM KitchenTickets
ORDER BY CreatedAt DESC;
GO

PRINT ''
PRINT '=== BOT Tickets Only ==='
SELECT 
    Id,
    TicketNumber,
    KitchenStation,
    OrderId,
    Status,
    CreatedAt
FROM KitchenTickets
WHERE KitchenStation = 'BAR'
   OR TicketNumber LIKE 'BOT-%'
ORDER BY CreatedAt DESC;
GO

PRINT ''
PRINT '=== Ticket Count by Station ==='
SELECT 
    KitchenStation,
    COUNT(*) AS TicketCount,
    MIN(CreatedAt) AS FirstTicket,
    MAX(CreatedAt) AS LastTicket
FROM KitchenTickets
GROUP BY KitchenStation;
GO

PRINT ''
PRINT '=== Today''s Tickets ==='
SELECT 
    KitchenStation,
    TicketNumber,
    Status,
    CreatedAt
FROM KitchenTickets
WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY CreatedAt DESC;
GO
