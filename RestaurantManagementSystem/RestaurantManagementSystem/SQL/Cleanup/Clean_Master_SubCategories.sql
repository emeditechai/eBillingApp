-- ============================================================================
-- Script: Clean Master Data - Sub Categories
-- Description:
--   Deletes all rows from dbo.SubCategories (master data).
--
-- IMPORTANT:
--   - If MenuItems are still linked to SubCategories (MenuItems.SubCategoryId),
--     this script will STOP. Run Clean_Master_MenuItems.sql first.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ReseedIdentities BIT = 1;
DECLARE @RowCount BIGINT;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT 'Clean Master Data - SubCategories';
    PRINT 'DB: ' + DB_NAME();
    PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

    IF OBJECT_ID('dbo.SubCategories', 'U') IS NULL
    BEGIN
        PRINT 'SubCategories table does not exist. Nothing to clean.';
        COMMIT TRANSACTION;
        RETURN;
    END

    -- Guard: if MenuItems has SubCategoryId and rows are linked, stop
    IF OBJECT_ID('dbo.MenuItems', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'MenuItems' AND COLUMN_NAME = 'SubCategoryId')
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.MenuItems WHERE SubCategoryId IS NOT NULL)
                THROW 51000, 'STOP: MenuItems.SubCategoryId still has data. Run Clean_Master_MenuItems.sql first.', 1;
        END
    END

    SELECT @RowCount = COUNT(1) FROM dbo.SubCategories;
    PRINT 'SubCategories current rows: ' + CAST(@RowCount AS VARCHAR(20));

    DELETE FROM dbo.SubCategories;
    PRINT 'Deleted SubCategories: ' + CAST(@@ROWCOUNT AS VARCHAR(20));

    IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.SubCategories', RESEED, 0);

    COMMIT TRANSACTION;

    PRINT 'SUCCESS: SubCategories cleaned.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT 'FAILED: Clean Master Data - SubCategories';
    PRINT 'Error: ' + ERROR_MESSAGE();

    THROW;
END CATCH;

SET NOCOUNT OFF;
GO
