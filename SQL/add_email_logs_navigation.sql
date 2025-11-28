-- Add Email Logs navigation entry to Settings menu
USE dev_Restaurant;
GO

-- Insert navigation menu entry for Email Logs
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS')
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
        'NAV_SETTINGS_EMAIL_LOGS',      -- Code
        'NAV_SETTINGS',                  -- ParentCode (Settings menu)
        'Email Logs',                    -- DisplayName
        'View email sending history and error logs', -- Description
        'EmailLogs',                     -- ControllerName
        'Index',                         -- ActionName
        'fas fa-clipboard-list compact-icon text-warning', -- IconCss
        11,                              -- DisplayOrder (after Mail Configuration which is 10)
        1,                               -- IsActive
        1,                               -- IsVisible
        GETDATE(),                       -- CreatedAt
        GETDATE()                        -- UpdatedAt
    );
    
    PRINT 'Navigation menu entry for Email Logs created successfully';
END
ELSE
BEGIN
    PRINT 'Navigation menu entry for Email Logs already exists';
END
GO

-- Grant permission to Administrator role
DECLARE @AdminRoleId INT;
DECLARE @EmailLogsMenuId INT;

SELECT @AdminRoleId = Id FROM Roles WHERE Name = 'Administrator';
SELECT @EmailLogsMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS';

IF @AdminRoleId IS NOT NULL AND @EmailLogsMenuId IS NOT NULL
BEGIN
    -- Check if permission already exists
    IF NOT EXISTS (
        SELECT 1 
        FROM RoleMenuPermissions 
        WHERE RoleId = @AdminRoleId 
        AND MenuId = @EmailLogsMenuId
    )
    BEGIN
        INSERT INTO RoleMenuPermissions (
            RoleId,
            MenuId,
            CanView,
            CanAdd,
            CanEdit,
            CanDelete,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @AdminRoleId,                    -- RoleId
            @EmailLogsMenuId,                -- MenuId
            1,                               -- CanView
            0,                               -- CanAdd
            0,                               -- CanEdit
            0,                               -- CanDelete
            GETDATE(),                       -- CreatedAt
            GETDATE()                        -- UpdatedAt
        );
        
        PRINT 'Email Logs permission granted to Administrator role';
    END
    ELSE
    BEGIN
        PRINT 'Email Logs permission already exists for Administrator role';
    END
END
ELSE
BEGIN
    IF @AdminRoleId IS NULL
        PRINT 'Administrator role not found';
    IF @EmailLogsMenuId IS NULL
        PRINT 'Email Logs menu not found';
END
GO

-- Grant permission to Manager role (optional)
DECLARE @ManagerRoleId INT;
DECLARE @EmailLogsMenuId INT;

SELECT @ManagerRoleId = Id FROM Roles WHERE Name = 'Manager';
SELECT @EmailLogsMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS';

IF @ManagerRoleId IS NOT NULL AND @EmailLogsMenuId IS NOT NULL
BEGIN
    -- Check if permission already exists
    IF NOT EXISTS (
        SELECT 1 
        FROM RoleMenuPermissions 
        WHERE RoleId = @ManagerRoleId 
        AND MenuId = @EmailLogsMenuId
    )
    BEGIN
        INSERT INTO RoleMenuPermissions (
            RoleId,
            MenuId,
            CanView,
            CanAdd,
            CanEdit,
            CanDelete,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @ManagerRoleId,                  -- RoleId
            @EmailLogsMenuId,                -- MenuId
            1,                               -- CanView
            0,                               -- CanAdd
            0,                               -- CanEdit
            0,                               -- CanDelete
            GETDATE(),                       -- CreatedAt
            GETDATE()                        -- UpdatedAt
        );
        
        PRINT 'Email Logs permission granted to Manager role';
    END
    ELSE
    BEGIN
        PRINT 'Email Logs permission already exists for Manager role';
    END
END
ELSE
BEGIN
    IF @ManagerRoleId IS NULL
        PRINT 'Manager role not found';
    IF @EmailLogsMenuId IS NULL
        PRINT 'Email Logs menu not found';
END
GO

-- Verify the navigation menu entry
SELECT 
    Code,
    ParentCode,
    DisplayName,
    ControllerName,
    ActionName,
    DisplayOrder,
    IsActive,
    IsVisible
FROM NavigationMenus
WHERE Code IN ('NAV_SETTINGS_EMAIL_LOGS', 'NAV_SETTINGS_MAIL')
ORDER BY DisplayOrder;
GO

PRINT 'Email Logs navigation setup completed successfully';
