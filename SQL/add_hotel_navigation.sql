-- Add Hotel Booking Module to Navigation Menu
-- This script adds Hotel menu items to the navigation bar

-- =============================================
-- Add Hotel Parent Menu
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL')
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
        'NAV_HOTEL',
        NULL,
        'Hotel',
        'Hotel Booking Management',
        NULL,
        'Hotel',
        'Dashboard',
        NULL,
        NULL,
        'fas fa-hotel',
        45,
        1,
        1,
        '#8B5CF6',
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Hotel parent menu added successfully.';
END
GO

-- =============================================
-- Add Hotel Dashboard
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_DASHBOARD')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_DASHBOARD',
        'NAV_HOTEL',
        'Dashboard',
        'Hotel Dashboard Overview',
        NULL,
        'Hotel',
        'Dashboard',
        NULL,
        NULL,
        'fas fa-tachometer-alt',
        1,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Hotel Dashboard menu added.';
END
GO

-- =============================================
-- Add Search Rooms
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_SEARCH')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_SEARCH',
        'NAV_HOTEL',
        'Search Rooms',
        'Search Available Rooms',
        NULL,
        'Hotel',
        'SearchRooms',
        NULL,
        NULL,
        'fas fa-search',
        2,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Search Rooms menu added.';
END
GO

-- =============================================
-- Add Bookings
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_BOOKINGS')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_BOOKINGS',
        'NAV_HOTEL',
        'Bookings',
        'Manage Hotel Bookings',
        NULL,
        'Hotel',
        'Bookings',
        NULL,
        NULL,
        'fas fa-calendar-alt',
        3,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Bookings menu added.';
END
GO

-- =============================================
-- Add Rooms
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_ROOMS')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_ROOMS',
        'NAV_HOTEL',
        'Rooms',
        'Manage Hotel Rooms',
        NULL,
        'Hotel',
        'Rooms',
        NULL,
        NULL,
        'fas fa-door-open',
        4,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Rooms menu added.';
END
GO

-- =============================================
-- Add Room Types
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_ROOMTYPES')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_ROOMTYPES',
        'NAV_HOTEL',
        'Room Types',
        'Manage Room Types',
        NULL,
        'Hotel',
        'RoomTypes',
        NULL,
        NULL,
        'fas fa-th-large',
        5,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Room Types menu added.';
END
GO

-- =============================================
-- Add Guests
-- =============================================
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_HOTEL_GUESTS')
BEGIN
    INSERT INTO NavigationMenus (
        Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName,
        RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible,
        ThemeColor, ShortcutHint, OpenInNewTab, CreatedAt, UpdatedAt
    )
    VALUES (
        'NAV_HOTEL_GUESTS',
        'NAV_HOTEL',
        'Guests',
        'Manage Hotel Guests',
        NULL,
        'Hotel',
        'Guests',
        NULL,
        NULL,
        'fas fa-users',
        6,
        1,
        1,
        NULL,
        NULL,
        0,
        GETDATE(),
        GETDATE()
    );
    PRINT 'Guests menu added.';
END
GO

-- =============================================
-- Grant permissions to all roles
-- =============================================
DECLARE @HotelMenuId INT;
SELECT @HotelMenuId = Id FROM NavigationMenus WHERE Code = 'NAV_HOTEL';

IF @HotelMenuId IS NOT NULL
BEGIN
    -- Grant permission to all existing roles
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanEdit, CreatedAt, UpdatedAt)
    SELECT 
        r.Id,
        @HotelMenuId,
        1,
        1,
        GETDATE(),
        GETDATE()
    FROM Roles r
    WHERE NOT EXISTS (
        SELECT 1 FROM RoleMenuPermissions 
        WHERE RoleId = r.Id AND MenuId = @HotelMenuId
    );
    
    PRINT 'Hotel menu permissions granted to all roles.';
END
GO

-- Grant permissions for all sub-menus
DECLARE @MenuCursor CURSOR;
DECLARE @SubMenuId INT;
DECLARE @ParentId INT;

SELECT @ParentId = Id FROM NavigationMenus WHERE Code = 'NAV_HOTEL';

SET @MenuCursor = CURSOR FOR
SELECT Id FROM NavigationMenus WHERE ParentCode = 'NAV_HOTEL';

OPEN @MenuCursor;
FETCH NEXT FROM @MenuCursor INTO @SubMenuId;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO RoleMenuPermissions (RoleId, MenuId, CanView, CanEdit, CreatedAt, UpdatedAt)
    SELECT 
        rmp.RoleId,
        @SubMenuId,
        1,
        1,
        GETDATE(),
        GETDATE()
    FROM RoleMenuPermissions rmp
    WHERE rmp.MenuId = @ParentId
    AND rmp.CanView = 1
    AND NOT EXISTS (
        SELECT 1 FROM RoleMenuPermissions 
        WHERE RoleId = rmp.RoleId AND MenuId = @SubMenuId
    );
    
    FETCH NEXT FROM @MenuCursor INTO @SubMenuId;
END

CLOSE @MenuCursor;
DEALLOCATE @MenuCursor;

PRINT 'Hotel sub-menu permissions granted.';
GO

PRINT 'Hotel navigation menu setup completed successfully!';
GO
