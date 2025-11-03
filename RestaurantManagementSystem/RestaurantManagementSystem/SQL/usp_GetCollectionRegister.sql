-- Order Wise Payment Method Wise Daily Collection Register
-- This stored procedure generates a detailed collection report with support for split payments

IF OBJECT_ID('dbo.usp_GetCollectionRegister', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetCollectionRegister;
GO

CREATE PROCEDURE dbo.usp_GetCollectionRegister
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @PaymentMethodId INT = NULL  -- NULL means ALL payment methods
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
        o.TotalAmount AS ActualBillAmount,
        ISNULL(p.DiscAmount, 0) AS DiscountAmount,
        ISNULL(p.RoundoffAdjustmentAmt, 0) AS RoundOffAmount,
        p.Amount + ISNULL(p.TipAmount, 0) AS ReceiptAmount,
        pm.Name AS PaymentMethod,
        CASE 
            WHEN p.DiscAmount > 0 THEN 
                CONCAT('Discount: ₹', CAST(p.DiscAmount AS VARCHAR(20)))
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.LastFourDigits, '') <> '' THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 THEN ' | ' ELSE '' END, 
                       'Card: ', p.CardType, ' *', p.LastFourDigits)
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.ReferenceNumber, '') <> '' THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 OR ISNULL(p.LastFourDigits, '') <> '' THEN ' | ' ELSE '' END,
                       'Ref: ', p.ReferenceNumber)
            ELSE ''
        END +
        CASE 
            WHEN ISNULL(p.TipAmount, 0) > 0 THEN 
                CONCAT(CASE WHEN p.DiscAmount > 0 OR ISNULL(p.LastFourDigits, '') <> '' OR ISNULL(p.ReferenceNumber, '') <> '' THEN ' | ' ELSE '' END,
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
    ORDER BY p.CreatedAt DESC, o.OrderNumber, pm.Name;
END
GO

PRINT 'Collection Register stored procedure created successfully.';
GO
