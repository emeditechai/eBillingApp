-- =============================================
-- Add KitchenStation field to BOT_Header
-- This allows filtering BOT tickets by kitchen station (e.g., "BAR")
-- =============================================

USE [RestaurantManagementDB] -- Change to your database name
GO

-- Add KitchenStation column to BOT_Header if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BOT_Header') AND name = 'KitchenStation')
BEGIN
    ALTER TABLE dbo.BOT_Header 
    ADD KitchenStation VARCHAR(50) NOT NULL DEFAULT 'BAR';
    
    PRINT 'Added KitchenStation column to BOT_Header table';
END
ELSE
BEGIN
    PRINT 'KitchenStation column already exists in BOT_Header table';
END
GO

-- Update existing BOT records to have 'BAR' as kitchen station
UPDATE dbo.BOT_Header
SET KitchenStation = 'BAR'
WHERE KitchenStation IS NULL OR KitchenStation = '';
GO

-- Create index for faster filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BOT_Header_KitchenStation' AND object_id = OBJECT_ID('dbo.BOT_Header'))
BEGIN
    CREATE INDEX IX_BOT_Header_KitchenStation ON dbo.BOT_Header(KitchenStation);
    PRINT 'Created index IX_BOT_Header_KitchenStation';
END
GO

PRINT 'KitchenStation field successfully added to BOT_Header';
GO
