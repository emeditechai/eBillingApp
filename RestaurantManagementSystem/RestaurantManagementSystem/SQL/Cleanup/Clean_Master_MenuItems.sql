-- ============================================================================
-- Script: Clean Master Data - Menu Items
-- Description:
--   Cleans Menu master data safely (MenuItems + dependent master tables/mappings)
--   WITHOUT touching Orders/Payments/transactional history.
--
-- IMPORTANT:
--   - If you have any transactions (Orders/OrderItems/OnlineOrders/BOT), this script
--     will STOP to protect history and avoid FK failures.
--   - If you want to wipe transactions first, run one of:
--       * RestaurantManagementSystem/RestaurantManagementSystem/SQL/clean_transaction_data_for_production.sql
--       * RestaurantManagementSystem/RestaurantManagementSystem/SQL/clean_transaction_data_dependency_safe.sql
--
-- Usage:
--   1) Review counts printed by the script
--   2) Run in a NON-PROD DB (take backup first)
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ReseedIdentities BIT = 1;         -- 1 = DBCC CHECKIDENT back to 0 where applicable
DECLARE @DeleteReferenceMasters BIT = 0;   -- 1 = also delete Modifiers/Allergens (optional)
DECLARE @RowCount BIGINT;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT 'Clean Master Data - Menu Items';
    PRINT 'DB: ' + DB_NAME();
    PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

    -- ------------------------------------------------------------------------
    -- SAFETY GUARDS: stop if transactional data exists
    -- ------------------------------------------------------------------------
    IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.OrderItems)
            THROW 51000, 'STOP: OrderItems has data. Run transaction cleanup first (keeps master clean).', 1;
    END

    IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Orders)
            THROW 51000, 'STOP: Orders has data. Run transaction cleanup first (keeps master clean).', 1;
    END

    IF OBJECT_ID('dbo.OnlineOrderItems', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.OnlineOrderItems)
            THROW 51000, 'STOP: OnlineOrderItems has data. Run transaction cleanup first.', 1;
    END

    IF OBJECT_ID('dbo.OnlineOrders', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.OnlineOrders)
            THROW 51000, 'STOP: OnlineOrders has data. Run transaction cleanup first.', 1;
    END

    IF OBJECT_ID('dbo.BOT_Detail', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.BOT_Detail)
            THROW 51000, 'STOP: BOT_Detail has data. Run transaction cleanup first.', 1;
    END

    IF OBJECT_ID('dbo.BOT_Header', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.BOT_Header)
            THROW 51000, 'STOP: BOT_Header has data. Run transaction cleanup first.', 1;
    END

    -- ------------------------------------------------------------------------
    -- PREVIEW COUNTS
    -- ------------------------------------------------------------------------
    PRINT '';
    PRINT 'Preview row counts (existing objects only):';

    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItems;
        PRINT ' - MenuItems: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.SubCategories;
        PRINT ' - SubCategories: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.Categories;
        PRINT ' - Categories: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemKitchenStations', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItemKitchenStations;
        PRINT ' - MenuItemKitchenStations: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemIngredients', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItemIngredients;
        PRINT ' - MenuItemIngredients: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemAllergens', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItemAllergens;
        PRINT ' - MenuItemAllergens: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItem_Allergens', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItem_Allergens;
        PRINT ' - MenuItem_Allergens: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemModifiers', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItemModifiers;
        PRINT ' - MenuItemModifiers: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItem_Modifiers', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuItem_Modifiers;
        PRINT ' - MenuItem_Modifiers: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.Recipes', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.Recipes;
        PRINT ' - Recipes: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.RecipeSteps', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.RecipeSteps;
        PRINT ' - RecipeSteps: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuVersionHistory', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.MenuVersionHistory;
        PRINT ' - MenuVersionHistory: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.PriceChangeApproval', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.PriceChangeApproval;
        PRINT ' - PriceChangeApproval: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.POSPublishStatus', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.POSPublishStatus;
        PRINT ' - POSPublishStatus: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.ExternalMenuItemMappings', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.ExternalMenuItemMappings;
        PRINT ' - ExternalMenuItemMappings: ' + CAST(@RowCount AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.ExternalModifierMappings', 'U') IS NOT NULL
    BEGIN
        SELECT @RowCount = COUNT(1) FROM dbo.ExternalModifierMappings;
        PRINT ' - ExternalModifierMappings: ' + CAST(@RowCount AS VARCHAR(20));
    END

    PRINT '';
    PRINT 'Deleting Menu master data (dependency order)...';

    -- ------------------------------------------------------------------------
    -- DELETE DEPENDENCIES FIRST
    -- ------------------------------------------------------------------------

    IF OBJECT_ID('dbo.ExternalModifierMappings', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.ExternalModifierMappings;
        PRINT ' - Deleted ExternalModifierMappings: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.ExternalModifierMappings', RESEED, 0);
    END

    IF OBJECT_ID('dbo.ExternalMenuItemMappings', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.ExternalMenuItemMappings;
        PRINT ' - Deleted ExternalMenuItemMappings: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.ExternalMenuItemMappings', RESEED, 0);
    END

    IF OBJECT_ID('dbo.POSPublishStatus', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.POSPublishStatus;
        PRINT ' - Deleted POSPublishStatus: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.POSPublishStatus', RESEED, 0);
    END

    IF OBJECT_ID('dbo.PriceChangeApproval', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.PriceChangeApproval;
        PRINT ' - Deleted PriceChangeApproval: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.PriceChangeApproval', RESEED, 0);
    END

    IF OBJECT_ID('dbo.MenuVersionHistory', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuVersionHistory;
        PRINT ' - Deleted MenuVersionHistory: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuVersionHistory', RESEED, 0);
    END

    IF OBJECT_ID('dbo.RecipeSteps', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.RecipeSteps;
        PRINT ' - Deleted RecipeSteps: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.RecipeSteps', RESEED, 0);
    END

    IF OBJECT_ID('dbo.Recipes', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.Recipes;
        PRINT ' - Deleted Recipes: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Recipes', RESEED, 0);
    END

    IF OBJECT_ID('dbo.MenuItemIngredients', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItemIngredients;
        PRINT ' - Deleted MenuItemIngredients: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItemIngredients', RESEED, 0);
    END

    IF OBJECT_ID('dbo.MenuItemAllergens', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItemAllergens;
        PRINT ' - Deleted MenuItemAllergens: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItemAllergens', RESEED, 0);
    END

    IF OBJECT_ID('dbo.MenuItem_Allergens', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItem_Allergens;
        PRINT ' - Deleted MenuItem_Allergens: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemModifiers', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItemModifiers;
        PRINT ' - Deleted MenuItemModifiers: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItemModifiers', RESEED, 0);
    END

    IF OBJECT_ID('dbo.MenuItem_Modifiers', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItem_Modifiers;
        PRINT ' - Deleted MenuItem_Modifiers: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
    END

    IF OBJECT_ID('dbo.MenuItemKitchenStations', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItemKitchenStations;
        PRINT ' - Deleted MenuItemKitchenStations: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItemKitchenStations', RESEED, 0);
    END

    -- ------------------------------------------------------------------------
    -- DELETE MENU ITEMS
    -- ------------------------------------------------------------------------
    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.MenuItems;
        PRINT ' - Deleted MenuItems: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItems', RESEED, 0);
    END

    -- Optional: reference masters (not always desired)
    IF @DeleteReferenceMasters = 1
    BEGIN
        IF OBJECT_ID('dbo.Modifiers', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.Modifiers;
            PRINT ' - Deleted Modifiers: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Modifiers', RESEED, 0);
        END

        IF OBJECT_ID('dbo.Allergens', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.Allergens;
            PRINT ' - Deleted Allergens: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Allergens', RESEED, 0);
        END
    END

    COMMIT TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'SUCCESS: MenuItems master data cleaned';
    PRINT 'End: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'FAILED: Clean Master Data - Menu Items';
    PRINT 'Error: ' + ERROR_MESSAGE();
    PRINT 'Line: ' + CAST(ERROR_LINE() AS VARCHAR(20));
    PRINT '============================================================';

    THROW;
END CATCH;

SET NOCOUNT OFF;
GO
