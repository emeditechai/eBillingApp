-- SQL Script to create the RestaurantSettings table
USE [dev_Restaurant]; -- Database name from connection string

-- Check if the RestaurantSettings table already exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RestaurantSettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    -- Create dbo.RestaurantSettings table
    CREATE TABLE [dbo].[RestaurantSettings] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [RestaurantName] NVARCHAR(100) NOT NULL,
        [StreetAddress] NVARCHAR(200) NOT NULL,
        [City] NVARCHAR(50) NOT NULL,
        [State] NVARCHAR(50) NOT NULL,
        [Pincode] NVARCHAR(10) NOT NULL,
        [Country] NVARCHAR(50) NOT NULL,
        [GSTCode] NVARCHAR(15) NOT NULL,
        [PhoneNumber] NVARCHAR(15) NULL,
        [Email] NVARCHAR(100) NULL,
        [Website] NVARCHAR(100) NULL,
        [LogoPath] NVARCHAR(200) NULL,
        [CurrencySymbol] NVARCHAR(50) NOT NULL DEFAULT N'₹',
        [DefaultGSTPercentage] DECIMAL(5,2) NOT NULL DEFAULT 5.00,
        [IsCounterRequired] BIT NOT NULL DEFAULT(0),
        [CreatedAt] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] DATETIME NOT NULL DEFAULT GETDATE()
    );
    
    -- Insert default restaurant settings
    INSERT INTO [dbo].[RestaurantSettings] (
        [RestaurantName], 
        [StreetAddress], 
        [City], 
        [State], 
        [Pincode], 
        [Country], 
        [GSTCode],
        [PhoneNumber],
        [Email],
        [Website],
        [CurrencySymbol],
        [DefaultGSTPercentage]
    )
    VALUES (
        'My Restaurant',
        'Sample Street Address',
        'Mumbai',
        'Maharashtra',
        '400001',
        'India',
        '27AAPFU0939F1ZV',
        '+919876543210',
        'info@myrestaurant.com',
        'https://www.myrestaurant.com',
        '₹',
        5.00
    );
    
    PRINT 'dbo.RestaurantSettings table created successfully with default settings.';
END
ELSE
BEGIN
    PRINT 'dbo.RestaurantSettings table already exists.';
END