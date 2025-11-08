-- Migration script to add GST metadata columns to Orders table
-- This script is idempotent and can be run multiple times safely

USE [RestaurantDB]
GO

-- Add GSTPercentage column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTPercentage')
BEGIN
    ALTER TABLE dbo.Orders ADD GSTPercentage DECIMAL(10,4) NULL;
    PRINT 'Added GSTPercentage column to Orders table';
END
ELSE
BEGIN
    PRINT 'GSTPercentage column already exists in Orders table';
END
GO

-- Add CGSTPercentage column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CGSTPercentage')
BEGIN
    ALTER TABLE dbo.Orders ADD CGSTPercentage DECIMAL(10,4) NULL;
    PRINT 'Added CGSTPercentage column to Orders table';
END
ELSE
BEGIN
    PRINT 'CGSTPercentage column already exists in Orders table';
END
GO

-- Add SGSTPercentage column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'SGSTPercentage')
BEGIN
    ALTER TABLE dbo.Orders ADD SGSTPercentage DECIMAL(10,4) NULL;
    PRINT 'Added SGSTPercentage column to Orders table';
END
ELSE
BEGIN
    PRINT 'SGSTPercentage column already exists in Orders table';
END
GO

-- Add GSTAmount column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTAmount')
BEGIN
    ALTER TABLE dbo.Orders ADD GSTAmount DECIMAL(18,2) NULL;
    PRINT 'Added GSTAmount column to Orders table';
END
ELSE
BEGIN
    PRINT 'GSTAmount column already exists in Orders table';
END
GO

-- Add CGSTAmount column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CGSTAmount')
BEGIN
    ALTER TABLE dbo.Orders ADD CGSTAmount DECIMAL(18,2) NULL;
    PRINT 'Added CGSTAmount column to Orders table';
END
ELSE
BEGIN
    PRINT 'CGSTAmount column already exists in Orders table';
END
GO

-- Add SGSTAmount column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'SGSTAmount')
BEGIN
    ALTER TABLE dbo.Orders ADD SGSTAmount DECIMAL(18,2) NULL;
    PRINT 'Added SGSTAmount column to Orders table';
END
ELSE
BEGIN
    PRINT 'SGSTAmount column already exists in Orders table';
END
GO

-- Verify all columns were added
SELECT 
    'Orders table GST columns status' AS Description,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTPercentage') THEN 'EXISTS' ELSE 'MISSING' END AS GSTPercentage,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CGSTPercentage') THEN 'EXISTS' ELSE 'MISSING' END AS CGSTPercentage,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'SGSTPercentage') THEN 'EXISTS' ELSE 'MISSING' END AS SGSTPercentage,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTAmount') THEN 'EXISTS' ELSE 'MISSING' END AS GSTAmount,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CGSTAmount') THEN 'EXISTS' ELSE 'MISSING' END AS CGSTAmount,
    CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'SGSTAmount') THEN 'EXISTS' ELSE 'MISSING' END AS SGSTAmount;
GO

PRINT 'Migration completed successfully';
GO
