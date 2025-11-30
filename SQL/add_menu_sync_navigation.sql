-- =============================================
-- Add Menu Sync Navigation Menu
-- =============================================
USE [dev_Restaurant]
GO

-- Check if Menu Sync navigation already exists
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_MENU_SYNC')
BEGIN
    -- Insert Menu Sync navigation item
    INSERT INTO NavigationMenus (
        Code, 
        ParentCode, 
        DisplayName, 
        Description, 
        Area, 
        ControllerName, 
        ActionName, 
        RouteValues,
        CustomUrl, 
        IconCss, 
        DisplayOrder, 
        IsActive, 
        IsVisible, 
        ThemeColor, 
        ShortcutHint, 
        OpenInNewTab
    )
    VALUES (
        'NAV_MENU_SYNC',
        'NAV_MENU',
        'Menu Sync',
        'Sync menu items to target server',
        NULL,
        'MenuSync',
        'Index',
        NULL,
        NULL,
        'fas fa-sync-alt compact-icon text-info',
        8,
        1,
        1,
        NULL,
        NULL,
        0
    );
    
    PRINT 'Menu Sync navigation item created successfully.';
    
    -- Get the newly created menu ID
    DECLARE @MenuSyncId INT;
    SELECT @MenuSyncId = Id FROM NavigationMenus WHERE Code = 'NAV_MENU_SYNC';
    
    -- Add permissions for all roles
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanEdit, CanDelete)
    SELECT 
        r.Id,
        @MenuSyncId,
        1, -- CanView
        1, -- CanEdit
        1  -- CanDelete
    FROM Roles r
    WHERE NOT EXISTS (
        SELECT 1 FROM RoleMenuPermissions 
        WHERE RoleId = r.Id AND MenuId = @MenuSyncId
    );
    
    PRINT 'Permissions added for all roles.';
END
ELSE
BEGIN
    PRINT 'Menu Sync navigation item already exists.';
END
GO
