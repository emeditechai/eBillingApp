/*
    Counter Master - Table Structure (end-to-end)
    - Creates dbo.Counters if missing
    - If dbo.Counters exists, ensures columns and defaults exist (including IsActive)
    - Ensures unique index on CounterCode
*/

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Counters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Counters
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Counters PRIMARY KEY,
        CounterCode NVARCHAR(50) NOT NULL,
        CounterName NVARCHAR(120) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Counters_IsActive DEFAULT (1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Counters_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NULL
    );
END
ELSE
BEGIN
    -- Add IsActive if missing
    IF COL_LENGTH('dbo.Counters', 'IsActive') IS NULL
    BEGIN
        ALTER TABLE dbo.Counters ADD IsActive BIT NULL;
        UPDATE dbo.Counters SET IsActive = 1 WHERE IsActive IS NULL;
        ALTER TABLE dbo.Counters ALTER COLUMN IsActive BIT NOT NULL;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Counters')
              AND c.name = N'IsActive'
        )
        BEGIN
            ALTER TABLE dbo.Counters ADD CONSTRAINT DF_Counters_IsActive DEFAULT (1) FOR IsActive;
        END
    END

    -- Add CreatedAt if missing
    IF COL_LENGTH('dbo.Counters', 'CreatedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Counters ADD CreatedAt DATETIME2(3) NULL;
        UPDATE dbo.Counters SET CreatedAt = SYSUTCDATETIME() WHERE CreatedAt IS NULL;
        ALTER TABLE dbo.Counters ALTER COLUMN CreatedAt DATETIME2(3) NOT NULL;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Counters')
              AND c.name = N'CreatedAt'
        )
        BEGIN
            ALTER TABLE dbo.Counters ADD CONSTRAINT DF_Counters_CreatedAt DEFAULT SYSUTCDATETIME() FOR CreatedAt;
        END
    END

    -- Add UpdatedAt if missing
    IF COL_LENGTH('dbo.Counters', 'UpdatedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Counters ADD UpdatedAt DATETIME2(3) NULL;
    END
END

-- Ensure unique index on CounterCode
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Counters_CounterCode'
      AND object_id = OBJECT_ID(N'dbo.Counters')
)
BEGIN
    CREATE UNIQUE INDEX UX_Counters_CounterCode
    ON dbo.Counters (CounterCode);
END

COMMIT TRANSACTION;
GO
