-- ================================================
-- Add Loyalty Points Configuration to Settings Menu
-- ================================================

BEGIN TRANSACTION;

-- Add Loyalty Configuration menu item to Settings
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_LOYALTY')
BEGIN
    INSERT INTO NavigationMenus (Code, DisplayName, ControllerName, ActionName, IconCss, DisplayOrder, IsActive, ParentCode, ThemeColor)
    VALUES ('NAV_SETTINGS_LOYALTY', 'Loyalty Points', 'LoyaltyConfig', 'Index', 'fas fa-award', 10, 1, 'NAV_SETTINGS', '#f1c21b');
    PRINT 'Loyalty Points menu item added to Settings';
END
ELSE
BEGIN
    PRINT 'Loyalty Points menu item already exists';
END

-- Grant permissions to Administrator
DECLARE @AdminRoleId INT = (SELECT Id FROM Roles WHERE Name = 'Administrator');
DECLARE @LoyaltyMenuId INT = (SELECT Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_LOYALTY');

IF @AdminRoleId IS NOT NULL AND @LoyaltyMenuId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @LoyaltyMenuId)
BEGIN
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanPrint, CanExport)
    VALUES (@AdminRoleId, @LoyaltyMenuId, 1, 1, 1, 1, 1, 1);
    PRINT 'Loyalty Points permissions granted to Administrator';
END

-- Grant permissions to Manager
DECLARE @ManagerRoleId INT = (SELECT Id FROM Roles WHERE Name = 'Manager');
IF @ManagerRoleId IS NOT NULL AND @LoyaltyMenuId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM RoleMenuPermissions WHERE RoleId = @ManagerRoleId AND MenuId = @LoyaltyMenuId)
BEGIN
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanPrint, CanExport)
    VALUES (@ManagerRoleId, @LoyaltyMenuId, 1, 1, 1, 0, 1, 1);
    PRINT 'Loyalty Points permissions granted to Manager';
END

-- Verify
SELECT 
    nm.Code,
    nm.DisplayName,
    nm.ControllerName,
    nm.ActionName,
    nm.DisplayOrder,
    nm.ParentCode
FROM NavigationMenus nm
WHERE nm.Code = 'NAV_SETTINGS_LOYALTY';

COMMIT TRANSACTION;
PRINT 'Loyalty Points navigation setup completed successfully';
