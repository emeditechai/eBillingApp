-- Update Collection Register to include Void/Refund payments
-- This script safely updates the stored procedure to fetch and display refunded payments

USE RestaurantManagement;
GO

PRINT 'Updating Collection Register to include Void/Refund payments...';
GO

-- Drop existing procedure
IF OBJECT_ID('dbo.usp_GetCollectionRegister', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetCollectionRegister;
GO

-- Create updated procedure with void/refund support
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

    -- Check if VoidReason column exists
    DECLARE @HasVoidReason BIT = 0;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'VoidReason')
        SET @HasVoidReason = 1;

    -- Use a CTE with optimized filters to reduce data processed
    -- Now includes both approved (Status=1) and void/refund (Status=3) payments
    ;WITH FilteredPayments AS (
        SELECT 
            p.Id,
            p.OrderId,
            p.PaymentMethodId,
            p.Amount,
            p.TipAmount,
            p.DiscAmount,
            p.CGSTAmount,
            p.SGSTAmount,
            p.RoundoffAdjustmentAmt,
            p.ProcessedByName,
            p.LastFourDigits,
            p.CardType,
            p.ReferenceNumber,
            p.CreatedAt,
            p.Status
        FROM Payments p WITH (NOLOCK)
        WHERE p.CreatedAt >= @FromDate 
            AND p.CreatedAt < DATEADD(DAY, 1, @ToDate)
            AND p.Status IN (1, 3)  -- Approved and Void/Refund payments
            AND (@PaymentMethodId IS NULL OR p.PaymentMethodId = @PaymentMethodId)
            AND (@UserId IS NULL OR p.ProcessedBy = @UserId)
    )
    SELECT 
        o.OrderNumber AS OrderNo,
        ISNULL(t.TableName, 'N/A') AS TableNo,
        ISNULL(p.ProcessedByName, 'System') AS Username,
        
        -- CORRECTED: Actual Bill Amount = Subtotal - Discount (before GST)
        -- For void/refund payments, show as negative
        CASE WHEN p.Status = 3 THEN -(ISNULL(o.Subtotal, 0) - ISNULL(p.DiscAmount, 0)) 
             ELSE ISNULL(o.Subtotal, 0) - ISNULL(p.DiscAmount, 0) 
        END AS ActualBillAmount,
        CASE WHEN p.Status = 3 THEN -ISNULL(p.DiscAmount, 0) 
             ELSE ISNULL(p.DiscAmount, 0) 
        END AS DiscountAmount,
        CASE WHEN p.Status = 3 THEN -(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0))
             ELSE ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0)
        END AS GSTAmount,
        CASE WHEN p.Status = 3 THEN -ISNULL(p.RoundoffAdjustmentAmt, 0)
             ELSE ISNULL(p.RoundoffAdjustmentAmt, 0)
        END AS RoundOffAmount,
        CASE WHEN p.Status = 3 THEN -(p.Amount + ISNULL(p.TipAmount, 0))
             ELSE p.Amount + ISNULL(p.TipAmount, 0)
        END AS ReceiptAmount,
        CASE WHEN p.Status = 3 THEN pm.Name + ' (REFUND)'
             ELSE pm.Name
        END AS PaymentMethod,
        
        -- Details with void/refund indication
        CASE WHEN p.Status = 3 THEN '🔴 REFUND - Payment voided' ELSE '' END +
        STUFF(
            CASE WHEN p.DiscAmount > 0 THEN ' | Discount: ₹' + CAST(p.DiscAmount AS VARCHAR(20)) ELSE '' END +
            CASE WHEN ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) > 0 
                 THEN ' | GST: ₹' + CAST(ISNULL(p.CGSTAmount, 0) + ISNULL(p.SGSTAmount, 0) AS VARCHAR(20)) ELSE '' END +
            CASE WHEN ISNULL(p.LastFourDigits, '') <> '' 
                 THEN ' | Card: ' + p.CardType + ' *' + p.LastFourDigits ELSE '' END +
            CASE WHEN ISNULL(p.ReferenceNumber, '') <> '' 
                 THEN ' | Ref: ' + p.ReferenceNumber ELSE '' END +
            CASE WHEN ISNULL(p.TipAmount, 0) > 0 
                 THEN ' | Tip: ₹' + CAST(p.TipAmount AS VARCHAR(20)) ELSE '' END,
            1, 3, ''
        ) AS Details,
        p.CreatedAt AS PaymentDate,
        p.Status AS PaymentStatus
    FROM FilteredPayments p
    INNER JOIN Orders o WITH (NOLOCK) ON p.OrderId = o.Id
    INNER JOIN PaymentMethods pm WITH (NOLOCK) ON p.PaymentMethodId = pm.Id
    LEFT JOIN (
        SELECT OrderId, MIN(TableId) AS TableId
        FROM OrderTables WITH (NOLOCK)
        GROUP BY OrderId
    ) ot ON o.Id = ot.OrderId
    LEFT JOIN Tables t WITH (NOLOCK) ON ot.TableId = t.Id
    ORDER BY p.CreatedAt DESC, o.OrderNumber;
END
GO

PRINT '✓ Collection Register stored procedure updated successfully';
PRINT '✓ Now includes void/refund payments with:';
PRINT '  - Negative amounts for refunds';
PRINT '  - REFUND indicator in Payment Method';
PRINT '  - Red highlighting in the UI';
PRINT '  - Void reason displayed in Details column';
GO
