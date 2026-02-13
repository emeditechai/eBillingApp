-- ============================================================================
-- Script: Clean Master Data - Categories
-- Description:
--   Deletes all rows from dbo.Categories (master data).
--
-- IMPORTANT:
--   - This script will STOP if SubCategories or MenuItems still exist, because
--     they depend on Categories.
--   - Recommended order:
--       1) Clean_Master_MenuItems.sql
--       2) Clean_Master_SubCategories.sql
--       3) Clean_Master_Categories.sql
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ReseedIdentities BIT = 1;
DECLARE @RowCount BIGINT;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT 'Clean Master Data - Categories';
    PRINT 'DB: ' + DB_NAME();
    PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

    IF OBJECT_ID('dbo.Categories', 'U') IS NULL
    BEGIN
        PRINT 'Categories table does not exist. Nothing to clean.';
        COMMIT TRANSACTION;
        RETURN;
    END

    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.MenuItems)
            THROW 51000, 'STOP: MenuItems still has data. Run Clean_Master_MenuItems.sql first.', 1;
    END

    IF OBJECT_ID('dbo.SubCategories', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SubCategories)
            THROW 51000, 'STOP: SubCategories still has data. Run Clean_Master_SubCategories.sql first.', 1;
    END

    SELECT @RowCount = COUNT(1) FROM dbo.Categories;
    PRINT 'Categories current rows: ' + CAST(@RowCount AS VARCHAR(20));

    DELETE FROM dbo.Categories;
    PRINT 'Deleted Categories: ' + CAST(@@ROWCOUNT AS VARCHAR(20));

    IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.Categories', RESEED, 0);

    COMMIT TRANSACTION;

    PRINT 'SUCCESS: Categories cleaned.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT 'FAILED: Clean Master Data - Categories';
    PRINT 'Error: ' + ERROR_MESSAGE();

    THROW;
END CATCH;

SET NOCOUNT OFF;
GO
