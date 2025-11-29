-- =============================================
-- Create Utility Navigation Menu
-- Transfer Day Closing, Audit Trail, Email Logs, Email Services from Settings to Utility
-- =============================================
SET NOCOUNT ON;
PRINT '========================================';
PRINT 'Creating Utility Navigation Menu';
PRINT '========================================';
PRINT '';

BEGIN TRANSACTION;

BEGIN TRY
    -- Step 1: Create Utility parent menu item
    PRINT 'Step 1: Creating Utility parent menu item...';
    
    IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_UTILITY')
    BEGIN
        INSERT INTO NavigationMenus (
            Code,
            DisplayName,
            ParentCode,
            ControllerName,
            ActionName,
            Area,
            IconCss,
            DisplayOrder,
            IsActive,
            ShortcutHint,
            CustomUrl,
            ThemeColor,
            OpenInNewTab
        )
        VALUES (
            'NAV_UTILITY',                      -- Code
            'Utility',                          -- DisplayName
            NULL,                               -- ParentCode (top level)
            NULL,                               -- ControllerName
            NULL,                               -- ActionName
            NULL,                               -- Area
            'fas fa-tools',                     -- IconCss
            10,                                 -- DisplayOrder (after Settings)
            1,                                  -- IsActive
            NULL,                               -- ShortcutHint
            NULL,                               -- CustomUrl
            '#10b981',                          -- ThemeColor (green)
            0                                   -- OpenInNewTab
        );
        PRINT '✓ Utility parent menu created successfully';
    END
    ELSE
    BEGIN
        PRINT '  Utility parent menu already exists';
    END
    PRINT '';

    -- Step 2: Update Day Closing to move to Utility
    PRINT 'Step 2: Moving Day Closing to Utility...';
    
    UPDATE NavigationMenus
    SET ParentCode = 'NAV_UTILITY',
        DisplayOrder = 1
    WHERE Code = 'NAV_SETTINGS_DAYCLOSING';
    
    IF @@ROWCOUNT > 0
        PRINT '✓ Day Closing moved to Utility';
    ELSE
        PRINT '  Day Closing already in correct location or not found';
    PRINT '';

    -- Step 3: Update Audit Trail to move to Utility
    PRINT 'Step 3: Moving Audit Trail to Utility...';
    
    UPDATE NavigationMenus
    SET ParentCode = 'NAV_UTILITY',
        DisplayOrder = 2
    WHERE Code = 'NAV_SETTINGS_AUDIT';
    
    IF @@ROWCOUNT > 0
        PRINT '✓ Audit Trail moved to Utility';
    ELSE
        PRINT '  Audit Trail already in correct location or not found';
    PRINT '';

    -- Step 4: Update Email Logs to move to Utility
    PRINT 'Step 4: Moving Email Logs to Utility...';
    
    UPDATE NavigationMenus
    SET ParentCode = 'NAV_UTILITY',
        DisplayOrder = 3
    WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS';
    
    IF @@ROWCOUNT > 0
        PRINT '✓ Email Logs moved to Utility';
    ELSE
        PRINT '  Email Logs already in correct location or not found';
    PRINT '';

    -- Step 5: Update Email Services to move to Utility
    PRINT 'Step 5: Moving Email Services to Utility...';
    
    UPDATE NavigationMenus
    SET ParentCode = 'NAV_UTILITY',
        DisplayOrder = 4
    WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES';
    
    IF @@ROWCOUNT > 0
        PRINT '✓ Email Services moved to Utility';
    ELSE
        PRINT '  Email Services already in correct location or not found';
    PRINT '';

    -- Step 6: Reorder remaining Settings menu items
    PRINT 'Step 6: Reordering remaining Settings menu items...';
    
    UPDATE NavigationMenus SET DisplayOrder = 1 WHERE Code = 'NAV_SETTINGS_RESTAURANT';
    UPDATE NavigationMenus SET DisplayOrder = 2 WHERE Code = 'NAV_SETTINGS_USERS';
    UPDATE NavigationMenus SET DisplayOrder = 3 WHERE Code = 'NAV_SETTINGS_BANK';
    UPDATE NavigationMenus SET DisplayOrder = 4 WHERE Code = 'NAV_SETTINGS_STATIONS';
    UPDATE NavigationMenus SET DisplayOrder = 5 WHERE Code = 'NAV_SETTINGS_MENU_BUILDER';
    UPDATE NavigationMenus SET DisplayOrder = 6 WHERE Code = 'NAV_SETTINGS_ROLE_MENU';
    UPDATE NavigationMenus SET DisplayOrder = 7 WHERE Code = 'NAV_SETTINGS_ROLE_PERMISSIONS';
    UPDATE NavigationMenus SET DisplayOrder = 8 WHERE Code = 'NAV_SETTINGS_MAIL';
    UPDATE NavigationMenus SET DisplayOrder = 9 WHERE Code = 'NAV_SETTINGS_EMAIL_TEMPLATES';
    
    PRINT '✓ Settings menu items reordered';
    PRINT '';

    -- Step 7: Grant permissions to Administrator role for Utility menu
    PRINT 'Step 7: Granting permissions to Administrator role...';
    
    DECLARE @UtilityMenuId INT;
    DECLARE @AdminRoleId INT;
    
    SELECT @UtilityMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_UTILITY';
    SELECT @AdminRoleId = Id FROM Roles WHERE Name = 'Administrator';
    
    IF @UtilityMenuId IS NOT NULL AND @AdminRoleId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @UtilityMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@AdminRoleId, @UtilityMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '✓ Administrator permissions granted for Utility menu';
        END
        ELSE
        BEGIN
            PRINT '  Administrator already has permissions for Utility menu';
        END
    END
    ELSE
    BEGIN
        PRINT '  Warning: Could not find Utility menu or Administrator role';
    END
    PRINT '';

    -- Step 8: Grant permissions to Manager role for Utility menu
    PRINT 'Step 8: Granting permissions to Manager role...';
    
    DECLARE @ManagerRoleId INT;
    SELECT @ManagerRoleId = Id FROM Roles WHERE Name = 'Manager';
    
    IF @UtilityMenuId IS NOT NULL AND @ManagerRoleId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @ManagerRoleId AND MenuId = @UtilityMenuId)
        BEGIN
            INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy)
            VALUES (@ManagerRoleId, @UtilityMenuId, 1, 1, 1, 1, 1, 1, 1, GETDATE(), 1);
            PRINT '✓ Manager permissions granted for Utility menu';
        END
        ELSE
        BEGIN
            PRINT '  Manager already has permissions for Utility menu';
        END
    END
    ELSE
    BEGIN
        PRINT '  Warning: Could not find Utility menu or Manager role';
    END
    PRINT '';

    -- Step 9: Verify the changes
    PRINT 'Step 9: Verifying changes...';
    PRINT '';
    PRINT 'Utility Menu Items:';
    SELECT 
        DisplayOrder,
        Code,
        DisplayName,
        ControllerName,
        ActionName
    FROM NavigationMenus
    WHERE ParentCode = 'NAV_UTILITY'
    ORDER BY DisplayOrder;
    PRINT '';

    PRINT 'Settings Menu Items (after reorganization):';
    SELECT 
        DisplayOrder,
        Code,
        DisplayName,
        ControllerName,
        ActionName
    FROM NavigationMenus
    WHERE ParentCode = 'NAV_SETTINGS'
    ORDER BY DisplayOrder;
    PRINT '';

    -- Commit the transaction
    COMMIT TRANSACTION;
    
    PRINT '========================================';
    PRINT 'Utility Navigation Menu Created Successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Summary:';
    PRINT '  ✓ Utility parent menu created';
    PRINT '  ✓ Day Closing moved to Utility';
    PRINT '  ✓ Audit Trail moved to Utility';
    PRINT '  ✓ Email Logs moved to Utility';
    PRINT '  ✓ Email Services moved to Utility';
    PRINT '  ✓ Settings menu items reordered';
    PRINT '  ✓ Permissions granted to Administrator and Manager roles';
    PRINT '';
    PRINT 'The Utility menu is now available in the navigation bar!';
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
