-- ================================================================
-- BOT MODULE SETUP INSTRUCTIONS
-- ================================================================
-- Execute this script on your SQL Server database to set up the 
-- BAR/BOT (Beverage Order Ticket) module.
--
-- IMPORTANT: Run SQL/BOT_Setup.sql first to create all tables and 
-- stored procedures.
--
-- After database setup is complete, the application will:
-- 1. Automatically route BAR items to BOT
-- 2. Route FOOD items to KOT (existing flow)
-- 3. Support mixed orders with both BAR and FOOD items
-- ================================================================

-- Step 1: Verify BOT tables exist
SELECT 
    'BOT_Header' as TableName,
    CASE WHEN OBJECT_ID('dbo.BOT_Header', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END as Status
UNION ALL
SELECT 'BOT_Detail', CASE WHEN OBJECT_ID('dbo.BOT_Detail', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'BOT_Audit', CASE WHEN OBJECT_ID('dbo.BOT_Audit', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'BOT_Bills', CASE WHEN OBJECT_ID('dbo.BOT_Bills', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'BOT_Payments', CASE WHEN OBJECT_ID('dbo.BOT_Payments', 'U') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END;

-- Step 2: Verify stored procedures exist
SELECT 
    'GetNextBOTNumber' as ProcedureName,
    CASE WHEN OBJECT_ID('dbo.GetNextBOTNumber', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END as Status
UNION ALL
SELECT 'GetBOTsByStatus', CASE WHEN OBJECT_ID('dbo.GetBOTsByStatus', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'GetBOTDetails', CASE WHEN OBJECT_ID('dbo.GetBOTDetails', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'UpdateBOTStatus', CASE WHEN OBJECT_ID('dbo.UpdateBOTStatus', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'VoidBOT', CASE WHEN OBJECT_ID('dbo.VoidBOT', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'GetBOTDashboardStats', CASE WHEN OBJECT_ID('dbo.GetBOTDashboardStats', 'P') IS NOT NULL THEN 'EXISTS' ELSE 'MISSING' END;

-- Step 3: Check if IsAlcoholic column exists on MenuItems
SELECT 
    'MenuItems.IsAlcoholic' as ColumnCheck,
    CASE WHEN EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.MenuItems') 
        AND name = 'IsAlcoholic'
    ) THEN 'EXISTS' ELSE 'MISSING' END as Status;

-- Step 4: Verify Bar menu item group exists
SELECT 
    'Bar menuitemgroup' as GroupCheck,
    CASE WHEN EXISTS (
        SELECT 1 FROM menuitemgroup 
        WHERE itemgroup = 'Bar' AND is_active = 1
    ) THEN 'EXISTS' ELSE 'MISSING - Please create Bar group' END as Status;

-- Step 5: Sample data - Create Bar menu item group if missing
-- Uncomment and execute if Bar group doesn't exist:
/*
IF NOT EXISTS (SELECT 1 FROM menuitemgroup WHERE itemgroup = 'Bar')
BEGIN
    INSERT INTO menuitemgroup (itemgroup, is_active, GST_Perc)
    VALUES ('Bar', 1, 18.00);
    PRINT 'Bar menu item group created successfully';
END
*/

-- Step 6: Sample - Update existing bar items to use Bar group
-- Uncomment and modify as needed:
/*
UPDATE MenuItems 
SET menuitemgroupID = (SELECT ID FROM menuitemgroup WHERE itemgroup = 'Bar')
WHERE Name LIKE '%Beer%' OR Name LIKE '%Wine%' OR Name LIKE '%Cocktail%' 
   OR Name LIKE '%Whiskey%' OR Name LIKE '%Vodka%';
*/

-- Step 7: Sample - Mark alcoholic items
-- Uncomment and modify as needed:
/*
UPDATE MenuItems 
SET IsAlcoholic = 1
WHERE menuitemgroupID = (SELECT ID FROM menuitemgroup WHERE itemgroup = 'Bar')
AND (Name LIKE '%Beer%' OR Name LIKE '%Wine%' OR Name LIKE '%Whiskey%' 
     OR Name LIKE '%Vodka%' OR Name LIKE '%Rum%' OR Name LIKE '%Brandy%');
*/

-- ================================================================
-- TESTING QUERIES
-- ================================================================

-- View all BOTs
-- SELECT * FROM BOT_Header ORDER BY CreatedAt DESC;

-- View BOT with items
-- SELECT h.*, d.* 
-- FROM BOT_Header h
-- LEFT JOIN BOT_Detail d ON h.BOT_ID = d.BOT_ID
-- WHERE h.BOT_ID = 1;

-- View BOT audit trail
-- SELECT * FROM BOT_Audit ORDER BY Timestamp DESC;

-- Dashboard stats
-- EXEC GetBOTDashboardStats;

-- ================================================================
-- SETUP COMPLETE
-- ================================================================
PRINT '=================================================================';
PRINT 'BOT MODULE SETUP VERIFICATION COMPLETE';
PRINT '=================================================================';
PRINT 'Next steps:';
PRINT '1. Ensure all tables show EXISTS status';
PRINT '2. Ensure all stored procedures show EXISTS status';
PRINT '3. Create Bar menu item group if missing';
PRINT '4. Assign bar items to Bar group';
PRINT '5. Mark alcoholic items with IsAlcoholic = 1';
PRINT '6. Run the application and test order routing';
PRINT '=================================================================';
