/*
    Kitchen Item Comments Table
    -------------------------------------
    Tracks per-item communication between kitchen staff and wait staff.
    Safe to run multiple times – checks for table existence.
*/
IF OBJECT_ID('dbo.KitchenItemComments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.KitchenItemComments
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId INT NOT NULL,
        OrderItemId INT NOT NULL,
        KitchenTicketId INT NULL,
        KitchenTicketItemId INT NULL,
        CommentText NVARCHAR(1000) NOT NULL,
        CreatedByUserId INT NULL,
        CreatedByName NVARCHAR(150) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_KitchenItemComments_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_KitchenItemComments_OrderItemId
        ON dbo.KitchenItemComments (OrderItemId);

    CREATE INDEX IX_KitchenItemComments_KitchenTicketItemId
        ON dbo.KitchenItemComments (KitchenTicketItemId);
END
GO
