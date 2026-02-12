-- Update usp_AddOrderItem stored procedure to use order type-based pricing
-- This script adds logic to select price based on OrderType:
-- Dine-In (0): Use Price column
-- Takeout (1): Use TakeoutPrice if available, fallback to Price
-- Delivery (2): Use DeliveryPrice if available, fallback to Price
-- Online (3): Use DeliveryPrice if available, fallback to Price

USE [RestaurantManagement]
GO

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_AddOrderItem')
    DROP PROCEDURE usp_AddOrderItem
GO

CREATE PROCEDURE [dbo].[usp_AddOrderItem]
    @OrderId INT,
    @MenuItemId INT,
    @Quantity INT,
    @SpecialInstructions NVARCHAR(500) = NULL,
    @CourseId INT = NULL,
    @ModifierIds NVARCHAR(MAX) = NULL -- Comma-separated list of modifier IDs
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UnitPrice DECIMAL(10, 2);
    DECLARE @Subtotal DECIMAL(10, 2);
    DECLARE @OrderItemId INT;
    DECLARE @Message NVARCHAR(200);
    DECLARE @OrderNumber NVARCHAR(20);
    
    -- Check if order exists
    IF NOT EXISTS (SELECT 1 FROM [Orders] WHERE [Id] = @OrderId)
    BEGIN
        SELECT 'Order does not exist.' AS [Message];
        RETURN;
    END
    
    -- Check if menu item exists and get price
    IF NOT EXISTS (SELECT 1 FROM [MenuItems] WHERE [Id] = @MenuItemId AND [IsAvailable] = 1)
    BEGIN
        SELECT 'Menu item does not exist or is not available.' AS [Message];
        RETURN;
    END
    
    -- Get menu item price based on order type
    -- OrderType: 0=Dine-In, 1=Takeout, 2=Delivery, 3=Online
    DECLARE @OrderType INT;
    SELECT @OrderType = [OrderType] FROM [Orders] WHERE [Id] = @OrderId;
    
    -- Select price based on order type
    -- Dine-In (0): Use Price column
    -- Takeout (1): Use TakeoutPrice if available, fallback to Price
    -- Delivery (2): Use DeliveryPrice if available, fallback to Price
    -- Online (3): Use DeliveryPrice if available, fallback to Price
    SELECT @UnitPrice = CASE 
        WHEN @OrderType = 1 THEN ISNULL([TakeoutPrice], [Price])  -- Takeout
        WHEN @OrderType IN (2, 3) THEN ISNULL([DeliveryPrice], [Price])  -- Delivery or Online
        ELSE [Price]  -- Dine-In (0) or default
    END
    FROM [MenuItems] WHERE [Id] = @MenuItemId;
    
    -- Calculate subtotal
    SET @Subtotal = @UnitPrice * @Quantity;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Assign OrderNumber on first item add (Orders.OrderNumber is NOT NULL; blank means "not assigned yet")
        SELECT @OrderNumber = o.OrderNumber
        FROM dbo.Orders o WITH (UPDLOCK, HOLDLOCK)
        WHERE o.Id = @OrderId;

        IF (@OrderNumber IS NULL OR LTRIM(RTRIM(@OrderNumber)) = '')
        BEGIN
            DECLARE @Today VARCHAR(8) = CONVERT(VARCHAR(8), GETDATE(), 112);
            DECLARE @OrderCount INT;

            SELECT @OrderCount = ISNULL(MAX(CAST(RIGHT(OrderNumber, 4) AS INT)), 0) + 1
            FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
            WHERE OrderNumber LIKE 'ORD-' + @Today + '-%';

            SET @OrderNumber = 'ORD-' + @Today + '-' + RIGHT('0000' + CAST(@OrderCount AS VARCHAR(4)), 4);

            UPDATE dbo.Orders
            SET OrderNumber = @OrderNumber,
                UpdatedAt = GETDATE()
            WHERE Id = @OrderId;
        END

        -- Add order item
        INSERT INTO [OrderItems] (
            [OrderId],
            [MenuItemId],
            [Quantity],
            [UnitPrice],
            [Subtotal],
            [SpecialInstructions],
            [CourseId],
            [CreatedAt],
            [UpdatedAt]
        ) VALUES (
            @OrderId,
            @MenuItemId,
            @Quantity,
            @UnitPrice,
            @Subtotal,
            @SpecialInstructions,
            @CourseId,
            GETDATE(),
            GETDATE()
        );
        
        SET @OrderItemId = SCOPE_IDENTITY();
        
        -- Add modifiers if any were provided
        IF @ModifierIds IS NOT NULL AND LEN(@ModifierIds) > 0
        BEGIN
            -- Create a temporary table to hold modifier IDs
            DECLARE @ModifierTable TABLE (ModifierId INT);
            
            -- Split the comma-separated list and insert into temp table
            INSERT INTO @ModifierTable (ModifierId)
            SELECT CAST(value AS INT)
            FROM STRING_SPLIT(@ModifierIds, ',')
            WHERE RTRIM(value) <> '';
            
            -- Insert modifiers for this order item
            INSERT INTO [OrderItemModifiers] (
                [OrderItemId],
                [ModifierId],
                [Price]
            )
            SELECT 
                @OrderItemId,
                mt.ModifierId,
                ISNULL(m.Price, 0)
            FROM @ModifierTable mt
            INNER JOIN [MenuItem_Modifiers] mim ON mim.ModifierId = mt.ModifierId AND mim.MenuItemId = @MenuItemId
            INNER JOIN [Modifiers] m ON m.Id = mt.ModifierId;
            
            -- Update the subtotal to include modifier prices
            UPDATE [OrderItems]
            SET [Subtotal] = [Subtotal] + (
                SELECT ISNULL(SUM(Price), 0) * @Quantity
                FROM [OrderItemModifiers]
                WHERE [OrderItemId] = @OrderItemId
            )
            WHERE [Id] = @OrderItemId;
        END
        
        -- Update order totals
        DECLARE @NewSubtotal DECIMAL(10, 2);
        DECLARE @TaxRate DECIMAL(5, 2) = 0; -- Assuming no tax for now, modify as needed
        DECLARE @TaxAmount DECIMAL(10, 2);
        DECLARE @Total DECIMAL(10, 2);
        
        -- Calculate new order subtotal
        SELECT @NewSubtotal = SUM([Subtotal])
        FROM [OrderItems]
        WHERE [OrderId] = @OrderId;
        
        -- Calculate tax and total
        SET @TaxAmount = @NewSubtotal * (@TaxRate / 100);
        SET @Total = @NewSubtotal + @TaxAmount;
        
        -- Update order with new totals
        UPDATE [Orders]
        SET 
            [Subtotal] = @NewSubtotal,
            [TaxAmount] = @TaxAmount,
            [TotalAmount] = @Total,
            [UpdatedAt] = GETDATE()
        WHERE [Id] = @OrderId;
        
        COMMIT TRANSACTION;
        
        SET @Message = 'Item added to order successfully.';
        SELECT @OrderItemId AS OrderItemId, @Message AS [Message], @OrderNumber AS OrderNumber;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        SET @Message = 'Error adding item to order: ' + ERROR_MESSAGE();
        SELECT 0 AS OrderItemId, @Message AS [Message];
    END CATCH
END
GO

PRINT 'usp_AddOrderItem stored procedure updated successfully with order type-based pricing logic.'
