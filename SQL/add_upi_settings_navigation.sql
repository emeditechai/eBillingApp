-- Add UPI Settings to Navigation Menu
-- This script adds UPI Settings under the Settings dropdown in the navigation bar

-- First, check if the entry already exists to avoid duplicates
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_UPI')
BEGIN
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
        OpenInNewTab,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        'NAV_SETTINGS_UPI',              -- Unique code
        'NAV_SETTINGS',                   -- Parent is Settings menu
        'UPI Settings',                   -- Display name
        'Configure UPI payment QR code settings',  -- Description
        NULL,                             -- No area
        'UPISettings',                    -- Controller name
        'Index',                          -- Action name
        NULL,                             -- No route values
        NULL,                             -- No custom URL
        'fas fa-qrcode',                  -- QR code icon
        11,                               -- Display order (after Loyalty Points)
        1,                                -- IsActive
        1,                                -- IsVisible
        NULL,                             -- No theme color
        NULL,                             -- No shortcut hint
        0,                                -- Don't open in new tab
        GETDATE(),                        -- Created timestamp
        GETDATE()                         -- Updated timestamp
    );
    
    PRINT 'UPI Settings navigation menu item added successfully.';
END
ELSE
BEGIN
    PRINT 'UPI Settings navigation menu item already exists.';
END
GO

-- Grant permissions to all roles for UPI Settings (adjust as needed for your security model)
-- Get the newly inserted menu item ID
DECLARE @UPIMenuId INT;
SELECT @UPIMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_UPI';

IF @UPIMenuId IS NOT NULL
BEGIN
    -- Grant permission to all existing roles that have access to Settings parent menu
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanEdit, CreatedAt, UpdatedAt)
    SELECT DISTINCT 
        rmp.RoleId,
        @UPIMenuId,
        1,  -- CanView
        1,  -- CanEdit (only admins should have this, adjust if needed)
        GETDATE(),
        GETDATE()
    FROM RoleMenuPermissions rmp
    WHERE rmp.MenuId = (SELECT Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS')
    AND rmp.CanView = 1
    AND NOT EXISTS (
        SELECT 1 FROM RoleMenuPermissions 
        WHERE RoleId = rmp.RoleId AND MenuId = @UPIMenuId
    );
    
    PRINT 'UPI Settings permissions granted to roles with Settings access.';
END
GO
