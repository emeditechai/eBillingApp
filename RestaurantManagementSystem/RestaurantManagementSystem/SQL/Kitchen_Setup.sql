/*
    Kitchen Management Setup Script
    UC-005: Kitchen Management
*/

-- Create KitchenStations Table
CREATE TABLE KitchenStations (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(50) NOT NULL,
    Description NVARCHAR(200) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

-- Create MenuItemKitchenStation junction table
CREATE TABLE MenuItemKitchenStations (
    Id INT PRIMARY KEY IDENTITY(1,1),
    MenuItemId INT NOT NULL,
    KitchenStationId INT NOT NULL,
    IsPrimary BIT NOT NULL DEFAULT 1, -- Primary preparation station for the item
    CONSTRAINT FK_MenuItemKitchenStation_MenuItem FOREIGN KEY (MenuItemId) REFERENCES MenuItems(Id),
    CONSTRAINT FK_MenuItemKitchenStation_KitchenStation FOREIGN KEY (KitchenStationId) REFERENCES KitchenStations(Id)
);

-- Create KitchenTickets Table
CREATE TABLE KitchenTickets (
    Id INT PRIMARY KEY IDENTITY(1,1),
    TicketNumber NVARCHAR(20) NOT NULL,
    OrderId INT NOT NULL,
    OrderNumber NVARCHAR(20) NOT NULL,
    KitchenStationId INT NULL,
    StationName NVARCHAR(50) NULL,
    TableName NVARCHAR(50) NULL,
    Status INT NOT NULL DEFAULT 0, -- 0=New, 1=In Progress, 2=Ready, 3=Delivered, 4=Cancelled
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CompletedAt DATETIME NULL,
    CONSTRAINT FK_KitchenTicket_Order FOREIGN KEY (OrderId) REFERENCES Orders(Id),
    CONSTRAINT FK_KitchenTicket_KitchenStation FOREIGN KEY (KitchenStationId) REFERENCES KitchenStations(Id)
);

-- Create KitchenTicketItems Table
CREATE TABLE KitchenTicketItems (
    Id INT PRIMARY KEY IDENTITY(1,1),
    KitchenTicketId INT NOT NULL,
    OrderItemId INT NOT NULL,
    MenuItemName NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL DEFAULT 1,
    SpecialInstructions NVARCHAR(500) NULL,
    Status INT NOT NULL DEFAULT 0, -- 0=New, 1=In Progress, 2=Ready, 3=Delivered, 4=Cancelled
    StartTime DATETIME NULL,
    CompletionTime DATETIME NULL,
    Notes NVARCHAR(500) NULL,
    KitchenStationId INT NULL,
    StationName NVARCHAR(50) NULL,
    PrepTime INT NOT NULL DEFAULT 10, -- Preparation time in minutes
    CONSTRAINT FK_KitchenTicketItem_KitchenTicket FOREIGN KEY (KitchenTicketId) REFERENCES KitchenTickets(Id),
    CONSTRAINT FK_KitchenTicketItem_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItems(Id)
);

-- Create KitchenTicketItemModifiers Table
CREATE TABLE KitchenTicketItemModifiers (
    Id INT PRIMARY KEY IDENTITY(1,1),
    KitchenTicketItemId INT NOT NULL,
    ModifierText NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_KitchenTicketItemModifier_KitchenTicketItem FOREIGN KEY (KitchenTicketItemId) REFERENCES KitchenTicketItems(Id)
);

GO

-- Stored Procedure to get all kitchen stations
CREATE PROCEDURE GetAllKitchenStations
AS
BEGIN
    SELECT 
        Id, 
        Name, 
        Description, 
        IsActive, 
        CreatedAt, 
        UpdatedAt
    FROM 
        KitchenStations
    ORDER BY 
        Name;
END;
GO

-- Stored Procedure to get kitchen station by ID
CREATE PROCEDURE GetKitchenStationById
    @StationId INT
AS
BEGIN
    -- Get station details
    SELECT 
        Id, 
        Name, 
        Description, 
        IsActive, 
        CreatedAt, 
        UpdatedAt
    FROM 
        KitchenStations
    WHERE 
        Id = @StationId;
    
    -- Get menu items assigned to this station
    SELECT 
        mks.MenuItemId,
        mi.Name AS MenuItemName,
        c.Name AS CategoryName,
        mks.IsPrimary
    FROM 
        MenuItemKitchenStations mks
    INNER JOIN 
        MenuItems mi ON mks.MenuItemId = mi.Id
    INNER JOIN 
        Categories c ON mi.CategoryId = c.Id
    WHERE 
        mks.KitchenStationId = @StationId
    ORDER BY 
        c.Name, mi.Name;
END;
GO

-- Stored Procedure to create a new kitchen station
CREATE PROCEDURE CreateKitchenStation
    @Name NVARCHAR(50),
    @Description NVARCHAR(200) = NULL,
    @IsActive BIT = 1,
    @StationId INT OUTPUT
AS
BEGIN
    -- Check if station name already exists
    IF EXISTS (SELECT 1 FROM KitchenStations WHERE Name = @Name)
    BEGIN
        SET @StationId = 0;
        RETURN;
    END
    
    INSERT INTO KitchenStations (
        Name,
        Description,
        IsActive,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        @Name,
        @Description,
        @IsActive,
        GETDATE(),
        GETDATE()
    );
    
    SET @StationId = SCOPE_IDENTITY();
END;
GO

-- Stored Procedure to update an existing kitchen station
CREATE PROCEDURE UpdateKitchenStation
    @StationId INT,
    @Name NVARCHAR(50),
    @Description NVARCHAR(200) = NULL,
    @IsActive BIT = 1
AS
BEGIN
    -- Check if station exists
    IF NOT EXISTS (SELECT 1 FROM KitchenStations WHERE Id = @StationId)
    BEGIN
        RETURN;
    END
    
    -- Check if new name conflicts with existing station (but not itself)
    IF EXISTS (SELECT 1 FROM KitchenStations WHERE Name = @Name AND Id <> @StationId)
    BEGIN
        RETURN;
    END
    
    UPDATE KitchenStations
    SET 
        Name = @Name,
        Description = @Description,
        IsActive = @IsActive,
        UpdatedAt = GETDATE()
    WHERE 
        Id = @StationId;
END;
GO

-- Stored Procedure to delete a kitchen station
CREATE PROCEDURE DeleteKitchenStation
    @StationId INT
AS
BEGIN
    -- Check if station has any active tickets
    IF EXISTS (SELECT 1 FROM KitchenTickets WHERE KitchenStationId = @StationId AND Status < 3)
    BEGIN
        RETURN;
    END
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Delete menu item assignments
        DELETE FROM MenuItemKitchenStations
        WHERE KitchenStationId = @StationId;
        
        -- Delete the station
        DELETE FROM KitchenStations
        WHERE Id = @StationId;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Stored Procedure to assign a menu item to a kitchen station
CREATE PROCEDURE AssignMenuItemToKitchenStation
    @StationId INT,
    @MenuItemId INT,
    @IsPrimary BIT = 1
AS
BEGIN
    -- Check if assignment already exists
    IF EXISTS (SELECT 1 FROM MenuItemKitchenStations WHERE KitchenStationId = @StationId AND MenuItemId = @MenuItemId)
    BEGIN
        -- Update existing assignment
        UPDATE MenuItemKitchenStations
        SET IsPrimary = @IsPrimary
        WHERE KitchenStationId = @StationId AND MenuItemId = @MenuItemId;
    END
    ELSE
    BEGIN
        -- Create new assignment
        INSERT INTO MenuItemKitchenStations (
            KitchenStationId,
            MenuItemId,
            IsPrimary
        )
        VALUES (
            @StationId,
            @MenuItemId,
            @IsPrimary
        );
    END
END;
GO

-- Stored Procedure to delete all menu item assignments for a kitchen station
CREATE PROCEDURE DeleteKitchenStationMenuItems
    @StationId INT
AS
BEGIN
    DELETE FROM MenuItemKitchenStations
    WHERE KitchenStationId = @StationId;
END;
GO

-- Stored Procedure to update kitchen tickets for an order
CREATE PROCEDURE UpdateKitchenTicketsForOrder
    @OrderId INT
AS
BEGIN
    DECLARE @OrderNumber NVARCHAR(20);
    DECLARE @TableName NVARCHAR(50) = NULL;
    
    -- Get order information
    SELECT 
        @OrderNumber = OrderNumber,
        @TableName = t.TableName
    FROM 
        Orders o
    LEFT JOIN 
        TableTurnovers tt ON o.TableTurnoverId = tt.Id
    LEFT JOIN 
        Tables t ON tt.TableId = t.Id
    WHERE 
        o.Id = @OrderId;

    -- Ensure we have a non-null OrderNumber. If the Orders row has NULL (migrated DB differences),
    -- try a direct SELECT with NOLOCK and finally fall back to a generated value based on OrderId.
    IF @OrderNumber IS NULL OR LTRIM(RTRIM(@OrderNumber)) = ''
    BEGIN
        SELECT @OrderNumber = OrderNumber FROM Orders WITH (NOLOCK) WHERE Id = @OrderId;
    END

    IF @OrderNumber IS NULL OR LTRIM(RTRIM(@OrderNumber)) = ''
    BEGIN
        -- Fallback: generate an order number from the OrderId to avoid NULL inserts into KitchenTickets
        SET @OrderNumber = 'ORD-' + RIGHT('00000' + CAST(@OrderId AS VARCHAR(10)), 5);
    END
    
    -- Process items by kitchen station
    DECLARE @StationId INT;
    DECLARE @StationName NVARCHAR(50);
    
    -- Get distinct kitchen stations for items in this order
    DECLARE station_cursor CURSOR FOR
    SELECT DISTINCT 
        ks.Id,
        ks.Name
    FROM 
        OrderItems oi
    INNER JOIN 
        MenuItems mi ON oi.MenuItemId = mi.Id
    INNER JOIN 
        MenuItemKitchenStations miks ON mi.Id = miks.MenuItemId
    INNER JOIN 
        KitchenStations ks ON miks.KitchenStationId = ks.Id
    WHERE 
        oi.OrderId = @OrderId
        AND oi.Status < 5 -- Not cancelled
        AND ks.IsActive = 1;
    
    OPEN station_cursor;
    FETCH NEXT FROM station_cursor INTO @StationId, @StationName;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Check if ticket already exists for this order and station
        DECLARE @TicketId INT = NULL;
        
        SELECT @TicketId = Id
        FROM KitchenTickets
        WHERE OrderId = @OrderId AND KitchenStationId = @StationId;
        
        IF @TicketId IS NULL
        BEGIN
            -- Create new ticket for this station
            INSERT INTO KitchenTickets (
                TicketNumber,
                OrderId,
                OrderNumber,
                KitchenStationId,
                StationName,
                TableName,
                Status,
                CreatedAt
            )
            VALUES (
                'KT' + RIGHT('00000' + CAST(@OrderId AS VARCHAR(10)), 5) + '-' + RIGHT('00' + CAST(@StationId AS VARCHAR(10)), 2),
                @OrderId,
                @OrderNumber,
                @StationId,
                @StationName,
                @TableName,
                0, -- New status
                GETDATE()
            );
            
            SET @TicketId = SCOPE_IDENTITY();
        END
        
        -- Process items for this station
        INSERT INTO KitchenTicketItems (
            KitchenTicketId,
            OrderItemId,
            MenuItemName,
            Quantity,
            SpecialInstructions,
            Status,
            KitchenStationId,
            StationName,
            PrepTime
        )
        SELECT 
            @TicketId,
            oi.Id,
            mi.Name,
            oi.Quantity,
            oi.SpecialInstructions,
            0, -- New status
            @StationId,
            @StationName,
            ISNULL(mi.PrepTime, 10) -- Default 10 minutes if not specified
        FROM 
            OrderItems oi
        INNER JOIN 
            MenuItems mi ON oi.MenuItemId = mi.Id
        INNER JOIN 
            MenuItemKitchenStations miks ON mi.Id = miks.MenuItemId
        WHERE 
            oi.OrderId = @OrderId
            AND miks.KitchenStationId = @StationId
            AND oi.Status < 5 -- Not cancelled
            AND NOT EXISTS (
                -- Don't duplicate items already in kitchen ticket
                SELECT 1
                FROM KitchenTicketItems kti
                WHERE kti.OrderItemId = oi.Id AND kti.KitchenTicketId = @TicketId
            );
        
        -- Add modifiers to kitchen ticket items
        INSERT INTO KitchenTicketItemModifiers (
            KitchenTicketItemId,
            ModifierText
        )
        SELECT 
            kti.Id,
            m.Name + ' (' + oimv.Value + ')'
        FROM 
            OrderItems oi
        INNER JOIN 
            OrderItemModifierValues oimv ON oi.Id = oimv.OrderItemId
        INNER JOIN 
            Modifiers m ON oimv.ModifierId = m.Id
        INNER JOIN 
            KitchenTicketItems kti ON oi.Id = kti.OrderItemId
        WHERE 
            oi.OrderId = @OrderId
            AND kti.KitchenTicketId = @TicketId
            AND NOT EXISTS (
                -- Don't duplicate modifiers
                SELECT 1
                FROM KitchenTicketItemModifiers ktim
                WHERE ktim.KitchenTicketItemId = kti.Id AND ktim.ModifierText = m.Name + ' (' + oimv.Value + ')'
            );
        
        FETCH NEXT FROM station_cursor INTO @StationId, @StationName;
    END
    
    CLOSE station_cursor;
    DEALLOCATE station_cursor;
    
    -- Check if there are items with no assigned kitchen station
    -- Create a general ticket for these items
    IF EXISTS (
        SELECT 1
        FROM OrderItems oi
        WHERE oi.OrderId = @OrderId
        AND oi.Status < 5 -- Not cancelled
        AND NOT EXISTS (
            SELECT 1 
            FROM KitchenTicketItems kti
            INNER JOIN KitchenTickets kt ON kti.KitchenTicketId = kt.Id
            WHERE kti.OrderItemId = oi.Id
        )
    )
    BEGIN
        -- Check if general ticket already exists
        DECLARE @GeneralTicketId INT = NULL;
        
        SELECT @GeneralTicketId = Id
        FROM KitchenTickets
        WHERE OrderId = @OrderId AND KitchenStationId IS NULL;
        
        IF @GeneralTicketId IS NULL
        BEGIN
            -- Create general ticket
            INSERT INTO KitchenTickets (
                TicketNumber,
                OrderId,
                OrderNumber,
                KitchenStationId,
                StationName,
                TableName,
                Status,
                CreatedAt
            )
            VALUES (
                'KT' + RIGHT('00000' + CAST(@OrderId AS VARCHAR(10)), 5) + '-GEN',
                @OrderId,
                @OrderNumber,
                NULL, -- No specific station
                'General Kitchen',
                @TableName,
                0, -- New status
                GETDATE()
            );
            
            SET @GeneralTicketId = SCOPE_IDENTITY();
        END
        
        -- Process unassigned items
        INSERT INTO KitchenTicketItems (
            KitchenTicketId,
            OrderItemId,
            MenuItemName,
            Quantity,
            SpecialInstructions,
            Status,
            KitchenStationId,
            StationName,
            PrepTime
        )
        SELECT 
            @GeneralTicketId,
            oi.Id,
            mi.Name,
            oi.Quantity,
            oi.SpecialInstructions,
            0, -- New status
            NULL, -- No specific station
            'General Kitchen',
            ISNULL(mi.PrepTime, 10) -- Default 10 minutes if not specified
        FROM 
            OrderItems oi
        INNER JOIN 
            MenuItems mi ON oi.MenuItemId = mi.Id
        WHERE 
            oi.OrderId = @OrderId
            AND oi.Status < 5 -- Not cancelled
            AND NOT EXISTS (
                -- Only include items not already assigned to a station
                SELECT 1 
                FROM KitchenTicketItems kti
                INNER JOIN KitchenTickets kt ON kti.KitchenTicketId = kt.Id
                WHERE kti.OrderItemId = oi.Id
            );
        
        -- Add modifiers to general kitchen ticket items
        INSERT INTO KitchenTicketItemModifiers (
            KitchenTicketItemId,
            ModifierText
        )
        SELECT 
            kti.Id,
            m.Name + ' (' + oimv.Value + ')'
        FROM 
            OrderItems oi
        INNER JOIN 
            OrderItemModifierValues oimv ON oi.Id = oimv.OrderItemId
        INNER JOIN 
            Modifiers m ON oimv.ModifierId = m.Id
        INNER JOIN 
            KitchenTicketItems kti ON oi.Id = kti.OrderItemId
        WHERE 
            oi.OrderId = @OrderId
            AND kti.KitchenTicketId = @GeneralTicketId
            AND NOT EXISTS (
                -- Don't duplicate modifiers
                SELECT 1
                FROM KitchenTicketItemModifiers ktim
                WHERE ktim.KitchenTicketItemId = kti.Id AND ktim.ModifierText = m.Name + ' (' + oimv.Value + ')'
            );
    END
END;
GO

-- Stored Procedure to get kitchen tickets by status
CREATE PROCEDURE GetKitchenTicketsByStatus
    @Status INT,
    @StationId INT = NULL
AS
BEGIN
    SELECT 
        kt.Id,
        kt.TicketNumber,
        kt.OrderId,
        kt.OrderNumber,
        kt.KitchenStationId,
        kt.StationName,
        kt.TableName,
        kt.Status,
        kt.CreatedAt,
        kt.CompletedAt,
        DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated
    FROM 
        KitchenTickets kt
    WHERE 
        kt.Status = @Status
        AND kt.KitchenStation != 'BAR'  -- Exclude BAR tickets
        AND (@StationId IS NULL OR kt.KitchenStationId = @StationId)
    ORDER BY 
        kt.CreatedAt;
END;
GO

-- Stored Procedure to get filtered kitchen tickets
CREATE PROCEDURE GetFilteredKitchenTickets
    @StationId INT = NULL,
    @Status INT = NULL,
    @DateFrom DATETIME = NULL,
    @DateTo DATETIME = NULL
AS
BEGIN
    SELECT 
        kt.Id,
        kt.TicketNumber,
        kt.OrderId,
        kt.OrderNumber,
        kt.KitchenStationId,
        kt.StationName,
        kt.TableName,
        kt.Status,
        kt.CreatedAt,
        kt.CompletedAt,
        DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated
    FROM 
        KitchenTickets kt
    WHERE 
        kt.KitchenStation != 'BAR'  -- Exclude BAR tickets
        AND (@StationId IS NULL OR kt.KitchenStationId = @StationId)
        AND (@Status IS NULL OR kt.Status = @Status)
        AND (@DateFrom IS NULL OR kt.CreatedAt >= @DateFrom)
        AND (@DateTo IS NULL OR kt.CreatedAt <= @DateTo)
    ORDER BY 
        kt.CreatedAt DESC;
END;
GO

-- Stored Procedure to get kitchen ticket details
CREATE PROCEDURE GetKitchenTicketDetails
    @TicketId INT
AS
BEGIN
    -- Get ticket info
    SELECT 
        kt.Id,
        kt.TicketNumber,
        kt.OrderId,
        kt.OrderNumber,
        kt.KitchenStationId,
        kt.StationName,
        kt.TableName,
        kt.Status,
        kt.CreatedAt,
        kt.CompletedAt,
        DATEDIFF(MINUTE, kt.CreatedAt, GETDATE()) AS MinutesSinceCreated,
        o.Notes AS OrderNotes
    FROM 
        KitchenTickets kt
    LEFT JOIN
        Orders o ON kt.OrderId = o.Id
    WHERE 
        kt.Id = @TicketId;
    
    -- Get ticket items
    SELECT 
        kti.Id,
        kti.KitchenTicketId,
        kti.OrderItemId,
        kti.MenuItemName,
        kti.Quantity,
        kti.SpecialInstructions,
        kti.Status,
        kti.StartTime,
        kti.CompletionTime,
        kti.Notes,
        CASE 
            WHEN kti.StartTime IS NULL THEN 0
            WHEN kti.CompletionTime IS NULL THEN DATEDIFF(MINUTE, kti.StartTime, GETDATE())
            ELSE DATEDIFF(MINUTE, kti.StartTime, kti.CompletionTime)
        END AS MinutesCooking,
        kti.KitchenStationId,
        kti.StationName,
        kti.PrepTime
    FROM 
        KitchenTicketItems kti
    WHERE 
        kti.KitchenTicketId = @TicketId
    ORDER BY 
        kti.Id;
    
    -- Get item modifiers
    SELECT 
        ktim.KitchenTicketItemId,
        ktim.ModifierText
    FROM 
        KitchenTicketItemModifiers ktim
    INNER JOIN
        KitchenTicketItems kti ON ktim.KitchenTicketItemId = kti.Id
    WHERE 
        kti.KitchenTicketId = @TicketId;
END;
GO

-- Stored Procedure to update kitchen ticket status
CREATE PROCEDURE UpdateKitchenTicketStatus
    @TicketId INT,
    @Status INT
AS
BEGIN
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Update the ticket status
        UPDATE KitchenTickets
        SET 
            Status = @Status,
            CompletedAt = CASE WHEN @Status >= 2 THEN GETDATE() ELSE CompletedAt END
        WHERE 
            Id = @TicketId;
        
        -- Update all items to match ticket status if not already completed or cancelled
        UPDATE KitchenTicketItems
        SET 
            Status = @Status,
            StartTime = CASE 
                            WHEN @Status >= 1 AND StartTime IS NULL THEN GETDATE() 
                            ELSE StartTime 
                        END,
            CompletionTime = CASE 
                                WHEN @Status >= 2 AND CompletionTime IS NULL THEN GETDATE() 
                                ELSE CompletionTime 
                             END
        WHERE 
            KitchenTicketId = @TicketId
            AND Status < 3;
        
        -- If marked as delivered/cancelled, update the order items
        IF @Status IN (3, 4) -- Delivered or Cancelled
        BEGIN
            -- Update the order items status
            UPDATE oi
            SET 
                oi.Status = CASE 
                                WHEN @Status = 3 THEN 3 -- Delivered = Completed
                                WHEN @Status = 4 THEN 5 -- Cancelled
                                ELSE oi.Status
                            END
            FROM 
                OrderItems oi
            INNER JOIN 
                KitchenTicketItems kti ON oi.Id = kti.OrderItemId
            WHERE 
                kti.KitchenTicketId = @TicketId
                AND oi.Status < 3;
        END
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Stored Procedure to update kitchen ticket item status
CREATE PROCEDURE UpdateKitchenTicketItemStatus
    @ItemId INT,
    @Status INT
AS
BEGIN
    DECLARE @TicketId INT;
    DECLARE @AllItemsReady BIT = 0;
    
    -- Get the ticket ID for this item
    SELECT @TicketId = KitchenTicketId
    FROM KitchenTicketItems
    WHERE Id = @ItemId;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Update the ticket item status and stamp times when first entering a phase
        UPDATE KitchenTicketItems
        SET 
            Status = @Status,
            StartTime = CASE 
                            WHEN @Status >= 1 AND StartTime IS NULL THEN GETDATE() 
                            ELSE StartTime 
                        END,
            CompletionTime = CASE 
                                WHEN @Status >= 2 AND CompletionTime IS NULL THEN GETDATE() 
                                ELSE CompletionTime 
                             END
        WHERE Id = @ItemId;
        
        -- Delivered (3) or Cancelled (4) actions reflected on OrderItems:
        --  Delivered -> OrderItems.Status = 3 (Completed)
        --  Cancelled at kitchen -> REVERT OrderItems.Status to 0 (New) so cashier can decide.
        IF @Status IN (3, 4)
        BEGIN
            UPDATE oi
            SET oi.Status = CASE 
                                WHEN @Status = 3 THEN 3  -- Delivered
                                WHEN @Status = 4 THEN 0  -- Kitchen cancel reverts to NEW
                                ELSE oi.Status
                            END
            FROM OrderItems oi
            INNER JOIN KitchenTicketItems kti ON oi.Id = kti.OrderItemId
            WHERE kti.Id = @ItemId;
        END
        
        -- Check if all items reached Ready or higher (>=2)
        IF @Status >= 2 AND NOT EXISTS (
            SELECT 1 FROM KitchenTicketItems WHERE KitchenTicketId = @TicketId AND Status < 2
        )
        BEGIN
            SET @AllItemsReady = 1;
        END
        
        -- If all ready, bump ticket to Ready (2) and stamp completion
        IF @AllItemsReady = 1
        BEGIN
            UPDATE KitchenTickets
            SET Status = CASE WHEN Status < 2 THEN 2 ELSE Status END,
                CompletedAt = CASE WHEN CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END
            WHERE Id = @TicketId;
        END
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Stored Procedure to get kitchen dashboard stats
CREATE PROCEDURE GetKitchenDashboardStats
    @StationId INT = NULL
AS
BEGIN
    SELECT
        COUNT(DISTINCT CASE WHEN kt.Status = 0 THEN kt.Id ELSE NULL END) AS NewTicketsCount,
        COUNT(DISTINCT CASE WHEN kt.Status = 1 THEN kt.Id ELSE NULL END) AS InProgressTicketsCount,
        COUNT(DISTINCT CASE WHEN kt.Status = 2 THEN kt.Id ELSE NULL END) AS ReadyTicketsCount,
        SUM(CASE WHEN kti.Status < 2 THEN 1 ELSE 0 END) AS PendingItemsCount,
        SUM(CASE WHEN kti.Status = 2 THEN 1 ELSE 0 END) AS ReadyItemsCount,
        AVG(CASE 
            WHEN kti.CompletionTime IS NOT NULL AND kti.StartTime IS NOT NULL 
            THEN DATEDIFF(MINUTE, kti.StartTime, kti.CompletionTime) 
            ELSE NULL 
        END) AS AvgPrepTimeMinutes
    FROM
        KitchenTickets kt
    LEFT JOIN
        KitchenTicketItems kti ON kt.Id = kti.KitchenTicketId
    WHERE
        kt.Status < 4  -- Not cancelled
        AND kt.KitchenStation != 'BAR'  -- Exclude BAR tickets
        AND (@StationId IS NULL OR kt.KitchenStationId = @StationId);
END;
GO

-- Stored Procedure to mark all tickets as ready
CREATE PROCEDURE MarkAllTicketsReady
    @StationId INT = NULL
AS
BEGIN
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Update all in-progress tickets to ready
        UPDATE kt
        SET 
            Status = 2,
            CompletedAt = GETDATE()
        FROM 
            KitchenTickets kt
        WHERE 
            kt.Status = 1
            AND (@StationId IS NULL OR kt.KitchenStationId = @StationId);
        
        -- Update all in-progress items to ready
        UPDATE kti
        SET 
            Status = 2,
            CompletionTime = GETDATE()
        FROM 
            KitchenTicketItems kti
        INNER JOIN
            KitchenTickets kt ON kti.KitchenTicketId = kt.Id
        WHERE 
            kti.Status = 1
            AND (@StationId IS NULL OR kt.KitchenStationId = @StationId);
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Initialize default kitchen stations
INSERT INTO KitchenStations (Name, Description, IsActive)
VALUES 
('Hot Kitchen', 'For hot cooked items: grills, fryers, ovens', 1),
('Cold Kitchen', 'For salads, sandwiches, and cold appetizers', 1),
('Bar', 'For drinks and bar items', 1),
('Dessert Station', 'For desserts and pastries', 1);
GO
