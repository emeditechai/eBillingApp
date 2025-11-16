-- Safely alter UpdateKitchenTicketStatus so cancelling a ticket returns its items to NEW (status=0)
-- instead of permanently cancelling them (status=5). This allows items to appear again on Order Details
-- with the red cross cancel option.

IF OBJECT_ID('dbo.UpdateKitchenTicketStatus','P') IS NOT NULL
BEGIN
    PRINT 'Altering UpdateKitchenTicketStatus to revert items to NEW on ticket cancel...';
    EXEC ('
    ALTER PROCEDURE dbo.UpdateKitchenTicketStatus
        @TicketId INT,
        @Status INT
    AS
    BEGIN
        SET NOCOUNT ON;
        BEGIN TRANSACTION;
        BEGIN TRY
            -- Update ticket status
            UPDATE KitchenTickets
            SET Status = @Status,
                CompletedAt = CASE WHEN @Status >= 2 THEN GETDATE() ELSE CompletedAt END
            WHERE Id = @TicketId;

            -- Sync ticket item statuses (do not overwrite already completed or explicitly cancelled items)
            UPDATE kti
            SET Status = @Status,
                StartTime = CASE WHEN @Status >= 1 AND StartTime IS NULL THEN GETDATE() ELSE StartTime END,
                CompletionTime = CASE WHEN @Status >= 2 AND CompletionTime IS NULL THEN GETDATE() ELSE CompletionTime END
            FROM KitchenTicketItems kti
            WHERE kti.KitchenTicketId = @TicketId AND kti.Status < 3; -- keep Completed items as-is

            -- Delivered or Cancelled logic
            IF @Status IN (3,4)
            BEGIN
                -- For Delivered: mark order items completed (3)
                -- For Cancelled: REVERT order items to NEW (0) so they can be re-fired or cancelled in Order Details.
                UPDATE oi
                SET oi.Status = CASE 
                                    WHEN @Status = 3 THEN 3  -- Delivered
                                    WHEN @Status = 4 THEN 0  -- Ticket cancelled -> revert to New
                                    ELSE oi.Status
                                END
                FROM OrderItems oi
                INNER JOIN KitchenTicketItems kti ON oi.Id = kti.OrderItemId
                WHERE kti.KitchenTicketId = @TicketId;
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
    PRINT 'UpdateKitchenTicketStatus procedure not found; no changes applied.';
END
