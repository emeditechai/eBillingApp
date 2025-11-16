-- Alter VoidBOT so voiding a BOT returns linked OrderItems to NEW (Status=0) instead of leaving them fired/cancelled.
-- This keeps Bar (BOT) orders consistent with Foods order ticket cancel behavior.
-- Safe: only runs if procedure exists; only updates OrderItems that are not already cancelled (Status=5).

IF OBJECT_ID('dbo.VoidBOT','P') IS NOT NULL
BEGIN
    PRINT 'Altering VoidBOT to revert OrderItems to NEW on void...';
    EXEC (N'ALTER PROCEDURE dbo.VoidBOT
        @BOT_ID INT,
        @Reason VARCHAR(500),
        @VoidedBy VARCHAR(200)
    AS
    BEGIN
        SET NOCOUNT ON;
        DECLARE @BOT_No VARCHAR(50);
        DECLARE @OldStatus INT;
        DECLARE @OrderId INT;

        SELECT @BOT_No = BOT_No, @OldStatus = Status, @OrderId = OrderId
        FROM dbo.BOT_Header
        WHERE BOT_ID = @BOT_ID;

        UPDATE dbo.BOT_Header
        SET Status = 4,
            VoidedAt = GETDATE(),
            VoidReason = @Reason,
            UpdatedBy = @VoidedBy,
            UpdatedAt = GETDATE()
        WHERE BOT_ID = @BOT_ID;

        -- Revert related OrderItems to NEW so they can be re-fired or cancelled individually
        UPDATE oi
        SET oi.Status = 0
        FROM OrderItems oi
        INNER JOIN BOT_Detail bd ON oi.Id = bd.OrderItemId
        WHERE bd.BOT_ID = @BOT_ID
          AND ISNULL(oi.Status,0) <> 5; -- do not touch already cancelled items

        -- Audit
        INSERT INTO dbo.BOT_Audit (BOT_ID, BOT_No, Action, OldStatus, NewStatus, UserName, Reason, Timestamp)
        VALUES (@BOT_ID, @BOT_No, ''VOID'', @OldStatus, 4, @VoidedBy, @Reason, GETDATE());
    END');
END
ELSE
BEGIN
    PRINT 'VoidBOT procedure not found; no changes applied.';
END
