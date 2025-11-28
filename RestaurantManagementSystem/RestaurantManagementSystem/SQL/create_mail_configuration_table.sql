-- SQL Script to create the Mail Configuration table
-- This table stores SMTP email server configuration settings

SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.tbl_MailConfiguration', N'U') IS NULL
BEGIN
    PRINT 'Creating table dbo.tbl_MailConfiguration...';
    CREATE TABLE dbo.tbl_MailConfiguration
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SmtpServer NVARCHAR(200) NOT NULL,
        SmtpPort INT NOT NULL DEFAULT(587),
        SmtpUsername NVARCHAR(200) NOT NULL,
        SmtpPassword NVARCHAR(500) NOT NULL, -- Encrypted password
        EnableSSL BIT NOT NULL DEFAULT(1),
        FromEmail NVARCHAR(200) NOT NULL,
        FromName NVARCHAR(200) NOT NULL,
        AdminNotificationEmail NVARCHAR(200) NULL,
        IsActive BIT NOT NULL DEFAULT(0), -- Email service activation flag
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy INT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        CONSTRAINT FK_MailConfig_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_MailConfig_UpdatedBy FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(Id)
    );

    CREATE INDEX IX_MailConfiguration_IsActive ON dbo.tbl_MailConfiguration(IsActive);
    
    PRINT 'Table dbo.tbl_MailConfiguration created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.tbl_MailConfiguration already exists.';
END
GO

-- Insert default configuration if table is empty
IF NOT EXISTS (SELECT 1 FROM dbo.tbl_MailConfiguration)
BEGIN
    PRINT 'Inserting default mail configuration...';
    INSERT INTO dbo.tbl_MailConfiguration 
    (
        SmtpServer, 
        SmtpPort, 
        SmtpUsername, 
        SmtpPassword, 
        EnableSSL, 
        FromEmail, 
        FromName, 
        AdminNotificationEmail, 
        IsActive
    )
    VALUES 
    (
        'smtp.gmail.com', 
        587, 
        '', 
        '', 
        1, 
        'noreply@restaurant.com', 
        'Restaurant Management System', 
        'admin@restaurant.com', 
        0
    );
    PRINT 'Default mail configuration inserted.';
END
GO

PRINT 'Mail Configuration table setup completed.';
GO
