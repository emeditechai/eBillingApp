-- Alter UpdateKitchenTicketItemStatus so that cancelling a ticket item
-- (Status = 4 at Kitchen) does NOT cancel the linked OrderItem.
-- Instead, revert the OrderItem.Status back to NEW (0), so it reappears
-- in Order Details with the red cross and billing remains unaffected
-- until the cashier cancels from Order Details.

IF OBJECT_ID('dbo.UpdateKitchenTicketItemStatus','P') IS NOT NULL
BEGIN
    PRINT 'Altering UpdateKitchenTicketItemStatus: map Cancelled -> OrderItems.Status = 0 (NEW)';
    EXEC (N'ALTER PROCEDURE dbo.UpdateKitchenTicketItemStatus
        @ItemId INT,
        @Status INT
    AS
    BEGIN
        DECLARE @TicketId INT;
        DECLARE @AllItemsReady BIT = 0;
        
        SELECT @TicketId = KitchenTicketId
        FROM KitchenTicketItems
        WHERE Id = @ItemId;
        
        BEGIN TRANSACTION;
        BEGIN TRY
            -- Update ticket item status and timestamps
            UPDATE KitchenTicketItems
            SET 
                Status = @Status,
                StartTime = CASE WHEN @Status >= 1 AND StartTime IS NULL THEN GETDATE() ELSE StartTime END,
                CompletionTime = CASE WHEN @Status >= 2 AND CompletionTime IS NULL THEN GETDATE() ELSE CompletionTime END
            WHERE Id = @ItemId;

            -- Map to OrderItems: Delivered -> 3, Cancelled -> 0 (revert to NEW)
            IF @Status IN (3,4)
            BEGIN
                UPDATE oi
                SET oi.Status = CASE WHEN @Status = 3 THEN 3 ELSE 0 END
                FROM OrderItems oi
                INNER JOIN KitchenTicketItems kti ON oi.Id = kti.OrderItemId
                WHERE kti.Id = @ItemId;
            END

            -- If item reached Ready or higher, check if all are ready
            IF @Status >= 2 AND NOT EXISTS (
                SELECT 1 FROM KitchenTicketItems WHERE KitchenTicketId = @TicketId AND Status < 2
            )
            BEGIN
                SET @AllItemsReady = 1;
            END

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
    END');
END
ELSE
BEGIN
    PRINT 'Procedure UpdateKitchenTicketItemStatus not found; no changes applied.';
END
