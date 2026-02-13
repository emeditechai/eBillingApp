-- ============================================================================
-- Script: Clean Master Data - Tables
-- Description:
--   Deletes table configuration master data (Tables + TableSections) and
--   dependent table-management data (Reservations/Waitlist/TableTurnovers/
--   ServerAssignments) WITHOUT deleting Orders.
--
-- IMPORTANT:
--   - If there are any Orders / OrderTables rows, this script will STOP to avoid
--     breaking historical references.
--   - If you want a full reset, run transaction cleanup first.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ReseedIdentities BIT = 1;

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '============================================================';
    PRINT 'Clean Master Data - Tables';
    PRINT 'DB: ' + DB_NAME();
    PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

    -- Guard: stop if Orders exist
    IF OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Orders)
            THROW 51000, 'STOP: Orders has data. Run transaction cleanup first, then clean tables.', 1;
    END

    IF OBJECT_ID('dbo.OrderTables', 'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.OrderTables)
            THROW 51000, 'STOP: OrderTables has data. Run transaction cleanup first, then clean tables.', 1;
    END

    PRINT '';
    PRINT 'Deleting table-related data (dependency order)...';

    -- If exists, child tables first
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
        PRINT 'Tables table does not exist. Skipping.';
    END

    -- Optional master list used by dropdowns
    IF OBJECT_ID('dbo.TableSections', 'U') IS NOT NULL
    BEGIN
        DELETE FROM dbo.TableSections;
        PRINT ' - Deleted TableSections: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
        IF @ReseedIdentities = 1 DBCC CHECKIDENT ('dbo.TableSections', RESEED, 0);
    END

    COMMIT TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'SUCCESS: Tables master data cleaned';
    PRINT 'End: ' + CONVERT(VARCHAR, GETDATE(), 120);
    PRINT '============================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '============================================================';
    PRINT 'FAILED: Clean Master Data - Tables';
    PRINT 'Error: ' + ERROR_MESSAGE();
    PRINT 'Line: ' + CAST(ERROR_LINE() AS VARCHAR(20));
    PRINT '============================================================';

    THROW;
END CATCH;

SET NOCOUNT OFF;
GO
