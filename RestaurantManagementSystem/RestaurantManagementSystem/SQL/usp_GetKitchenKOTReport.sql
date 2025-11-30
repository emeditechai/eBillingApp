--EXEC [dbo].[usp_GetKitchenKOTReport] ''
create or alter PROCEDURE [dbo].[usp_GetKitchenKOTReport]
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @Station NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Start DATETIME = COALESCE(CAST(@FromDate AS DATETIME), DATEADD(day, -1, CAST(GETDATE() AS DATE)));
    DECLARE @End DATETIME = DATEADD(day, 1, COALESCE(CAST(@ToDate AS DATETIME), CAST(GETDATE() AS DATE)));

    SELECT
        o.Id AS OrderId,
        o.OrderNumber,
        kt.TicketNumber AS KOTNumber,
        ISNULL(t.TableName, CONCAT('Table ', o.TableTurnoverId)) AS TableName,
        i.Name AS ItemName,
        oi.Quantity,
        ISNULL(s.Name, '') AS Station,
        CASE WHEN kti.CompletionTime IS NOT NULL THEN 'Completed' ELSE 'Pending' END AS Status,
        COALESCE(kti.StartTime, kt.CreatedAt) AS RequestedAt
    FROM OrderItems oi
    INNER JOIN dbo.Orders o ON oi.OrderId = o.Id
    INNER JOIN [dbo].[KitchenTicketItems] kti on kti.OrderItemId = oi.Id
    INNER JOIN [dbo].[KitchenTickets] kt ON kti.KitchenTicketId = kt.Id
    LEFT JOIN dbo.MenuItems i ON oi.MenuItemId = i.Id
    LEFT JOIN [dbo].[KitchenStations] s ON i.KitchenStationId = s.Id
    LEFT JOIN Tables t ON o.TableTurnoverId = t.Id
    WHERE kt.KitchenStation = 'KITCHEN'
      AND (
          (kt.CreatedAt >= @Start AND kt.CreatedAt < @End)
          OR (kti.CompletionTime >= @Start AND kti.CompletionTime < @End)
      )
      AND (@Station IS NULL OR @Station = '' OR s.Name = @Station)
      AND (s.Name IS NULL OR s.Name <> 'Bar')
    ORDER BY kt.CreatedAt DESC, kt.TicketNumber DESC;
END
GO
