-- Update stored procedure to get order payment info with GST data from Payments table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_GetOrderPaymentInfo]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_GetOrderPaymentInfo]
GO

CREATE PROCEDURE [dbo].[usp_GetOrderPaymentInfo]
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get order details
    SELECT 
        o.Id,
        o.OrderNumber,
        o.Subtotal,
        o.TaxAmount,
        o.TipAmount,
        o.DiscountAmount,
        o.TotalAmount,
        ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0) AS PaidAmount,
        -- RemainingAmount calculation: if roundoff exists and makes remaining nearly zero, show 0
        CASE 
            WHEN ABS(o.TotalAmount - ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0)) <= 1 THEN 0
            ELSE (o.TotalAmount - ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0))
        END AS RemainingAmount,
        ISNULL(t.TableName, 'N/A') AS TableName,
        o.Status,
        o.OrderType,
        o.Customeremailid AS CustomerEmailId,
        o.CustomerName
    FROM 
        Orders o
    LEFT JOIN 
        Payments p ON o.Id = p.OrderId AND p.Status = 1 -- Approved payments only
    LEFT JOIN 
        TableTurnovers tt ON o.TableTurnoverId = tt.Id
    LEFT JOIN
        Tables t ON tt.TableId = t.Id
    WHERE 
        o.Id = @OrderId
    GROUP BY
        o.Id,
        o.OrderNumber,
        o.Subtotal,
        o.TaxAmount,
        o.TipAmount,
        o.DiscountAmount,
        o.TotalAmount,
        t.TableName,
        o.Status,
        o.OrderType,
        o.Customeremailid,
        o.CustomerName;
    
    -- Get order items
    SELECT
        oi.Id,
        oi.MenuItemId,
        mi.Name,
        oi.Quantity,
        oi.UnitPrice,
        oi.Subtotal
    FROM
        OrderItems oi
    INNER JOIN
        MenuItems mi ON oi.MenuItemId = mi.Id
    WHERE
        oi.OrderId = @OrderId
        AND oi.Status != 5; -- Not cancelled
    
    -- Get existing payments with GST information
    SELECT
        p.Id,
        p.PaymentMethodId,
        pm.Name AS PaymentMethod,
        pm.DisplayName AS PaymentMethodDisplay,
    p.Amount,
        p.TipAmount,
        p.Status,
    p.RoundoffAdjustmentAmt,
        p.ReferenceNumber,
        p.LastFourDigits,
        p.CardType,
        p.AuthorizationCode,
        p.Notes,
        p.ProcessedByName,
        p.CreatedAt,
        p.GSTAmount,
        p.CGSTAmount,
        p.SGSTAmount,
        p.DiscAmount,
        p.GST_Perc,
        p.CGST_Perc,
        p.SGST_Perc,
        p.Amount_ExclGST
    FROM
        Payments p
    INNER JOIN
        PaymentMethods pm ON p.PaymentMethodId = pm.Id
    WHERE
        p.OrderId = @OrderId
    ORDER BY
        p.CreatedAt DESC;
    
    -- Get available payment methods
    SELECT
        Id,
        Name,
        DisplayName,
        RequiresCardInfo,
        RequiresCardPresent,
        RequiresApproval
    FROM
        PaymentMethods
    WHERE
        IsActive = 1
    ORDER BY
        DisplayName;
END
GO

PRINT 'Updated usp_GetOrderPaymentInfo stored procedure with GST columns successfully.';