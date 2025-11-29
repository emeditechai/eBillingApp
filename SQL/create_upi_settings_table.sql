-- Create table for UPI Payment Settings
-- This table stores UPI configuration for generating payment QR codes

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UPISettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UPISettings] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UPIId] NVARCHAR(100) NOT NULL, -- UPI ID like restaurant@paytm
        [PayeeName] NVARCHAR(200) NOT NULL, -- Restaurant/Business name
        [IsEnabled] BIT NOT NULL DEFAULT 1, -- Enable/Disable UPI payments
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedBy] INT NULL, -- User ID who updated
        CONSTRAINT FK_UPISettings_UpdatedBy FOREIGN KEY ([UpdatedBy]) REFERENCES [Users]([Id])
    );
    
    PRINT '✓ UPISettings table created successfully';
END
ELSE
BEGIN
    PRINT '⚠ UPISettings table already exists';
END
GO

-- Insert default record if table is empty
IF NOT EXISTS (SELECT 1 FROM [dbo].[UPISettings])
BEGIN
    INSERT INTO [dbo].[UPISettings] ([UPIId], [PayeeName], [IsEnabled])
    VALUES ('restaurant@paytm', 'Restaurant Name', 0); -- Disabled by default until configured
    
    PRINT '✓ Default UPI settings inserted';
END
GO

PRINT '✓ UPI Settings table setup complete';
