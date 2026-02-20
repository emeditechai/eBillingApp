-- ============================================================================
-- Script: Clear Menu Master Data With Dependencies
-- Description:
--   Clears Categories, SubCategories, and MenuItems while handling dependent
--   rows in related tables first.
--
--   Dependency handling includes dynamic cleanup for tables containing:
--   - MenuItemId
--   - OrderItemId (for order rows tied to menu items)
--   - RecipeId   (for recipe rows tied to menu items)
--   - SubCategoryId
--   - CategoryId
--
-- WARNING:
--   This script deletes menu master data and dependent transactional rows.
--   Take a full backup before running in production.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '========================================';
    PRINT 'Starting Menu Master Data Cleanup';
    PRINT 'Date: ' + CONVERT(VARCHAR(19), GETDATE(), 120);
    PRINT '========================================';

    IF OBJECT_ID('tempdb..#MenuItemIds') IS NOT NULL DROP TABLE #MenuItemIds;
    IF OBJECT_ID('tempdb..#OrderItemIds') IS NOT NULL DROP TABLE #OrderItemIds;
    IF OBJECT_ID('tempdb..#RecipeIds') IS NOT NULL DROP TABLE #RecipeIds;
    IF OBJECT_ID('tempdb..#SubCategoryIds') IS NOT NULL DROP TABLE #SubCategoryIds;
    IF OBJECT_ID('tempdb..#CategoryIds') IS NOT NULL DROP TABLE #CategoryIds;

    CREATE TABLE #MenuItemIds (Id INT PRIMARY KEY);
    CREATE TABLE #OrderItemIds (Id INT PRIMARY KEY);
    CREATE TABLE #RecipeIds (Id INT PRIMARY KEY);
    CREATE TABLE #SubCategoryIds (Id INT PRIMARY KEY);
    CREATE TABLE #CategoryIds (Id INT PRIMARY KEY);

    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #MenuItemIds (Id)
        SELECT Id FROM dbo.MenuItems;
    END

    IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #OrderItemIds (Id)
        SELECT oi.Id
        FROM dbo.OrderItems oi
        INNER JOIN #MenuItemIds mi ON mi.Id = oi.MenuItemId;
    END

    IF OBJECT_ID('dbo.Recipes', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #RecipeIds (Id)
        SELECT r.Id
        FROM dbo.Recipes r
        INNER JOIN #MenuItemIds mi ON mi.Id = r.MenuItemId;
    END

    IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #SubCategoryIds (Id)
        SELECT Id FROM dbo.SubCategories;
    END

    IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    BEGIN
        INSERT INTO #CategoryIds (Id)
        SELECT Id FROM dbo.Categories;
    END

    DECLARE @menuItemCount INT;
    DECLARE @orderItemCount INT;
    DECLARE @recipeCount INT;
    DECLARE @subCategoryCount INT;
    DECLARE @categoryCount INT;

    SELECT @menuItemCount = COUNT(*) FROM #MenuItemIds;
    SELECT @orderItemCount = COUNT(*) FROM #OrderItemIds;
    SELECT @recipeCount = COUNT(*) FROM #RecipeIds;
    SELECT @subCategoryCount = COUNT(*) FROM #SubCategoryIds;
    SELECT @categoryCount = COUNT(*) FROM #CategoryIds;

    PRINT '';
    PRINT 'Target row counts:';
    PRINT ' - MenuItems: ' + CAST(@menuItemCount AS VARCHAR(20));
    PRINT ' - OrderItems linked to MenuItems: ' + CAST(@orderItemCount AS VARCHAR(20));
    PRINT ' - Recipes linked to MenuItems: ' + CAST(@recipeCount AS VARCHAR(20));
    PRINT ' - SubCategories: ' + CAST(@subCategoryCount AS VARCHAR(20));
    PRINT ' - Categories: ' + CAST(@categoryCount AS VARCHAR(20));

    -- ------------------------------------------------------------------------
    -- 1) Delete dependent rows by RecipeId (children of Recipes)
    -- ------------------------------------------------------------------------
    DECLARE @schemaName SYSNAME;
    DECLARE @tableName SYSNAME;
    DECLARE @sql NVARCHAR(MAX);

    DECLARE recipe_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'RecipeId'
      AND NOT (s.name = 'dbo' AND t.name = 'Recipes');

    OPEN recipe_cursor;
    FETCH NEXT FROM recipe_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'
            DELETE T
            FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' T
            INNER JOIN #RecipeIds R ON R.Id = T.RecipeId;';

        EXEC sp_executesql @sql;
        FETCH NEXT FROM recipe_cursor INTO @schemaName, @tableName;
    END

    CLOSE recipe_cursor;
    DEALLOCATE recipe_cursor;

    -- ------------------------------------------------------------------------
    -- 2) Delete dependent rows by OrderItemId (children of OrderItems)
    -- ------------------------------------------------------------------------
    DECLARE orderitem_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'OrderItemId'
      AND NOT (s.name = 'dbo' AND t.name = 'OrderItems');

    OPEN orderitem_cursor;
    FETCH NEXT FROM orderitem_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'
            DELETE T
            FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' T
            INNER JOIN #OrderItemIds OI ON OI.Id = T.OrderItemId;';

        EXEC sp_executesql @sql;
        FETCH NEXT FROM orderitem_cursor INTO @schemaName, @tableName;
    END

    CLOSE orderitem_cursor;
    DEALLOCATE orderitem_cursor;

    -- ------------------------------------------------------------------------
    -- 3) Delete dependent rows by MenuItemId (children of MenuItems)
    -- ------------------------------------------------------------------------
    DECLARE menuitem_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'MenuItemId'
      AND NOT (s.name = 'dbo' AND t.name = 'MenuItems');

    OPEN menuitem_cursor;
    FETCH NEXT FROM menuitem_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'
            DELETE T
            FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' T
            INNER JOIN #MenuItemIds MI ON MI.Id = T.MenuItemId;';

        EXEC sp_executesql @sql;
        FETCH NEXT FROM menuitem_cursor INTO @schemaName, @tableName;
    END

    CLOSE menuitem_cursor;
    DEALLOCATE menuitem_cursor;

    -- Explicit delete of OrderItems linked to MenuItems (if table exists)
    IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL
    BEGIN
        DELETE OI
        FROM dbo.OrderItems OI
        INNER JOIN #OrderItemIds X ON X.Id = OI.Id;

        PRINT ' - OrderItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    -- Explicit delete of Recipes linked to MenuItems (if table exists)
    IF OBJECT_ID('dbo.Recipes', 'U') IS NOT NULL
    BEGIN
        DELETE R
        FROM dbo.Recipes R
        INNER JOIN #RecipeIds X ON X.Id = R.Id;

        PRINT ' - Recipes deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    -- ------------------------------------------------------------------------
    -- 4) Delete MenuItems
    -- ------------------------------------------------------------------------
    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        DELETE M
        FROM dbo.MenuItems M
        INNER JOIN #MenuItemIds X ON X.Id = M.Id;

        PRINT ' - MenuItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    -- ------------------------------------------------------------------------
    -- 5) Cleanup by SubCategoryId (for any remaining dependent tables)
    -- ------------------------------------------------------------------------
    DECLARE subcat_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'SubCategoryId'
      AND NOT (s.name = 'dbo' AND t.name = 'SubCategories')
      AND NOT (s.name = 'dbo' AND t.name = 'MenuItems');

    OPEN subcat_cursor;
    FETCH NEXT FROM subcat_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'
            DELETE T
            FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' T
            INNER JOIN #SubCategoryIds SC ON SC.Id = T.SubCategoryId;';

        EXEC sp_executesql @sql;
        FETCH NEXT FROM subcat_cursor INTO @schemaName, @tableName;
    END

    CLOSE subcat_cursor;
    DEALLOCATE subcat_cursor;

    -- ------------------------------------------------------------------------
    -- 6) Delete SubCategories
    -- ------------------------------------------------------------------------
    IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
    BEGIN
        DELETE SC
        FROM dbo.SubCategories SC
        INNER JOIN #SubCategoryIds X ON X.Id = SC.Id;

        PRINT ' - SubCategories deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    -- ------------------------------------------------------------------------
    -- 7) Cleanup by CategoryId (for any remaining dependent tables)
    -- ------------------------------------------------------------------------
    DECLARE category_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'CategoryId'
      AND NOT (s.name = 'dbo' AND t.name = 'Categories')
      AND NOT (s.name = 'dbo' AND t.name = 'MenuItems')
      AND NOT (s.name = 'dbo' AND t.name = 'SubCategories');

    OPEN category_cursor;
    FETCH NEXT FROM category_cursor INTO @schemaName, @tableName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'
            DELETE T
            FROM ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName) + N' T
            INNER JOIN #CategoryIds C ON C.Id = T.CategoryId;';

        EXEC sp_executesql @sql;
        FETCH NEXT FROM category_cursor INTO @schemaName, @tableName;
    END

    CLOSE category_cursor;
    DEALLOCATE category_cursor;

    -- ------------------------------------------------------------------------
    -- 8) Delete Categories
    -- ------------------------------------------------------------------------
    IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    BEGIN
        DELETE C
        FROM dbo.Categories C
        INNER JOIN #CategoryIds X ON X.Id = C.Id;

        PRINT ' - Categories deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    -- Optional identity reseed for cleaned master tables
    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
        DBCC CHECKIDENT ('dbo.MenuItems', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
        DBCC CHECKIDENT ('dbo.SubCategories', RESEED, 0) WITH NO_INFOMSGS;

    IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
        DBCC CHECKIDENT ('dbo.Categories', RESEED, 0) WITH NO_INFOMSGS;

    COMMIT TRANSACTION;

    PRINT '';
    PRINT '========================================';
    PRINT 'Menu Master Data Cleanup COMPLETED';
    PRINT 'Status: SUCCESS';
    PRINT 'Date: ' + CONVERT(VARCHAR(19), GETDATE(), 120);
    PRINT '========================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '========================================';
    PRINT 'ERROR: Menu Master Data Cleanup FAILED';
    PRINT '========================================';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(20));
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR(20));
    PRINT '';
    PRINT 'Transaction has been rolled back.';

    THROW;
END CATCH;
GO
