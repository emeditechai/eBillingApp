/*
    Add Counter Master page under Utility navigation
    - Adds NavigationMenus entry under NAV_UTILITY
    - Copies role permissions from NAV_UTILITY_TABLE_SECTIONS where available

    Page endpoints: Master/CounterList
*/

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @UtilityMenuId INT;
DECLARE @BaselineMenuId INT;
DECLARE @CounterMenuId INT;

SELECT @UtilityMenuId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_UTILITY';
SELECT @BaselineMenuId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_UTILITY_TABLE_SECTIONS';

IF @UtilityMenuId IS NULL
BEGIN
    RAISERROR('NAV_UTILITY menu not found in dbo.NavigationMenus. Run create_utility_navigation.sql first.', 16, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.NavigationMenus WHERE Code = 'NAV_UTILITY_COUNTER_MASTER')
BEGIN
    INSERT INTO dbo.NavigationMenus
    (
        Code, ParentCode, DisplayName, Description, Area,
        ControllerName, ActionName, RouteValues, CustomUrl,
        IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab,
        CreatedAt, UpdatedAt
    )
    VALUES
    (
        'NAV_UTILITY_COUNTER_MASTER',
        'NAV_UTILITY',
        'Counter Master',
        'Counter Master - add/edit counters',
        NULL,
        'Master',
        'CounterList',
        NULL,
        NULL,
        'fas fa-cash-register compact-icon text-primary',
        6,
        1,
        1,
        NULL,
        NULL,
        0,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    UPDATE dbo.NavigationMenus
    SET ParentCode = 'NAV_UTILITY',
        DisplayName = 'Counter Master',
        ControllerName = 'Master',
        ActionName = 'CounterList',
        IsActive = 1,
        IsVisible = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Code = 'NAV_UTILITY_COUNTER_MASTER';
END

SELECT @CounterMenuId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_UTILITY_COUNTER_MASTER';

IF @CounterMenuId IS NULL
BEGIN
    RAISERROR('Failed to create/find NAV_UTILITY_COUNTER_MASTER in dbo.NavigationMenus.', 16, 1);
END

-- Copy permissions from Table Sections (recommended default)
IF @BaselineMenuId IS NOT NULL
BEGIN
    INSERT INTO dbo.RoleMenuPermissions
    (
        RoleId, MenuId,
        CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport,
        CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
    )
    SELECT
        rmp.RoleId,
        @CounterMenuId,
        rmp.CanView,
        rmp.CanAdd,
        rmp.CanEdit,
        rmp.CanDelete,
        rmp.CanApprove,
        rmp.CanPrint,
        rmp.CanExport,
        SYSUTCDATETIME(),
        rmp.CreatedBy,
        SYSUTCDATETIME(),
        rmp.UpdatedBy
    FROM dbo.RoleMenuPermissions rmp
    WHERE rmp.MenuId = @BaselineMenuId
      AND rmp.CanView = 1
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.RoleMenuPermissions existing
            WHERE existing.RoleId = rmp.RoleId
              AND existing.MenuId = @CounterMenuId
      );
END
ELSE
BEGIN
    -- Fallback: grant administrators (if present)
    DECLARE @AdminRoleId INT;
    SELECT @AdminRoleId = Id FROM dbo.Roles WHERE Name = 'Administrator';

    IF @AdminRoleId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @CounterMenuId)
        BEGIN
            INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
            VALUES (@AdminRoleId, @CounterMenuId, 1, 1, 1, 1, 1, 1, 1, SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), NULL);
        END
    END
END

COMMIT TRANSACTION;
GO

-- Verify
SELECT Code, ParentCode, DisplayName, ControllerName, ActionName, DisplayOrder, IsActive, IsVisible
FROM dbo.NavigationMenus
WHERE Code = 'NAV_UTILITY_COUNTER_MASTER';
GO
