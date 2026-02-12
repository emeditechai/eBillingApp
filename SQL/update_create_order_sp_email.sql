-- Update usp_CreateOrder stored procedure to accept CustomerEmailId parameter
-- Safe implementation: Adds email support to order creation

-- Check if the stored procedure exists
IF OBJECT_ID('usp_CreateOrder', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE usp_CreateOrder;
END
GO

CREATE PROCEDURE [dbo].[usp_CreateOrder]
    @TableTurnoverId INT = NULL,
    @OrderType INT,
    @UserId INT,
    @CustomerName NVARCHAR(100) = NULL,
    @CustomerPhone NVARCHAR(20) = NULL,
    @CustomerEmailId NVARCHAR(100) = NULL,
    @SpecialInstructions NVARCHAR(500) = NULL,
    @OrderByUserId INT = NULL,
    @OrderByUserName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @OrderId INT;
    DECLARE @OrderNumber NVARCHAR(20);
    DECLARE @Message NVARCHAR(500);
    
    BEGIN TRY
        BEGIN TRANSACTION;

        -- OrderNumber is assigned when the first menu item is added (prevents consuming numbers for abandoned orders)
        SET @OrderNumber = '';
        
        -- Insert order
        INSERT INTO Orders (
            OrderNumber,
            TableTurnoverId,
            OrderType,
            Status,
            UserId,
            Order_by_UserID,
            Order_by_UserName,
            CustomerName,
            CustomerPhone,
            Customeremailid,
            SpecialInstructions,
            Subtotal,
            TaxAmount,
            TipAmount,
            DiscountAmount,
            TotalAmount,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @OrderNumber,
            @TableTurnoverId,
            @OrderType,
            0, -- Status: Open
            ISNULL(@OrderByUserId, @UserId),
            ISNULL(@OrderByUserId, @UserId),
            @OrderByUserName,
            @CustomerName,
            @CustomerPhone,
            @CustomerEmailId,
            @SpecialInstructions,
            0, -- Initial Subtotal
            0, -- Initial TaxAmount
            0, -- Initial TipAmount
            0, -- Initial DiscountAmount
            0, -- Initial TotalAmount,
            GETDATE(),
            GETDATE()
        );
        
        SET @OrderId = SCOPE_IDENTITY();
        SET @Message = 'Order created successfully.';
        
        COMMIT TRANSACTION;
        
        -- Return order details
        SELECT @OrderId AS OrderId, @OrderNumber AS OrderNumber, @Message AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SET @Message = ERROR_MESSAGE();
        SELECT 0 AS OrderId, '' AS OrderNumber, @Message AS Message;
    END CATCH
END
GO

PRINT 'usp_CreateOrder stored procedure updated with CustomerEmailId parameter';
