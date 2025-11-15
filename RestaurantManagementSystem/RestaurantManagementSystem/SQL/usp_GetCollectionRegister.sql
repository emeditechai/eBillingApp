-- Order Wise Payment Method Wise Daily Collection Register
-- Updated to show GST Amount and correct Actual Bill Amount (Subtotal - Discount)
-- This stored procedure generates a detailed collection report with support for split payments

IF OBJECT_ID('dbo.usp_GetCollectionRegister', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetCollectionRegister;
GO

CREATE PROCEDURE dbo.usp_GetCollectionRegister
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @PaymentMethodId INT = NULL,  -- NULL means ALL payment methods
    @UserId INT = NULL            -- NULL means all users
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to today if no dates provided
    IF @FromDate IS NULL SET @FromDate = CAST(GETDATE() AS DATE);
    IF @ToDate IS NULL SET @ToDate = CAST(GETDATE() AS DATE);

    -- Ensure FromDate <= ToDate
    IF @FromDate > @ToDate
    BEGIN
        DECLARE @Temp DATE = @FromDate;
        SET @FromDate = @ToDate;
        SET @ToDate = @Temp;
    END;

    SELECT 
        o.OrderNumber AS OrderNo,
        ISNULL(t.TableName, 'N/A') AS TableNo,
        ISNULL(p.ProcessedByName, 'System') AS Username,
        
        -- CORRECTED: Actual Bill Amount = Subtotal - Discount (before GST)
        -- This represents the taxable amount (base for GST calculation)
        ISNULL(o.Subtotal, 0) - ISNULL(p.DiscAmount, 0) AS ActualBillAmount,
        
        ISNULL(p.DiscAmount, 0) AS DiscountAmount,
        
        -- GST Amount = CGST + SGST (sum from payment or order)
        ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) AS GSTAmount,
        
        ISNULL(p.RoundoffAdjustmentAmt, 0) AS RoundOffAmount,
        
        -- Receipt Amount = Amount paid + Tip
        p.Amount + ISNULL(p.TipAmount, 0) AS ReceiptAmount,
        
        pm.Name AS PaymentMethod,
        CASE 
            WHEN p.DiscAmount > 0 THEN 
                CONCAT('Discount: ₹', CAST(p.DiscAmount AS VARCHAR(20)))
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) > 0 THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 THEN ' | ' ELSE '' END,
                       'GST: ₹', CAST(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) AS VARCHAR(20)))
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.LastFourDigits, '') <> '' THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 OR (ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0)) > 0 THEN ' | ' ELSE '' END, 
                       'Card: ', p.CardType, ' *', p.LastFourDigits)
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.ReferenceNumber, '') <> '' THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 OR ISNULL(p.LastFourDigits, '') <> '' OR (ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0)) > 0 THEN ' | ' ELSE '' END,
                       'Ref: ', p.ReferenceNumber)
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.TipAmount, 0) > 0 THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 OR ISNULL(p.LastFourDigits, '') <> '' OR ISNULL(p.ReferenceNumber, '') <> '' OR (ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0)) > 0 THEN ' | ' ELSE '' END,
                       'Tip: ₹', CAST(p.TipAmount AS VARCHAR(20)))
            ELSE ''
        END AS Details,
        p.CreatedAt AS PaymentDate
    FROM Payments p
    INNER JOIN Orders o ON p.OrderId = o.Id
    INNER JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
    LEFT JOIN OrderTables ot ON o.Id = ot.OrderId
    LEFT JOIN Tables t ON ot.TableId = t.Id
        WHERE CAST(p.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
            AND p.Status = 1  -- Only approved payments
            AND (@PaymentMethodId IS NULL OR p.PaymentMethodId = @PaymentMethodId)
            AND (@UserId IS NULL OR p.ProcessedBy = @UserId)
    ORDER BY p.CreatedAt DESC, o.OrderNumber, pm.Name;
END
GO

PRINT 'Collection Register stored procedure updated successfully.';
PRINT 'Actual Bill Amount now calculated as Orders.Subtotal - Discount';
PRINT 'GST Amount column added (CGST + SGST)';
GO
