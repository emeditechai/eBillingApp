-- SQL Script for UC-004: Process Payments

-- Create tables if they don't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PaymentMethods]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PaymentMethods](
        [Id] [int] IDENTITY(1,1) PRIMARY KEY,
        [Name] [nvarchar](50) NOT NULL,
        [DisplayName] [nvarchar](100) NOT NULL,
        [IsActive] [bit] NOT NULL DEFAULT(1),
        [RequiresCardInfo] [bit] NOT NULL DEFAULT(0),
        [RequiresCardPresent] [bit] NOT NULL DEFAULT(0),
        [RequiresApproval] [bit] NOT NULL DEFAULT(0),
        [CreatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        [UpdatedAt] [datetime] NOT NULL DEFAULT(GETDATE())
    )
    
    -- Insert default payment methods
    INSERT INTO [dbo].[PaymentMethods] ([Name], [DisplayName], [RequiresCardInfo], [RequiresCardPresent], [RequiresApproval])
    VALUES
        ('CASH', 'Cash', 0, 0, 0),
        ('CREDIT_CARD', 'Credit Card', 1, 1, 1),
        ('DEBIT_CARD', 'Debit Card', 1, 1, 1),
        ('GIFT_CARD', 'Gift Card', 1, 1, 1),
        ('HOUSE_ACCOUNT', 'House Account', 0, 0, 1),
        ('COMP', 'Complimentary', 0, 0, 1)
END

-- Ensure existing databases get the DiscAmount column on Payments if it's missing
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'DiscAmount' AND Object_ID = OBJECT_ID(N'dbo.Payments'))
    BEGIN
        ALTER TABLE dbo.Payments ADD DiscAmount DECIMAL(18,2) NOT NULL DEFAULT(0);
    END
END

-- Create Payments table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Payments](
        [Id] [int] IDENTITY(1,1) PRIMARY KEY,
        [OrderId] [int] NOT NULL,
        [PaymentMethodId] [int] NOT NULL,
        [Amount] [decimal](18,2) NOT NULL,
        [DiscAmount] [decimal](18,2) NOT NULL DEFAULT(0),
        [TipAmount] [decimal](18,2) NOT NULL DEFAULT(0),
        [Status] [int] NOT NULL DEFAULT(0), -- 0=Pending, 1=Approved, 2=Rejected, 3=Voided
        [ReferenceNumber] [nvarchar](100) NULL,
        [LastFourDigits] [nvarchar](4) NULL,
        [CardType] [nvarchar](50) NULL,
        [AuthorizationCode] [nvarchar](50) NULL,
        [Notes] [nvarchar](500) NULL,
        [ProcessedBy] [int] NULL,
        [ProcessedByName] [nvarchar](100) NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        [UpdatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        
        CONSTRAINT [FK_Payments_Orders] FOREIGN KEY([OrderId]) REFERENCES [dbo].[Orders] ([Id]),
        CONSTRAINT [FK_Payments_PaymentMethods] FOREIGN KEY([PaymentMethodId]) REFERENCES [dbo].[PaymentMethods] ([Id])
    )
END

-- Create SplitBills table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SplitBills]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SplitBills](
        [Id] [int] IDENTITY(1,1) PRIMARY KEY,
        [OrderId] [int] NOT NULL,
        [Amount] [decimal](18,2) NOT NULL,
        [TaxAmount] [decimal](18,2) NOT NULL DEFAULT(0),
        [Status] [int] NOT NULL DEFAULT(0), -- 0=Open, 1=Paid, 2=Voided
        [Notes] [nvarchar](500) NULL,
        [CreatedBy] [int] NULL,
        [CreatedByName] [nvarchar](100) NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        [UpdatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        
        CONSTRAINT [FK_SplitBills_Orders] FOREIGN KEY([OrderId]) REFERENCES [dbo].[Orders] ([Id])
    )
END

-- Create SplitBillItems table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SplitBillItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SplitBillItems](
        [Id] [int] IDENTITY(1,1) PRIMARY KEY,
        [SplitBillId] [int] NOT NULL,
        [OrderItemId] [int] NOT NULL,
        [Quantity] [int] NOT NULL DEFAULT(1),
        [Amount] [decimal](18,2) NOT NULL,
        
        CONSTRAINT [FK_SplitBillItems_SplitBills] FOREIGN KEY([SplitBillId]) REFERENCES [dbo].[SplitBills] ([Id]),
        CONSTRAINT [FK_SplitBillItems_OrderItems] FOREIGN KEY([OrderItemId]) REFERENCES [dbo].[OrderItems] ([Id])
    )
END

-- Create a stored procedure to get order payment information
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
    (o.TotalAmount - ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0)) AS RemainingAmount,
        ISNULL(t.TableName, 'N/A') AS TableName,
        o.Status
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
        o.Status;
    
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
    
    -- Get existing payments
    SELECT
        p.Id,
        p.PaymentMethodId,
        pm.Name AS PaymentMethod,
        pm.DisplayName AS PaymentMethodDisplay,
        p.Amount,
        p.TipAmount,
        p.Status,
        p.ReferenceNumber,
        p.LastFourDigits,
        p.CardType,
        p.AuthorizationCode,
        p.Notes,
        p.ProcessedByName,
        p.CreatedAt
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

-- Create stored procedure to process a payment (GST-aware)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_ProcessPayment]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_ProcessPayment]
GO

CREATE PROCEDURE [dbo].[usp_ProcessPayment]
    @OrderId INT,
    @PaymentMethodId INT,
    @Amount DECIMAL(18,2),
    @TipAmount DECIMAL(18,2) = 0,
    @ReferenceNumber NVARCHAR(100) = NULL,
    @LastFourDigits NVARCHAR(4) = NULL,
    @CardType NVARCHAR(50) = NULL,
    @AuthorizationCode NVARCHAR(50) = NULL,
    @Notes NVARCHAR(500) = NULL,
    @ProcessedBy INT = NULL,
    @ProcessedByName NVARCHAR(100) = NULL,
    @GSTAmount DECIMAL(18,2) = NULL,
    @CGSTAmount DECIMAL(18,2) = NULL,
    @SGSTAmount DECIMAL(18,2) = NULL,
    @DiscAmount DECIMAL(18,2) = NULL,
    @GST_Perc DECIMAL(10,4) = NULL,
    @CGST_Perc DECIMAL(10,4) = NULL,
    @SGST_Perc DECIMAL(10,4) = NULL,
    @Amount_ExclGST DECIMAL(18,2) = NULL
    ,@RoundoffAdjustmentAmt DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @PaymentId INT;
    DECLARE @PaymentStatus INT;
    DECLARE @Message NVARCHAR(500);
    DECLARE @RequiresApproval BIT;

    BEGIN TRY
        -- Validate order exists
        IF NOT EXISTS (SELECT 1 FROM Orders WHERE Id = @OrderId)
        BEGIN
            SELECT 0 AS PaymentId, 0 AS PaymentStatus, 'Order not found' AS Message;
            RETURN;
        END
        
        -- Get payment method details
        SELECT @RequiresApproval = RequiresApproval
        FROM PaymentMethods
        WHERE Id = @PaymentMethodId;
        
        -- Set payment status based on approval requirement
        SET @PaymentStatus = CASE WHEN @RequiresApproval = 1 THEN 0 ELSE 1 END;
        
        -- Insert payment with GST information
        INSERT INTO Payments (
            OrderId,
            PaymentMethodId,
            Amount,
            TipAmount,
            Status,
            ReferenceNumber,
            LastFourDigits,
            CardType,
            AuthorizationCode,
            Notes,
            ProcessedBy,
            ProcessedByName,
            GSTAmount,
            CGSTAmount,
            SGSTAmount,
            DiscAmount,
            GST_Perc,
            CGST_Perc,
            SGST_Perc,
            Amount_ExclGST,
            RoundoffAdjustmentAmt,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @OrderId,
            @PaymentMethodId,
            @Amount,
            @TipAmount,
            @PaymentStatus,
            @ReferenceNumber,
            @LastFourDigits,
            @CardType,
            @AuthorizationCode,
            @Notes,
            @ProcessedBy,
            @ProcessedByName,
            @GSTAmount,
            @CGSTAmount,
            @SGSTAmount,
            @DiscAmount,
            @GST_Perc,
            @CGST_Perc,
            @SGST_Perc,
            @Amount_ExclGST,
            @RoundoffAdjustmentAmt,
            GETDATE(),
            GETDATE()
        );
        
        SET @PaymentId = SCOPE_IDENTITY();
        
        -- Check if order is fully paid
        DECLARE @TotalPaid DECIMAL(18,2);
        DECLARE @OrderTotal DECIMAL(18,2);
        
    SELECT @TotalPaid = ISNULL(SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)), 0)
        FROM Payments
        WHERE OrderId = @OrderId AND Status = 1; -- Approved payments only
        
        SELECT @OrderTotal = TotalAmount
        FROM Orders
        WHERE Id = @OrderId;
        
        -- Update order status if fully paid
        IF @TotalPaid >= @OrderTotal
        BEGIN
            UPDATE Orders
            SET Status = CASE WHEN Status < 3 THEN 3 ELSE Status END, -- Set to Completed if not already completed or cancelled
                CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                UpdatedAt = GETDATE()
            WHERE Id = @OrderId;
        END
        
        SET @Message = CASE 
            WHEN @PaymentStatus = 1 THEN 'Payment processed successfully'
            ELSE 'Payment saved for approval'
        END;
        
        SELECT @PaymentId AS PaymentId, @PaymentStatus AS PaymentStatus, @Message AS Message;
        
    END TRY
    BEGIN CATCH
        SET @Message = 'Error processing payment: ' + ERROR_MESSAGE();
        SELECT 0 AS PaymentId, -1 AS PaymentStatus, @Message AS Message;
    END CATCH
END
GO

-- Create stored procedure to void a payment
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_VoidPayment]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_VoidPayment]
GO

CREATE PROCEDURE [dbo].[usp_VoidPayment]
    @PaymentId INT,
    @Reason NVARCHAR(500),
    @ProcessedBy INT = NULL,
    @ProcessedByName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @OrderId INT;
    DECLARE @PaymentAmount DECIMAL(18,2);
    DECLARE @TipAmount DECIMAL(18,2);
    DECLARE @CurrentStatus INT;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Get payment information (including per-payment discount)
        SELECT 
            @OrderId = OrderId,
            @PaymentAmount = Amount,
            @TipAmount = TipAmount,
            @CurrentStatus = Status
        FROM 
            Payments
        WHERE 
            Id = @PaymentId;
        
        IF @OrderId IS NULL
        BEGIN
            RAISERROR('Payment not found.', 16, 1);
            RETURN;
        END
        
        IF @CurrentStatus = 3 -- Already voided
        BEGIN
            RAISERROR('This payment has already been voided.', 16, 1);
            RETURN;
        END
        
        -- Update payment status to voided (capture DiscAmount before update)
        DECLARE @PaymentDiscAmount DECIMAL(18,2) = 0;
        SELECT @PaymentDiscAmount = ISNULL(DiscAmount, 0) FROM Payments WHERE Id = @PaymentId;

        UPDATE Payments
        SET Status = 3, -- Voided
            Notes = ISNULL(Notes + ' | ', '') + 'VOIDED: ' + @Reason,
            UpdatedAt = GETDATE(),
            DiscAmount = 0 -- clear per-payment discount for voided payment
        WHERE Id = @PaymentId;
        
        -- Update order to reduce tip amount
        IF @TipAmount > 0
        BEGIN
            UPDATE Orders
            SET TipAmount = TipAmount - @TipAmount,
                UpdatedAt = GETDATE()
            WHERE Id = @OrderId;
        END

        -- If the voided payment had a discount amount, subtract it from Orders.DiscountAmount
        IF @PaymentDiscAmount > 0
        BEGIN
            -- Subtract the disc amount and recalculate TaxAmount and TotalAmount based on the order's subtotal
            DECLARE @CurrentDiscount DECIMAL(18,2);
            DECLARE @OrderSubtotal DECIMAL(18,2);
            DECLARE @OrderTip DECIMAL(18,2);
            DECLARE @GSTPerc DECIMAL(10,4) = 0;

            SELECT @CurrentDiscount = ISNULL(DiscountAmount, 0),
                   @OrderSubtotal = Subtotal,
                   @OrderTip = ISNULL(TipAmount, 0)
            FROM Orders WHERE Id = @OrderId;

            -- Get GST percentage from settings if available
            SELECT @GSTPerc = ISNULL(DefaultGSTPercentage, 0) FROM dbo.RestaurantSettings;

            DECLARE @NewDiscount DECIMAL(18,2) = CASE WHEN @CurrentDiscount - @PaymentDiscAmount >= 0 THEN @CurrentDiscount - @PaymentDiscAmount ELSE 0 END;
            DECLARE @NetSubtotal DECIMAL(18,2) = @OrderSubtotal - @NewDiscount;
            IF @NetSubtotal < 0 SET @NetSubtotal = 0;

            DECLARE @NewTaxAmount DECIMAL(18,2) = 0;
            IF @GSTPerc > 0
                SET @NewTaxAmount = ROUND(@NetSubtotal * @GSTPerc / 100.0, 2);

            DECLARE @NewTotalAmount DECIMAL(18,2) = @NetSubtotal + @NewTaxAmount + @OrderTip;

            UPDATE Orders
            SET DiscountAmount = @NewDiscount,
                TaxAmount = @NewTaxAmount,
                TotalAmount = @NewTotalAmount,
                UpdatedAt = GETDATE()
            WHERE Id = @OrderId;
        END
        
                -- Reopen order if needed. Include per-payment RoundoffAdjustmentAmt in sums so
                -- the decision to reopen matches UI/Controller math that includes roundoff.
                UPDATE Orders
                SET Status = 1, -- In Progress
                        CompletedAt = NULL,
                        UpdatedAt = GETDATE()
                WHERE Id = @OrderId
                    AND Status = 3 -- Completed
                    AND (SELECT ISNULL(SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)),0) FROM Payments WHERE OrderId = @OrderId AND Status = 1) < TotalAmount;
        
        COMMIT TRANSACTION;
        
        -- Return success
        SELECT 1 AS Result, 'Payment voided successfully.' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Return error
        SELECT 0 AS Result, ERROR_MESSAGE() AS Message;
    END CATCH;
END
GO

-- Create stored procedure to create a split bill
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[usp_CreateSplitBill]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[usp_CreateSplitBill]
GO

CREATE PROCEDURE [dbo].[usp_CreateSplitBill]
    @OrderId INT,
    @Items NVARCHAR(MAX), -- Format: 'OrderItemId,Quantity,Amount;OrderItemId,Quantity,Amount'
    @Notes NVARCHAR(500) = NULL,
    @CreatedBy INT = NULL,
    @CreatedByName NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @SplitBillId INT;
    DECLARE @TotalAmount DECIMAL(18,2) = 0;
    DECLARE @TaxRate DECIMAL(5,4);
    DECLARE @TaxAmount DECIMAL(18,2) = 0;
    DECLARE @OrderStatus INT;
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Get order status
        SELECT @OrderStatus = Status 
        FROM Orders 
        WHERE Id = @OrderId;
        
        IF @OrderStatus IS NULL
        BEGIN
            RAISERROR('Order not found.', 16, 1);
            RETURN;
        END
        
        IF @OrderStatus = 4 -- Cancelled
        BEGIN
            RAISERROR('Cannot create split bill for a cancelled order.', 16, 1);
            RETURN;
        END
        
        -- Get tax rate (from order tax divided by subtotal)
        SELECT 
            @TaxRate = CASE WHEN Subtotal > 0 THEN TaxAmount / Subtotal ELSE 0 END
        FROM 
            Orders
        WHERE 
            Id = @OrderId;
        
        -- Create split bill
        INSERT INTO SplitBills (
            OrderId,
            Amount,
            TaxAmount,
            Notes,
            CreatedBy,
            CreatedByName
        )
        VALUES (
            @OrderId,
            0, -- Will update after adding items
            0, -- Will update after adding items
            @Notes,
            @CreatedBy,
            @CreatedByName
        );
        
        SET @SplitBillId = SCOPE_IDENTITY();
        
        -- Parse items and add them to the split bill
        DECLARE @Index INT = 1;
        DECLARE @Pos INT = 0;
        DECLARE @NextPos INT = 0;
        DECLARE @ItemData NVARCHAR(100);
        
        WHILE CHARINDEX(';', @Items, @Index) > 0
        BEGIN
            SET @NextPos = CHARINDEX(';', @Items, @Index);
            SET @ItemData = SUBSTRING(@Items, @Index, @NextPos - @Index);
            
            DECLARE @OrderItemId INT;
            DECLARE @Quantity INT;
            DECLARE @Amount DECIMAL(18,2);
            
            -- Parse OrderItemId
            SET @Pos = CHARINDEX(',', @ItemData, 1);
            SET @OrderItemId = CAST(SUBSTRING(@ItemData, 1, @Pos - 1) AS INT);
            
            -- Parse Quantity
            SET @Index = @Pos + 1;
            SET @Pos = CHARINDEX(',', @ItemData, @Index);
            SET @Quantity = CAST(SUBSTRING(@ItemData, @Index, @Pos - @Index) AS INT);
            
            -- Parse Amount
            SET @Index = @Pos + 1;
            SET @Amount = CAST(SUBSTRING(@ItemData, @Index, LEN(@ItemData) - @Index + 1) AS DECIMAL(18,2));
            
            -- Add item to split bill
            INSERT INTO SplitBillItems (
                SplitBillId,
                OrderItemId,
                Quantity,
                Amount
            )
            VALUES (
                @SplitBillId,
                @OrderItemId,
                @Quantity,
                @Amount
            );
            
            SET @TotalAmount = @TotalAmount + @Amount;
            SET @Index = @NextPos + 1;
        END
        
        -- Handle the last item
        IF @Index <= LEN(@Items)
        BEGIN
            SET @ItemData = SUBSTRING(@Items, @Index, LEN(@Items) - @Index + 1);
            
            DECLARE @LastOrderItemId INT;
            DECLARE @LastQuantity INT;
            DECLARE @LastAmount DECIMAL(18,2);
            
            -- Parse OrderItemId
            SET @Pos = CHARINDEX(',', @ItemData, 1);
            SET @LastOrderItemId = CAST(SUBSTRING(@ItemData, 1, @Pos - 1) AS INT);
            
            -- Parse Quantity
            SET @Index = @Pos + 1;
            SET @Pos = CHARINDEX(',', @ItemData, @Index);
            SET @LastQuantity = CAST(SUBSTRING(@ItemData, @Index, @Pos - @Index) AS INT);
            
            -- Parse Amount
            SET @Index = @Pos + 1;
            SET @LastAmount = CAST(SUBSTRING(@ItemData, @Index, LEN(@ItemData) - @Index + 1) AS DECIMAL(18,2));
            
            -- Add item to split bill
            INSERT INTO SplitBillItems (
                SplitBillId,
                OrderItemId,
                Quantity,
                Amount
            )
            VALUES (
                @SplitBillId,
                @LastOrderItemId,
                @LastQuantity,
                @LastAmount
            );
            
            SET @TotalAmount = @TotalAmount + @LastAmount;
        END
        
        -- Calculate tax amount
        SET @TaxAmount = @TotalAmount * @TaxRate;
        
        -- Update split bill with total amount and tax
        UPDATE SplitBills
        SET Amount = @TotalAmount,
            TaxAmount = @TaxAmount
        WHERE Id = @SplitBillId;
        
        COMMIT TRANSACTION;
        
        -- Return split bill ID and amounts
        SELECT 
            @SplitBillId AS SplitBillId, 
            @TotalAmount AS Amount, 
            @TaxAmount AS TaxAmount, 
            (@TotalAmount + @TaxAmount) AS TotalAmount,
            'Split bill created successfully.' AS Message;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        -- Return error
        SELECT 0 AS SplitBillId, 0 AS Amount, 0 AS TaxAmount, 0 AS TotalAmount, ERROR_MESSAGE() AS Message;
    END CATCH;
END
GO
