--EXEC [dbo].[usp_GetBarBOTReport] ''
CREATE OR ALTER PROCEDURE [dbo].[usp_GetBarBOTReport]
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @Station NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Start DATETIME = COALESCE(CAST(@FromDate AS DATETIME), DATEADD(day, -1, CAST(GETDATE() AS DATE)));
    DECLARE @End DATETIME = DATEADD(day, 1, COALESCE(CAST(@ToDate AS DATETIME), CAST(GETDATE() AS DATE)));

    SELECT DISTINCT
        o.Id AS OrderId,
        o.OrderNumber,
        ISNULL(t.TableName, CONCAT('Table ', o.TableTurnoverId)) AS TableName,
        i.Name AS ItemName,
        oi.Quantity,
        ISNULL(s.Name, 'Bar') AS Station,
        CASE 
            WHEN kt.Status = 0 THEN 'New'
            WHEN kt.Status = 1 THEN 'In Progress'
            WHEN kt.Status = 2 THEN 'Ready'
            WHEN kt.Status = 3 THEN 'Completed'
            ELSE 'Pending'
        END AS Status,
        o.CreatedAt AS RequestedAt
    FROM OrderItems oi
    INNER JOIN dbo.Orders o ON oi.OrderId = o.Id
    LEFT JOIN dbo.MenuItems i ON oi.MenuItemId = i.Id
    LEFT JOIN dbo.menuitemgroup mig ON i.menuitemgroupID = mig.ID
    LEFT JOIN [dbo].[KitchenStations] s ON i.KitchenStationId = s.Id
    LEFT JOIN Tables t ON o.TableTurnoverId = t.Id
    LEFT JOIN KitchenTickets kt ON kt.OrderId = o.Id 
        AND kt.KitchenStation = 'BAR' 
        AND kt.TicketNumber LIKE 'BOT-%'
    WHERE mig.itemgroup = 'BAR'
      AND (o.CreatedAt >= @Start AND o.CreatedAt < @End)
      AND (@Station IS NULL OR @Station = '' OR s.Name = @Station)
    ORDER BY o.CreatedAt DESC;
END
GO
