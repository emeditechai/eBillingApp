-- Room Service Pending Settlement Procedure
-- Returns pending Room Service (OrderType=4) order details for a given BookingID + RoomID + BranchID.
-- Includes order header fields (same as vw_RoomService_PendingSettlement) + item-wise MenuItemName and Rate.

USE [dev_restaurant];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetRoomServicePendingSettlementDetails
    @BookingID INT,
    @RoomID INT,
    @BranchID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OrderID INT;

    -- Pick the most recent non-completed Room Service order for this booking/room/branch
    SELECT TOP (1)
        @OrderID = o.Id
        FROM [dev_restaurant].dbo.Orders o
    WHERE o.OrderType = 4
      AND ISNULL(o.Status, 0) <> 3
      AND o.HBookingID = @BookingID
      AND o.RoomID = @RoomID
      AND o.H_BranchID = @BranchID
    ORDER BY o.CreatedAt DESC, o.Id DESC;

    IF @OrderID IS NULL
    BEGIN
        -- Return an empty result set with the expected schema
        SELECT
            CAST(NULL AS INT)            AS BookingID,
            CAST(NULL AS NVARCHAR(50))   AS BookingNo,
            CAST(NULL AS NVARCHAR(100))  AS GuestName,
            CAST(NULL AS NVARCHAR(30))   AS GuestPhoneNumber,
            CAST(NULL AS INT)            AS RoomID,
            CAST(NULL AS NVARCHAR(50))   AS RoomNo,
            CAST(4 AS INT)               AS OrderType,
            CAST(NULL AS INT)            AS OrderID,
            CAST(NULL AS NVARCHAR(20))   AS OrderNo,
            CAST(0 AS DECIMAL(18,2))     AS BillAmount,
            CAST(0 AS DECIMAL(18,2))     AS GSTAmount,
            CAST(0 AS DECIMAL(18,2))     AS CGSTAmount,
            CAST(0 AS DECIMAL(18,2))     AS SGSTAmount,
            CAST(0 AS DECIMAL(18,2))     AS DiscountAmount,
            CAST(0 AS DECIMAL(18,2))     AS PayableAmount,
            CAST(NULL AS INT)            AS BranchID,
            CAST(NULL AS INT)            AS MenuItemID,
            CAST(NULL AS NVARCHAR(200))  AS MenuItemName,
            CAST(0 AS INT)               AS Quantity,
            CAST(0 AS DECIMAL(18,2))     AS Rate,
            CAST(0 AS DECIMAL(18,2))     AS ItemAmount;
        RETURN;
    END

    ;WITH OrderHeader AS (
        SELECT
            o.Id,
            o.OrderNumber,
            o.OrderType,
            o.Status,
            o.HBookingID,
            CAST(o.HBookingNo AS NVARCHAR(50)) AS HBookingNo,
            CAST(o.CustomerName AS NVARCHAR(100)) AS GuestName,
            CAST(o.CustomerPhone AS NVARCHAR(30)) AS GuestPhoneNumber,
            o.RoomID,
            CAST(o.RoomID AS NVARCHAR(50)) AS RoomNo,
            o.H_BranchID AS BranchID,
            CAST(ISNULL(o.TotalAmount, 0) AS DECIMAL(18,2)) AS BillAmount,
            CAST(ISNULL(o.TaxAmount, 0) AS DECIMAL(18,2)) AS GSTAmount,
            CAST(ROUND(ISNULL(o.TaxAmount, 0) / 2.0, 2) AS DECIMAL(18,2)) AS CGSTAmount,
            CAST(ROUND(ISNULL(o.TaxAmount, 0) - (ISNULL(o.TaxAmount, 0) / 2.0), 2) AS DECIMAL(18,2)) AS SGSTAmount,
            CAST(ISNULL(o.DiscountAmount, 0) AS DECIMAL(18,2)) AS DiscountAmount
        FROM [dev_restaurant].dbo.Orders o
        WHERE o.Id = @OrderID
    ), Paid AS (
        SELECT
            p.OrderId,
            SUM(ISNULL(p.Amount, 0) + ISNULL(p.TipAmount, 0)) AS PaidAmount
        FROM [dev_restaurant].dbo.Payments p
        WHERE p.OrderId = @OrderID
          AND p.Status = 1
        GROUP BY p.OrderId
    )
    SELECT
        h.HBookingID AS BookingID,
        h.HBookingNo AS BookingNo,
        h.GuestName,
        h.GuestPhoneNumber,
        h.RoomID,
        h.RoomNo,
        h.OrderType,
        h.Id AS OrderID,
        h.OrderNumber AS OrderNo,
        h.BillAmount,
        h.GSTAmount,
        h.CGSTAmount,
        h.SGSTAmount,
        h.DiscountAmount,
        CAST(ROUND(h.BillAmount - ISNULL(pd.PaidAmount, 0), 2) AS DECIMAL(18,2)) AS PayableAmount,
        h.BranchID,

        oi.MenuItemId AS MenuItemID,
        mi.Name AS MenuItemName,
        oi.Quantity,
        CAST(ISNULL(oi.UnitPrice, 0) AS DECIMAL(18,2)) AS Rate,
        CAST(ISNULL(oi.Subtotal, (ISNULL(oi.UnitPrice, 0) * ISNULL(oi.Quantity, 0))) AS DECIMAL(18,2)) AS ItemAmount
    FROM OrderHeader h
    LEFT JOIN Paid pd ON pd.OrderId = h.Id
    INNER JOIN [dev_restaurant].dbo.OrderItems oi ON oi.OrderId = h.Id
    INNER JOIN [dev_restaurant].dbo.MenuItems mi ON mi.Id = oi.MenuItemId
    WHERE ISNULL(oi.Status, 0) < 5
    ORDER BY oi.CreatedAt, oi.Id;
END
GO
