-- =============================================
-- Add Email Services to Navigation Menu
-- =============================================
USE dev_Restaurant;
GO

-- Insert navigation menu entry for Email Services
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES')
BEGIN
    INSERT INTO NavigationMenus (
        Code,
        ParentCode,
        DisplayName,
        Description,
        ControllerName,
        ActionName,
        IconCss,
        DisplayOrder,
        IsActive,
        IsVisible,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        'NAV_SETTINGS_EMAIL_SERVICES',   -- Code
        'NAV_SETTINGS',                  -- ParentCode (Settings menu)
        'Email Services',                -- DisplayName
        'Send automated birthday/anniversary emails and custom campaigns', -- Description
        'EmailServices',                 -- ControllerName
        'Index',                         -- ActionName
        'fas fa-envelope-open-text compact-icon text-info', -- IconCss
        12,                              -- DisplayOrder (after Email Logs which is 11)
        1,                               -- IsActive
        1,                               -- IsVisible
        GETDATE(),                       -- CreatedAt
        GETDATE()                        -- UpdatedAt
    );
    
    PRINT 'Navigation menu entry for Email Services created successfully';
END
ELSE
BEGIN
    PRINT 'Navigation menu entry for Email Services already exists';
END
GO

-- Grant permission to Administrator role
DECLARE @AdminRoleId INT;
DECLARE @EmailServicesMenuId INT;

SELECT @AdminRoleId = Id FROM Roles WHERE Name = 'Administrator';
SELECT @EmailServicesMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES';

IF @AdminRoleId IS NOT NULL AND @EmailServicesMenuId IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM RoleNavigationPermissions 
        WHERE RoleId = @AdminRoleId AND NavigationMenuId = @EmailServicesMenuId
    )
    BEGIN
        INSERT INTO RoleNavigationPermissions (RoleId, NavigationMenuId, CanView, CanCreate, CanEdit, CanDelete)
        VALUES (@AdminRoleId, @EmailServicesMenuId, 1, 1, 1, 1);
        
        PRINT 'Administrator role granted permissions to Email Services menu';
    END
    ELSE
    BEGIN
        PRINT 'Administrator role already has permissions for Email Services menu';
    END
END
GO

-- Grant permission to Manager role
DECLARE @ManagerRoleId INT;
DECLARE @EmailServicesMenuId INT;

SELECT @ManagerRoleId = Id FROM Roles WHERE Name = 'Manager';
SELECT @EmailServicesMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES';

IF @ManagerRoleId IS NOT NULL AND @EmailServicesMenuId IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM RoleNavigationPermissions 
        WHERE RoleId = @ManagerRoleId AND NavigationMenuId = @EmailServicesMenuId
    )
    BEGIN
        INSERT INTO RoleNavigationPermissions (RoleId, NavigationMenuId, CanView, CanCreate, CanEdit, CanDelete)
        VALUES (@ManagerRoleId, @EmailServicesMenuId, 1, 1, 1, 1);
        
        PRINT 'Manager role granted permissions to Email Services menu';
    END
    ELSE
    BEGIN
        PRINT 'Manager role already has permissions for Email Services menu';
    END
END
GO

-- Verify the insertion
SELECT 
    n.Code,
    n.ParentCode,
    n.DisplayName,
    n.ControllerName,
    n.ActionName,
    n.DisplayOrder,
    n.IsActive
FROM NavigationMenus n
WHERE n.Code IN ('NAV_SETTINGS', 'NAV_SETTINGS_MAIL_CONFIG', 'NAV_SETTINGS_EMAIL_LOGS', 'NAV_SETTINGS_EMAIL_SERVICES')
ORDER BY 
    CASE WHEN n.ParentCode IS NULL THEN 0 ELSE 1 END,
    n.DisplayOrder;
