/*
  2026-02-12: Defer OrderNumber assignment until the first menu item is saved.

  Goal
  - Prevent consuming OrderNumber for abandoned orders (order header created but no items added).

  New behavior
  - usp_CreateOrder inserts Orders row with OrderNumber = '' (blank).
  - First item save assigns OrderNumber in format: ORD-YYYYMMDD-XXXX.

  Notes
  - Orders.OrderNumber is NOT NULL in this project; blank string is used to represent "not assigned yet".
  - Concurrency: uses UPDLOCK/HOLDLOCK during sequence selection.
*/

-- =============================
-- usp_CreateOrder
-- =============================
IF OBJECT_ID('dbo.usp_CreateOrder', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_CreateOrder;
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
    DECLARE @OrderNumber NVARCHAR(20) = '';
    DECLARE @Message NVARCHAR(500);

    BEGIN TRY
        BEGIN TRANSACTION;

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
            0,
            ISNULL(@OrderByUserId, @UserId),
            ISNULL(@OrderByUserId, @UserId),
            @OrderByUserName,
            @CustomerName,
            @CustomerPhone,
            @CustomerEmailId,
            @SpecialInstructions,
            0,0,0,0,0,
            GETDATE(),
            GETDATE()
        );

        SET @OrderId = SCOPE_IDENTITY();
        SET @Message = 'Order created successfully.';

        COMMIT TRANSACTION;

        SELECT @OrderId AS OrderId, @OrderNumber AS OrderNumber, @Message AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Message = ERROR_MESSAGE();
        SELECT 0 AS OrderId, '' AS OrderNumber, @Message AS Message;
    END CATCH
END
GO

-- =============================
-- usp_AddOrderItem
-- =============================
IF OBJECT_ID('dbo.usp_AddOrderItem', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.usp_AddOrderItem;
END
GO

CREATE PROCEDURE [dbo].[usp_AddOrderItem]
    @OrderId INT,
    @MenuItemId INT,
    @Quantity INT,
    @SpecialInstructions NVARCHAR(500) = NULL,
    @CourseId INT = NULL,
    @ModifierIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UnitPrice DECIMAL(10, 2);
    DECLARE @Subtotal DECIMAL(10, 2);
    DECLARE @OrderItemId INT;
    DECLARE @Message NVARCHAR(200);
    DECLARE @OrderNumber NVARCHAR(20);

    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId)
    BEGIN
        SELECT 0 AS OrderItemId, 'Order does not exist.' AS [Message], '' AS OrderNumber;
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE Id = @MenuItemId AND IsAvailable = 1)
    BEGIN
        SELECT 0 AS OrderItemId, 'Menu item does not exist or is not available.' AS [Message], '' AS OrderNumber;
        RETURN;
    END

    DECLARE @OrderType INT;
    SELECT @OrderType = OrderType FROM dbo.Orders WHERE Id = @OrderId;

    SELECT @UnitPrice = CASE
        WHEN @OrderType = 1 THEN ISNULL(TakeoutPrice, Price)
        WHEN @OrderType IN (2, 3) THEN ISNULL(DeliveryPrice, Price)
        ELSE Price
    END
    FROM dbo.MenuItems WHERE Id = @MenuItemId;

    SET @Subtotal = @UnitPrice * @Quantity;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Assign OrderNumber on first item add
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

        INSERT INTO dbo.OrderItems (
            OrderId, MenuItemId, Quantity, UnitPrice, Subtotal, SpecialInstructions, CourseId, CreatedAt, UpdatedAt
        )
        VALUES (
            @OrderId, @MenuItemId, @Quantity, @UnitPrice, @Subtotal, @SpecialInstructions, @CourseId, GETDATE(), GETDATE()
        );

        SET @OrderItemId = SCOPE_IDENTITY();

        IF @ModifierIds IS NOT NULL AND LEN(@ModifierIds) > 0
        BEGIN
            DECLARE @ModifierTable TABLE (ModifierId INT);
            INSERT INTO @ModifierTable (ModifierId)
            SELECT CAST(value AS INT)
            FROM STRING_SPLIT(@ModifierIds, ',')
            WHERE RTRIM(value) <> '';

            INSERT INTO dbo.OrderItemModifiers (OrderItemId, ModifierId, Price)
            SELECT @OrderItemId, mt.ModifierId, ISNULL(m.Price, 0)
            FROM @ModifierTable mt
            INNER JOIN dbo.MenuItem_Modifiers mim ON mim.ModifierId = mt.ModifierId AND mim.MenuItemId = @MenuItemId
            INNER JOIN dbo.Modifiers m ON m.Id = mt.ModifierId;

            UPDATE dbo.OrderItems
            SET Subtotal = Subtotal + (
                SELECT ISNULL(SUM(Price), 0) * @Quantity
                FROM dbo.OrderItemModifiers
                WHERE OrderItemId = @OrderItemId
            )
            WHERE Id = @OrderItemId;
        END

        -- (Totals are recalculated by application logic in this project)

        COMMIT TRANSACTION;

        SET @Message = 'Item added to order successfully.';
        SELECT @OrderItemId AS OrderItemId, @Message AS [Message], @OrderNumber AS OrderNumber;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Message = 'Error adding item to order: ' + ERROR_MESSAGE();
        SELECT 0 AS OrderItemId, @Message AS [Message], ISNULL(@OrderNumber,'') AS OrderNumber;
    END CATCH
END
GO
