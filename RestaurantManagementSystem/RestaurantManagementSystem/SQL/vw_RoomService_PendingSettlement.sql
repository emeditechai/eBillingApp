-- Room Service Pending Settlement View
-- Returns pending Room Service (OrderType=4) orders which are not completed (Status <> 3)
-- Intended for syncing/settlement with an external Hotel application.

USE [dev_restaurant];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER VIEW dbo.vw_RoomService_PendingSettlement
AS
    SELECT
        o.[HBookingID]                         AS BookingID,
        CAST(o.[HBookingNo] AS NVARCHAR(50))   AS BookingNo,

        -- Guest details: for Room Service we persist guest info in Orders.CustomerName/CustomerPhone
        CAST(o.[CustomerName] AS NVARCHAR(100)) AS GuestName,
        CAST(o.[CustomerPhone] AS NVARCHAR(30)) AS GuestPhoneNumber,

        o.[RoomID]                             AS RoomID,
        -- RoomNo is not persisted in Orders by default; expose RoomID as a stable identifier.
        CAST(o.[RoomID] AS NVARCHAR(50))       AS RoomNo,

        o.[OrderType]                          AS OrderType,
        o.[Id]                                 AS OrderID,
        o.[OrderNumber]                        AS OrderNo,

        CAST(ISNULL(o.[TotalAmount], 0) AS DECIMAL(18,2)) AS BillAmount,

        CAST(ISNULL(o.[TaxAmount], 0) AS DECIMAL(18,2)) AS GSTAmount,

        -- If you store CGST/SGST separately in Orders, update here.
        CAST(ROUND(ISNULL(o.[TaxAmount], 0) / 2.0, 2) AS DECIMAL(18,2)) AS CGSTAmount,
        CAST(ROUND(ISNULL(o.[TaxAmount], 0) - (ISNULL(o.[TaxAmount], 0) / 2.0), 2) AS DECIMAL(18,2)) AS SGSTAmount,

        CAST(ISNULL(o.[DiscountAmount], 0) AS DECIMAL(18,2)) AS DiscountAmount,

        CAST(
            ROUND(
                ISNULL(o.[TotalAmount], 0)
                - ISNULL(paid.[PaidAmount], 0)
            , 2)
        AS DECIMAL(18,2)) AS PayableAmount,

        o.[H_BranchID]                          AS BranchID
    FROM [dev_restaurant].dbo.Orders o
    OUTER APPLY (
        SELECT
            SUM(ISNULL(p.[Amount], 0)
                + ISNULL(p.[TipAmount], 0)
            ) AS PaidAmount
        FROM [dev_restaurant].dbo.Payments p
        WHERE p.[OrderId] = o.[Id]
          AND p.[Status] = 1
    ) paid
    WHERE o.[OrderType] = 4
      AND ISNULL(o.[Status], 0) <> 3;
GO
