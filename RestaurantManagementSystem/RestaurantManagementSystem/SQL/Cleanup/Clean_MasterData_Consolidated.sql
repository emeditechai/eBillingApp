-- ============================================================================
-- Script: Clean Master Data (Consolidated)
-- Description:
--   One-stop script to clean master data for:
--     - Menu Items
--     - Categories
--     - Sub Categories
--     - Tables
--
-- Safety:
--   - By default it STOPS if transactional data exists (Orders/OrderItems/etc.).
--   - This prevents breaking history or failing FK constraints.
--
-- Recommended:
--   - Take a full DB backup
--   - Run on non-production first
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- =============================
-- USER SETTINGS
-- =============================
DECLARE @CleanMenuMasters BIT = 1;        -- MenuItems and all dependent menu master tables
DECLARE @CleanCategoryMasters BIT = 1;    -- Categories
DECLARE @CleanSubCategoryMasters BIT = 1; -- SubCategories
DECLARE @CleanTableMasters BIT = 1;       -- Tables + reservation/table-service tables

DECLARE @ReseedIdentities BIT = 1;        -- DBCC CHECKIDENT reseed where applicable
DECLARE @StrictTransactionGuard BIT = 1;  -- 1 = STOP if any transaction data exists

-- Optional: also delete reference masters used by menu
DECLARE @DeleteReferenceMasters BIT = 0;  -- 1 = also delete Modifiers/Allergens

-- =============================
-- START
-- =============================
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @RowCount BIGINT;

    PRINT '============================================================';
    PRINT 'Clean Master Data (Consolidated)';
    PRINT 'DB: ' + DB_NAME();
    PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

    -- ------------------------------------------------------------------------
    -- GLOBAL GUARDS (Transactional data)
    -- ------------------------------------------------------------------------
    IF @StrictTransactionGuard = 1
    BEGIN
        IF OBJECT_ID('dbo.OrderItems', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.OrderItems)
                THROW 51000, 'STOP: OrderItems has data. Run transaction cleanup first.', 1;
        END

        IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Orders)
                THROW 51000, 'STOP: Orders has data. Run transaction cleanup first.', 1;
        END

        IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Payments)
                THROW 51000, 'STOP: Payments has data. Run transaction cleanup first.', 1;
        END

        IF OBJECT_ID('dbo.PaymentSplits', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.PaymentSplits)
                THROW 51000, 'STOP: PaymentSplits has data. Run transaction cleanup first.', 1;
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

        IF OBJECT_ID('dbo.KitchenTicketItems', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.KitchenTicketItems)
                THROW 51000, 'STOP: KitchenTicketItems has data. Run transaction cleanup first.', 1;
        END

        IF OBJECT_ID('dbo.KitchenTickets', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.KitchenTickets)
                THROW 51000, 'STOP: KitchenTickets has data. Run transaction cleanup first.', 1;
        END

        IF OBJECT_ID('dbo.OrderTables', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.OrderTables)
                THROW 51000, 'STOP: OrderTables has data. Run transaction cleanup first.', 1;
        END
    END

    -- ------------------------------------------------------------------------
    -- SECTION A: MENU MASTER CLEAN
    -- ------------------------------------------------------------------------
    IF @CleanMenuMasters = 1
    BEGIN
        PRINT '';
        PRINT '============================================================';
        PRINT 'A) Cleaning MENU master data...';
        PRINT '============================================================';

        -- Preview counts (best-effort)
        IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
        BEGIN
            SELECT @RowCount = COUNT(1) FROM dbo.MenuItems;
            PRINT ' - MenuItems: ' + CAST(@RowCount AS VARCHAR(20));
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

        IF OBJECT_ID('dbo.RecipeSteps', 'U') IS NOT NULL
        BEGIN
            SELECT @RowCount = COUNT(1) FROM dbo.RecipeSteps;
            PRINT ' - RecipeSteps: ' + CAST(@RowCount AS VARCHAR(20));
        END

        IF OBJECT_ID('dbo.Recipes', 'U') IS NOT NULL
        BEGIN
            SELECT @RowCount = COUNT(1) FROM dbo.Recipes;
            PRINT ' - Recipes: ' + CAST(@RowCount AS VARCHAR(20));
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
        PRINT 'Deleting menu dependencies...';

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

        IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.MenuItems;
            PRINT ' - Deleted MenuItems: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.MenuItems', RESEED, 0);
        END

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

        PRINT 'A) MENU master data cleaned.';
    END

    -- ------------------------------------------------------------------------
    -- SECTION B: SUB-CATEGORIES CLEAN
    -- ------------------------------------------------------------------------
    IF @CleanSubCategoryMasters = 1
    BEGIN
        PRINT '';
        PRINT '============================================================';
        PRINT 'B) Cleaning SubCategories...';
        PRINT '============================================================';

        IF OBJECT_ID('dbo.SubCategories', 'U') IS NULL
        BEGIN
            PRINT ' - SubCategories table not found. Skipping.';
        END
        ELSE
        BEGIN
            -- Guard: if MenuItems has SubCategoryId and still used, stop
            IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
               AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MenuItems' AND COLUMN_NAME = 'SubCategoryId')
               AND EXISTS (SELECT 1 FROM dbo.MenuItems WHERE SubCategoryId IS NOT NULL)
            BEGIN
                THROW 51000, 'STOP: MenuItems.SubCategoryId still has data. Clean menu items first.', 1;
            END

            SELECT @RowCount = COUNT(1) FROM dbo.SubCategories;
            PRINT ' - SubCategories: ' + CAST(@RowCount AS VARCHAR(20));
            DELETE FROM dbo.SubCategories;
            PRINT ' - Deleted SubCategories: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.SubCategories', RESEED, 0);
        END

        PRINT 'B) SubCategories cleaned.';
    END

    -- ------------------------------------------------------------------------
    -- SECTION C: CATEGORIES CLEAN
    -- ------------------------------------------------------------------------
    IF @CleanCategoryMasters = 1
    BEGIN
        PRINT '';
        PRINT '============================================================';
        PRINT 'C) Cleaning Categories...';
        PRINT '============================================================';

        IF OBJECT_ID('dbo.Categories', 'U') IS NULL
        BEGIN
            PRINT ' - Categories table not found. Skipping.';
        END
        ELSE
        BEGIN
            IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.MenuItems)
                    THROW 51000, 'STOP: MenuItems still has data. Clean menu items first.', 1;
            END

            IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.SubCategories)
                    THROW 51000, 'STOP: SubCategories still has data. Clean subcategories first.', 1;
            END

            SELECT @RowCount = COUNT(1) FROM dbo.Categories;
            PRINT ' - Categories: ' + CAST(@RowCount AS VARCHAR(20));
            DELETE FROM dbo.Categories;
            PRINT ' - Deleted Categories: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Categories', RESEED, 0);
        END

        PRINT 'C) Categories cleaned.';
    END

    -- ------------------------------------------------------------------------
    -- SECTION D: TABLES CLEAN
    -- ------------------------------------------------------------------------
    IF @CleanTableMasters = 1
    BEGIN
        PRINT '';
        PRINT '============================================================';
        PRINT 'D) Cleaning Tables master data...';
        PRINT '============================================================';

        -- Additional guard: orders/tables linking
        IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.Orders)
                THROW 51000, 'STOP: Orders has data. Run transaction cleanup first before cleaning tables.', 1;
        END

        IF OBJECT_ID('dbo.OrderTables', 'U') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.OrderTables)
                THROW 51000, 'STOP: OrderTables has data. Run transaction cleanup first before cleaning tables.', 1;
        END

        IF OBJECT_ID('dbo.ServerAssignments', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.ServerAssignments;
            PRINT ' - Deleted ServerAssignments: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.ServerAssignments', RESEED, 0);
        END

        IF OBJECT_ID('dbo.TableTurnovers', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.TableTurnovers;
            PRINT ' - Deleted TableTurnovers: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.TableTurnovers', RESEED, 0);
        END

        IF OBJECT_ID('dbo.Reservations', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.Reservations;
            PRINT ' - Deleted Reservations: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Reservations', RESEED, 0);
        END

        IF OBJECT_ID('dbo.Waitlist', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.Waitlist;
            PRINT ' - Deleted Waitlist: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Waitlist', RESEED, 0);
        END

        IF OBJECT_ID('dbo.Tables', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.Tables;
            PRINT ' - Deleted Tables: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Tables', RESEED, 0);
        END
        ELSE
        BEGIN
            PRINT ' - Tables table not found. Skipping.';
        END

        IF OBJECT_ID('dbo.TableSections', 'U') IS NOT NULL
        BEGIN
            DELETE FROM dbo.TableSections;
            PRINT ' - Deleted TableSections: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
            IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.TableSections', RESEED, 0);
        END

        PRINT 'D) Tables master data cleaned.';
    END

    COMMIT TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'SUCCESS: Consolidated master data cleanup completed';
    PRINT 'End: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'FAILED: Consolidated master data cleanup';
    PRINT 'Error: ' + ERROR_MESSAGE();
    PRINT 'Line: ' + CAST(ERROR_LINE() AS VARCHAR(20));
    PRINT '============================================================';

    THROW;
END CATCH;

SET NOCOUNT OFF;
GO
