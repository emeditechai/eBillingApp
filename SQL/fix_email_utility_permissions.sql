-- =============================================
-- Fix Email Services and Email Logs Permissions
-- Grant access to all roles that should have it
-- =============================================
SET NOCOUNT ON;
PRINT '========================================';
PRINT 'Fixing Email Services & Email Logs Permissions';
PRINT '========================================';
PRINT '';

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @EmailLogsMenuId INT;
    DECLARE @EmailServicesMenuId INT;
    DECLARE @AdminRoleId INT;
    DECLARE @ManagerRoleId INT;
    DECLARE @FloorManagerRoleId INT;
    
    -- Get Menu IDs
    SELECT @EmailLogsMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS';
    SELECT @EmailServicesMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES';
    
    -- Get Role IDs
    SELECT @AdminRoleId = Id FROM Roles WHERE Name = 'Administrator';
    SELECT @ManagerRoleId = Id FROM Roles WHERE Name = 'Manager';
    SELECT @FloorManagerRoleId = Id FROM Roles WHERE Name = 'Floor Manager';
    
    PRINT 'Step 1: Granting Email Logs permissions...';
    PRINT '';
    
    -- Administrator - Email Logs
    IF @AdminRoleId IS NOT NULL AND @EmailLogsMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @EmailLogsMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@AdminRoleId, @EmailLogsMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Administrator granted Email Logs permissions';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1, CanApprove = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @AdminRoleId AND MenuId = @EmailLogsMenuId;
            PRINT '  ✓ Administrator Email Logs permissions updated';
        END
    END
    
    -- Manager - Email Logs
    IF @ManagerRoleId IS NOT NULL AND @EmailLogsMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @ManagerRoleId AND MenuId = @EmailLogsMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@ManagerRoleId, @EmailLogsMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Manager granted Email Logs permissions';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1, CanApprove = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @ManagerRoleId AND MenuId = @EmailLogsMenuId;
            PRINT '  ✓ Manager Email Logs permissions updated';
        END
    END
    
    -- Floor Manager - Email Logs
    IF @FloorManagerRoleId IS NOT NULL AND @EmailLogsMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @FloorManagerRoleId AND MenuId = @EmailLogsMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@FloorManagerRoleId, @EmailLogsMenuId, 1, 0, 0, 0, 0, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Floor Manager granted Email Logs permissions (view, print, export)';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @FloorManagerRoleId AND MenuId = @EmailLogsMenuId;
            PRINT '  ✓ Floor Manager Email Logs permissions updated';
        END
    END
    
    PRINT '';
    PRINT 'Step 2: Granting Email Services permissions...';
    PRINT '';
    
    -- Administrator - Email Services
    IF @AdminRoleId IS NOT NULL AND @EmailServicesMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @EmailServicesMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@AdminRoleId, @EmailServicesMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Administrator granted Email Services permissions';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1, CanApprove = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @AdminRoleId AND MenuId = @EmailServicesMenuId;
            PRINT '  ✓ Administrator Email Services permissions updated';
        END
    END
    
    -- Manager - Email Services
    IF @ManagerRoleId IS NOT NULL AND @EmailServicesMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @ManagerRoleId AND MenuId = @EmailServicesMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@ManagerRoleId, @EmailServicesMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Manager granted Email Services permissions';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanAdd = 1, CanEdit = 1, CanDelete = 1, CanApprove = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @ManagerRoleId AND MenuId = @EmailServicesMenuId;
            PRINT '  ✓ Manager Email Services permissions updated';
        END
    END
    
    -- Floor Manager - Email Services
    IF @FloorManagerRoleId IS NOT NULL AND @EmailServicesMenuId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @FloorManagerRoleId AND MenuId = @EmailServicesMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@FloorManagerRoleId, @EmailServicesMenuId, 1, 0, 0, 0, 0, 1, 1, GETDATE(), 1);
            PRINT '  ✓ Floor Manager granted Email Services permissions (view, print, export)';
        END
        ELSE
        BEGIN
            UPDATE RoleMenuPermissions 
            SET CanView = 1, CanPrint = 1, CanExport = 1, UpdatedAt = GETDATE(), UpdatedBy = 1
            WHERE RoleId = @FloorManagerRoleId AND MenuId = @EmailServicesMenuId;
            PRINT '  ✓ Floor Manager Email Services permissions updated';
        END
    END
    
    PRINT '';
    PRINT 'Step 3: Verifying permissions...';
    PRINT '';
    
    SELECT 
        r.Name AS [Role],
        nm.DisplayName AS [Menu],
        CASE WHEN rmp.CanView = 1 THEN 'Yes' ELSE 'No' END AS [View],
        CASE WHEN rmp.CanAdd = 1 THEN 'Yes' ELSE 'No' END AS [Add],
        CASE WHEN rmp.CanEdit = 1 THEN 'Yes' ELSE 'No' END AS [Edit],
        CASE WHEN rmp.CanDelete = 1 THEN 'Yes' ELSE 'No' END AS [Delete]
    FROM RoleMenuPermissions rmp
    INNER JOIN Roles r ON r.Id = rmp.RoleId
    INNER JOIN NavigationMenus nm ON nm.Id = rmp.MenuId
    WHERE nm.Code IN ('NAV_SETTINGS_EMAIL_LOGS', 'NAV_SETTINGS_EMAIL_SERVICES')
    ORDER BY nm.DisplayName, r.Name;
    
    -- Commit the transaction
    COMMIT TRANSACTION;
    
    PRINT '';
    PRINT '========================================';
    PRINT '✓ Permissions Fixed Successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'All roles now have appropriate access to:';
    PRINT '  • Email Logs';
    PRINT '  • Email Services';
    PRINT '';
    PRINT 'Please restart the application for changes to take effect.';
    PRINT '';

END TRY
BEGIN CATCH
    -- Rollback in case of error
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    
    PRINT '';
    PRINT '========================================';
    PRINT 'ERROR OCCURRED!';
    PRINT '========================================';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR(10));
    PRINT '';
    PRINT 'Transaction rolled back. No changes were made.';
    PRINT '';
    
    -- Re-throw the error
    THROW;
END CATCH;

SET NOCOUNT OFF;
