/*
    Add POS Order page to Orders navigation
    - Adds NavigationMenus entry under NAV_ORDERS
    - Copies role permissions from NAV_ORDERS_CREATE so the same roles see it

    Note:
    - Page endpoints: Order/POSOrder
    - Controller permission checks currently rely on NAV_ORDERS_CREATE
*/

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DECLARE @OrdersParentId INT;
DECLARE @CreateOrderMenuId INT;
DECLARE @PosOrderMenuId INT;

SELECT @OrdersParentId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_ORDERS';
SELECT @CreateOrderMenuId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_ORDERS_CREATE';

IF @OrdersParentId IS NULL
BEGIN
    -- Parent menu missing; abort with a clear message
    RAISERROR('NAV_ORDERS menu not found in dbo.NavigationMenus. Run create_navigation_permissions.sql first.', 16, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.NavigationMenus WHERE Code = 'NAV_ORDERS_POS')
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
        'NAV_ORDERS_POS',
        'NAV_ORDERS',
        'POS Order',
        'Single-screen Takeout/Delivery order entry',
        NULL,
        'Order',
        'POSOrder',
        NULL,
        NULL,
        'fas fa-cash-register compact-icon text-primary',
        5,
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
    -- Keep it under Orders and point it to Order/POSOrder
    UPDATE dbo.NavigationMenus
    SET ParentCode = 'NAV_ORDERS',
        DisplayName = 'POS Order',
        ControllerName = 'Order',
        ActionName = 'POSOrder',
        IsActive = 1,
        IsVisible = 1,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Code = 'NAV_ORDERS_POS';
END

SELECT @PosOrderMenuId = Id FROM dbo.NavigationMenus WHERE Code = 'NAV_ORDERS_POS';

IF @PosOrderMenuId IS NULL
BEGIN
    RAISERROR('Failed to create/find NAV_ORDERS_POS in dbo.NavigationMenus.', 16, 1);
END

-- Copy permissions from NAV_ORDERS_CREATE (recommended default)
IF @CreateOrderMenuId IS NOT NULL
BEGIN
    INSERT INTO dbo.RoleMenuPermissions
    (
        RoleId, MenuId,
        CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport,
        CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
    )
    SELECT
        rmp.RoleId,
        @PosOrderMenuId,
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
    WHERE rmp.MenuId = @CreateOrderMenuId
      AND rmp.CanView = 1
      AND NOT EXISTS (
            SELECT 1
            FROM dbo.RoleMenuPermissions existing
            WHERE existing.RoleId = rmp.RoleId
              AND existing.MenuId = @PosOrderMenuId
      );
END
ELSE
BEGIN
    -- If NAV_ORDERS_CREATE doesn't exist, at least grant administrators (if present)
    DECLARE @AdminRoleId INT;
    SELECT @AdminRoleId = Id FROM dbo.Roles WHERE Name = 'Administrator';

    IF @AdminRoleId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.RoleMenuPermissions WHERE RoleId = @AdminRoleId AND MenuId = @PosOrderMenuId)
        BEGIN
            INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
            VALUES (@AdminRoleId, @PosOrderMenuId, 1, 1, 1, 1, 1, 1, 1, SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), NULL);
        END
    END
END

COMMIT TRANSACTION;
GO

-- Verify
SELECT Code, ParentCode, DisplayName, ControllerName, ActionName, DisplayOrder, IsActive, IsVisible
FROM dbo.NavigationMenus
WHERE Code = 'NAV_ORDERS_POS';
GO
