-- Create Email Log Table to track all email sending attempts
-- This table stores both successful and failed email attempts with detailed information

USE dev_Restaurant;
GO

-- Create the EmailLog table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tbl_EmailLog')
BEGIN
    CREATE TABLE tbl_EmailLog (
        EmailLogID INT IDENTITY(1,1) PRIMARY KEY,
        
        -- Email Details
        FromEmail NVARCHAR(255) NOT NULL,
        ToEmail NVARCHAR(255) NOT NULL,
        Subject NVARCHAR(500) NULL,
        EmailBody NVARCHAR(MAX) NULL,
        
        -- SMTP Configuration Used
        SmtpServer NVARCHAR(255) NOT NULL,
        SmtpPort INT NOT NULL,
        EnableSSL BIT NOT NULL DEFAULT 1,
        SmtpUsername NVARCHAR(255) NOT NULL,
        
        -- Status and Error Information
        Status NVARCHAR(20) NOT NULL CHECK (Status IN ('Success', 'Failed')),
        ErrorMessage NVARCHAR(MAX) NULL,
        ErrorCode NVARCHAR(50) NULL,
        
        -- Metadata
        SentAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        ProcessingTimeMs INT NULL, -- Time taken to send email in milliseconds
        IPAddress NVARCHAR(50) NULL,
        UserAgent NVARCHAR(500) NULL,
        
        -- Audit Fields
        CreatedBy INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        
        CONSTRAINT FK_EmailLog_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES Users(Id)
    );
    
    PRINT 'Table tbl_EmailLog created successfully';
END
ELSE
BEGIN
    PRINT 'Table tbl_EmailLog already exists';
END
GO

-- Create indexes for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailLog_Status_SentAt' AND object_id = OBJECT_ID('tbl_EmailLog'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailLog_Status_SentAt 
    ON tbl_EmailLog(Status, SentAt DESC);
    PRINT 'Index IX_EmailLog_Status_SentAt created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailLog_ToEmail' AND object_id = OBJECT_ID('tbl_EmailLog'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailLog_ToEmail 
    ON tbl_EmailLog(ToEmail);
    PRINT 'Index IX_EmailLog_ToEmail created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_EmailLog_SentAt' AND object_id = OBJECT_ID('tbl_EmailLog'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailLog_SentAt 
    ON tbl_EmailLog(SentAt DESC);
    PRINT 'Index IX_EmailLog_SentAt created';
END
GO

-- Verify the table structure
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'tbl_EmailLog'
ORDER BY ORDINAL_POSITION;
GO

PRINT 'Email Log table setup completed successfully';
