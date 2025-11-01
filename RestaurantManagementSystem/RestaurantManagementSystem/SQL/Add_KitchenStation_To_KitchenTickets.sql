-- =============================================
-- Add KitchenStation field to KitchenTickets table
-- This allows BOT and KOT tickets to share the same table
-- BOT tickets will have KitchenStation = 'BAR'
-- KOT tickets will have KitchenStation = 'KITCHEN' or NULL
-- =============================================

USE [dev_Restaurant] -- Updated to match connection string
GO

-- Add KitchenStation column to KitchenTickets if not exists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.KitchenTickets') AND name = 'KitchenStation')
BEGIN
    ALTER TABLE dbo.KitchenTickets 
    ADD KitchenStation VARCHAR(50) NULL;
    
    PRINT 'Added KitchenStation column to KitchenTickets table';
    
    -- Update existing KOT tickets to have 'KITCHEN' station
    UPDATE dbo.KitchenTickets
    SET KitchenStation = 'KITCHEN'
    WHERE TicketNumber LIKE 'KOT-%';
    
    PRINT 'Updated existing KOT tickets with KITCHEN station';
END
ELSE
BEGIN
    PRINT 'KitchenStation column already exists in KitchenTickets table';
END
GO

-- Create index for faster filtering by KitchenStation
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_KitchenTickets_KitchenStation' AND object_id = OBJECT_ID('dbo.KitchenTickets'))
BEGIN
    CREATE INDEX IX_KitchenTickets_KitchenStation ON dbo.KitchenTickets(KitchenStation);
    PRINT 'Created index IX_KitchenTickets_KitchenStation';
END
GO

PRINT 'KitchenStation field successfully added to KitchenTickets table';
GO
