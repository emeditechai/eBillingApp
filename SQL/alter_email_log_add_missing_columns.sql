-- Add missing columns to tbl_EmailLog table
-- These columns are required by EmailServicesController

USE dev_Restaurant;
GO

-- Add FromName column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'FromName')
BEGIN
    ALTER TABLE tbl_EmailLog ADD FromName NVARCHAR(255) NULL;
    PRINT 'Column FromName added successfully';
END
ELSE
BEGIN
    PRINT 'Column FromName already exists';
END
GO

-- Add Body column (alias for EmailBody for compatibility)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'Body')
BEGIN
    ALTER TABLE tbl_EmailLog ADD Body NVARCHAR(MAX) NULL;
    PRINT 'Column Body added successfully';
END
ELSE
BEGIN
    PRINT 'Column Body already exists';
END
GO

-- Add SmtpUseSsl column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'SmtpUseSsl')
BEGIN
    ALTER TABLE tbl_EmailLog ADD SmtpUseSsl BIT NULL DEFAULT 1;
    PRINT 'Column SmtpUseSsl added successfully';
END
ELSE
BEGIN
    PRINT 'Column SmtpUseSsl already exists';
END
GO

-- Add SmtpTimeout column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'SmtpTimeout')
BEGIN
    ALTER TABLE tbl_EmailLog ADD SmtpTimeout INT NULL DEFAULT 30000;
    PRINT 'Column SmtpTimeout added successfully';
END
ELSE
BEGIN
    PRINT 'Column SmtpTimeout already exists';
END
GO

-- Add EmailType column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'EmailType')
BEGIN
    ALTER TABLE tbl_EmailLog ADD EmailType NVARCHAR(50) NULL;
    PRINT 'Column EmailType added successfully';
END
ELSE
BEGIN
    PRINT 'Column EmailType already exists';
END
GO

-- Add SentBy column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'SentBy')
BEGIN
    ALTER TABLE tbl_EmailLog ADD SentBy INT NULL;
    PRINT 'Column SentBy added successfully';
END
ELSE
BEGIN
    PRINT 'Column SentBy already exists';
END
GO

-- Add SentFrom column (to track which part of the system sent the email)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'tbl_EmailLog' AND COLUMN_NAME = 'SentFrom')
BEGIN
    ALTER TABLE tbl_EmailLog ADD SentFrom NVARCHAR(100) NULL;
    PRINT 'Column SentFrom added successfully';
END
ELSE
BEGIN
    PRINT 'Column SentFrom already exists';
END
GO

PRINT 'All missing columns added to tbl_EmailLog successfully';
GO
