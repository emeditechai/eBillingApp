-- Audit Trail Tables for Order Flow Tracking
-- This script creates comprehensive audit trail functionality for tracking order changes

-- Create OrderAuditTrail table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderAuditTrail')
BEGIN
    CREATE TABLE OrderAuditTrail (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        OrderId INT NOT NULL,
        OrderNumber NVARCHAR(50),
        Action NVARCHAR(100) NOT NULL,
        EntityType NVARCHAR(50) NOT NULL, -- Order, OrderItem, Payment, etc.
        EntityId INT NULL,
        FieldName NVARCHAR(100) NULL,
        OldValue NVARCHAR(MAX) NULL,
        NewValue NVARCHAR(MAX) NULL,
        ChangedBy INT NOT NULL, -- UserId
        ChangedByName NVARCHAR(200),
        ChangedDate DATETIME NOT NULL DEFAULT GETDATE(),
        IPAddress NVARCHAR(50) NULL,
        UserAgent NVARCHAR(500) NULL,
        AdditionalInfo NVARCHAR(MAX) NULL,
        FOREIGN KEY (OrderId) REFERENCES Orders(Id),
        FOREIGN KEY (ChangedBy) REFERENCES Users(Id)
    );
    
    CREATE INDEX IX_OrderAuditTrail_OrderId ON OrderAuditTrail(OrderId);
    CREATE INDEX IX_OrderAuditTrail_ChangedDate ON OrderAuditTrail(ChangedDate DESC);
    CREATE INDEX IX_OrderAuditTrail_ChangedBy ON OrderAuditTrail(ChangedBy);
    CREATE INDEX IX_OrderAuditTrail_EntityType ON OrderAuditTrail(EntityType);
    
    PRINT 'OrderAuditTrail table created successfully';
END
ELSE
BEGIN
    PRINT 'OrderAuditTrail table already exists';
END
GO

-- Create stored procedure to log audit entries
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_LogOrderAudit')
    DROP PROCEDURE usp_LogOrderAudit
GO

CREATE PROCEDURE usp_LogOrderAudit
    @OrderId INT,
    @OrderNumber NVARCHAR(50),
    @Action NVARCHAR(100),
    @EntityType NVARCHAR(50),
    @EntityId INT = NULL,
    @FieldName NVARCHAR(100) = NULL,
    @OldValue NVARCHAR(MAX) = NULL,
    @NewValue NVARCHAR(MAX) = NULL,
    @ChangedBy INT,
    @ChangedByName NVARCHAR(200),
    @IPAddress NVARCHAR(50) = NULL,
    @UserAgent NVARCHAR(500) = NULL,
    @AdditionalInfo NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO OrderAuditTrail (
        OrderId, OrderNumber, Action, EntityType, EntityId,
        FieldName, OldValue, NewValue, ChangedBy, ChangedByName,
        ChangedDate, IPAddress, UserAgent, AdditionalInfo
    )
    VALUES (
        @OrderId, @OrderNumber, @Action, @EntityType, @EntityId,
        @FieldName, @OldValue, @NewValue, @ChangedBy, @ChangedByName,
        GETDATE(), @IPAddress, @UserAgent, @AdditionalInfo
    );
END
GO

-- Create stored procedure to get audit trail for an order
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_GetOrderAuditTrail')
    DROP PROCEDURE usp_GetOrderAuditTrail
GO

CREATE PROCEDURE usp_GetOrderAuditTrail
    @OrderId INT = NULL,
    @StartDate DATETIME = NULL,
    @EndDate DATETIME = NULL,
    @UserId INT = NULL,
    @EntityType NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    -- Get total count
    SELECT COUNT(*) AS TotalRecords
    FROM OrderAuditTrail
    WHERE (@OrderId IS NULL OR OrderId = @OrderId)
        AND (@StartDate IS NULL OR ChangedDate >= @StartDate)
        AND (@EndDate IS NULL OR ChangedDate <= @EndDate)
        AND (@UserId IS NULL OR ChangedBy = @UserId)
        AND (@EntityType IS NULL OR EntityType = @EntityType);
    
    -- Get paginated results
    SELECT 
        Id,
        OrderId,
        OrderNumber,
        Action,
        EntityType,
        EntityId,
        FieldName,
        OldValue,
        NewValue,
        ChangedBy,
        ChangedByName,
        ChangedDate,
        IPAddress,
        UserAgent,
        AdditionalInfo
    FROM OrderAuditTrail
    WHERE (@OrderId IS NULL OR OrderId = @OrderId)
        AND (@StartDate IS NULL OR ChangedDate >= @StartDate)
        AND (@EndDate IS NULL OR ChangedDate <= @EndDate)
        AND (@UserId IS NULL OR ChangedBy = @UserId)
        AND (@EntityType IS NULL OR EntityType = @EntityType)
    ORDER BY ChangedDate DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

PRINT 'Audit Trail stored procedures created successfully';
