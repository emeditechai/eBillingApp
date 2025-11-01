-- Check all KitchenTickets
SELECT TOP 10
    Id,
    TicketNumber,
    KitchenStation,
    OrderId,
    Status,
    CreatedAt
FROM KitchenTickets
ORDER BY CreatedAt DESC;
