/*
    Navigation menu & role permission schema
    Run this script to create NavigationMenus and RoleMenuPermissions tables along with seed data.
*/

SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.NavigationMenus', N'U') IS NULL
BEGIN
    PRINT 'Creating table dbo.NavigationMenus...';
    CREATE TABLE dbo.NavigationMenus
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(60) NOT NULL UNIQUE,
        ParentCode NVARCHAR(60) NULL,
        DisplayName NVARCHAR(120) NOT NULL,
        Description NVARCHAR(255) NULL,
        Area NVARCHAR(80) NULL,
        ControllerName NVARCHAR(120) NULL,
        ActionName NVARCHAR(120) NULL,
        RouteValues NVARCHAR(200) NULL,
        CustomUrl NVARCHAR(400) NULL,
        IconCss NVARCHAR(100) NULL,
        DisplayOrder INT NOT NULL DEFAULT(0),
        IsActive BIT NOT NULL DEFAULT(1),
        IsVisible BIT NOT NULL DEFAULT(1),
        ThemeColor NVARCHAR(30) NULL,
        ShortcutHint NVARCHAR(40) NULL,
        OpenInNewTab BIT NOT NULL DEFAULT(0),
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_NavigationMenus_ParentCode ON dbo.NavigationMenus (ParentCode);
END
ELSE
BEGIN
    PRINT 'Table dbo.NavigationMenus already exists.';
END
GO

IF OBJECT_ID(N'dbo.RoleMenuPermissions', N'U') IS NULL
BEGIN
    PRINT 'Creating table dbo.RoleMenuPermissions...';
    CREATE TABLE dbo.RoleMenuPermissions
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RoleId INT NOT NULL,
        MenuId INT NOT NULL,
        CanView BIT NOT NULL DEFAULT(0),
        CanAdd BIT NOT NULL DEFAULT(0),
        CanEdit BIT NOT NULL DEFAULT(0),
        CanDelete BIT NOT NULL DEFAULT(0),
        CanApprove BIT NOT NULL DEFAULT(0),
        CanPrint BIT NOT NULL DEFAULT(0),
        CanExport BIT NOT NULL DEFAULT(0),
        CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy INT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        CONSTRAINT FK_RoleMenuPermissions_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(Id),
        CONSTRAINT FK_RoleMenuPermissions_Menus FOREIGN KEY(MenuId) REFERENCES dbo.NavigationMenus(Id),
        CONSTRAINT UQ_RoleMenuPermissions UNIQUE(RoleId, MenuId)
    );

    CREATE INDEX IX_RoleMenuPermissions_RoleId ON dbo.RoleMenuPermissions(RoleId);
    CREATE INDEX IX_RoleMenuPermissions_MenuId ON dbo.RoleMenuPermissions(MenuId);
END
ELSE
BEGIN
    PRINT 'Table dbo.RoleMenuPermissions already exists.';
END
GO

PRINT 'Seeding navigation menu records...';
DECLARE @Menus TABLE
(
    Code NVARCHAR(60) NOT NULL,
    ParentCode NVARCHAR(60) NULL,
    DisplayName NVARCHAR(120) NOT NULL,
    Description NVARCHAR(255) NULL,
    Area NVARCHAR(80) NULL,
    ControllerName NVARCHAR(120) NULL,
    ActionName NVARCHAR(120) NULL,
    RouteValues NVARCHAR(200) NULL,
    CustomUrl NVARCHAR(400) NULL,
    IconCss NVARCHAR(100) NULL,
    DisplayOrder INT NOT NULL,
    IsActive BIT NOT NULL,
    IsVisible BIT NOT NULL,
    ThemeColor NVARCHAR(30) NULL,
    ShortcutHint NVARCHAR(40) NULL,
    OpenInNewTab BIT NOT NULL
);

INSERT INTO @Menus (Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName, RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible, ThemeColor, ShortcutHint, OpenInNewTab)
VALUES
    ('NAV_HOME', NULL, 'Home', 'Dashboard landing page', NULL, 'Home', 'Index', NULL, NULL, 'fas fa-home compact-icon', 1, 1, 1, '#1a3c5b', NULL, 0),
    ('NAV_RESERVATIONS', NULL, 'Reservations', 'Reservation workflows', NULL, NULL, NULL, NULL, NULL, 'far fa-calendar-alt compact-icon', 2, 1, 1, '#f8b133', NULL, 0),
    ('NAV_RESERVATIONS_DASH', 'NAV_RESERVATIONS', 'Dashboard', NULL, NULL, 'Reservation', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_RESERVATIONS_LIST', 'NAV_RESERVATIONS', 'Reservations', NULL, NULL, 'Reservation', 'List', NULL, NULL, 'fas fa-list compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_RESERVATIONS_WAITLIST', 'NAV_RESERVATIONS', 'Waitlist', NULL, NULL, 'Reservation', 'Waitlist', NULL, NULL, 'fas fa-clock compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_RESERVATIONS_TABLES', 'NAV_RESERVATIONS', 'Tables', NULL, NULL, 'Reservation', 'Tables', NULL, NULL, 'fas fa-table compact-icon text-primary', 4, 1, 1, NULL, NULL, 0),
    ('NAV_RESERVATIONS_FEEDBACK_FORM', 'NAV_RESERVATIONS', 'Guest Feedback', NULL, NULL, 'Feedback', 'Form', NULL, NULL, 'fas fa-comment-dots compact-icon text-primary', 5, 1, 1, NULL, NULL, 0),
    ('NAV_RESERVATIONS_FEEDBACK_SUMMARY', 'NAV_RESERVATIONS', 'Feedback Summary', NULL, NULL, 'Feedback', 'Summary', NULL, NULL, 'fas fa-chart-line compact-icon text-primary', 6, 1, 1, NULL, NULL, 0),
    ('NAV_TABLES', NULL, 'Tables', 'Table management', NULL, NULL, NULL, NULL, NULL, 'fas fa-chair compact-icon', 3, 1, 1, '#f8b133', NULL, 0),
    ('NAV_TABLES_DASH', 'NAV_TABLES', 'Dashboard', NULL, NULL, 'TableService', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 1, 1, 1, NULL, 'Shift+T', 0),
    ('NAV_TABLES_ACTIVE', 'NAV_TABLES', 'Active Tables', NULL, NULL, 'TableService', 'ActiveTables', NULL, NULL, 'fas fa-list-alt compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_TABLES_SEAT', 'NAV_TABLES', 'Seat Guest', NULL, NULL, 'TableService', 'SeatGuest', NULL, NULL, 'fas fa-user-plus compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_ORDERS', NULL, 'Orders', 'Order management', NULL, NULL, NULL, NULL, NULL, 'fas fa-clipboard-list compact-icon', 4, 1, 1, '#f8b133', NULL, 0),
    ('NAV_ORDERS_DASH', 'NAV_ORDERS', 'Dashboard', NULL, NULL, 'Order', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_ORDERS_CREATE', 'NAV_ORDERS', 'Create Order', NULL, NULL, 'Order', 'Create', NULL, NULL, 'fas fa-plus compact-icon text-primary', 2, 1, 1, NULL, 'Shift+F', 0),
    ('NAV_ORDERS_ESTIMATION', 'NAV_ORDERS', 'Menu Items & Estimation', NULL, NULL, 'Order', 'Estimation', NULL, NULL, 'fas fa-calculator compact-icon text-primary', 3, 1, 1, NULL, NULL, 1),
    ('NAV_PAYMENTS_DASH', 'NAV_ORDERS', 'Payment Dashboard', NULL, NULL, 'Payment', 'Dashboard', NULL, NULL, 'fas fa-chart-line compact-icon text-success', 4, 1, 1, NULL, NULL, 0),
    ('NAV_KITCHEN', NULL, 'Kitchen', 'Kitchen tickets', NULL, NULL, NULL, NULL, NULL, 'fas fa-fire compact-icon', 5, 1, 1, '#f8b133', NULL, 0),
    ('NAV_KITCHEN_DASH', 'NAV_KITCHEN', 'Dashboard', NULL, NULL, 'Kitchen', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 1, 1, 1, NULL, 'Shift+K', 0),
    ('NAV_KITCHEN_TICKETS', 'NAV_KITCHEN', 'Tickets', NULL, NULL, 'Kitchen', 'Tickets', NULL, NULL, 'fas fa-receipt compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_BAR', NULL, 'Bar', 'Bar BOT tools', NULL, NULL, NULL, NULL, NULL, 'fas fa-cocktail compact-icon', 6, 1, 1, '#7c3aed', NULL, 0),
    ('NAV_BAR_ORDER_DASH', 'NAV_BAR', 'Bar Order Dashboard', NULL, NULL, 'BOT', 'BarOrderDashboard', NULL, NULL, 'fas fa-tachometer-alt compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_BAR_BOT_DASH', 'NAV_BAR', 'BOT Dashboard', NULL, NULL, 'BOT', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_BAR_TICKETS', 'NAV_BAR', 'Bar Tickets', NULL, NULL, 'BOT', 'Tickets', NULL, NULL, 'fas fa-receipt compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_BAR_CREATE', 'NAV_BAR', 'Bar Order Create', NULL, NULL, 'BOT', 'BarOrderCreate', NULL, NULL, 'fas fa-plus compact-icon text-primary', 4, 1, 1, NULL, NULL, 0),
    ('NAV_ONLINE', NULL, 'Online', 'Online orders', NULL, NULL, NULL, NULL, NULL, 'fas fa-globe compact-icon', 7, 1, 1, '#f8b133', NULL, 0),
    ('NAV_ONLINE_DASH', 'NAV_ONLINE', 'Dashboard', NULL, NULL, 'OnlineOrder', 'Dashboard', NULL, NULL, 'fas fa-th-large compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_ONLINE_LIST', 'NAV_ONLINE', 'Order List', NULL, NULL, 'OnlineOrder', 'Index', NULL, NULL, 'fas fa-list compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_ONLINE_SOURCES', 'NAV_ONLINE', 'Sources', NULL, NULL, 'OnlineOrder', 'OrderSources', NULL, NULL, 'fas fa-external-link-alt compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_ONLINE_MAPPINGS', 'NAV_ONLINE', 'Mappings', NULL, NULL, 'OnlineOrder', 'MenuItemMappings', NULL, NULL, 'fas fa-exchange-alt compact-icon text-primary', 4, 1, 1, NULL, NULL, 0),
    ('NAV_MENU', NULL, 'Menu', 'Menu master data', NULL, NULL, NULL, NULL, NULL, 'fas fa-book-open compact-icon', 8, 1, 1, '#f8b133', NULL, 0),
    ('NAV_MENU_ITEMS', 'NAV_MENU', 'Menu Items', NULL, NULL, 'Menu', 'Index', NULL, NULL, 'fas fa-utensils compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_RECIPE_DASH', 'NAV_MENU', 'Recipe Dashboard', NULL, NULL, 'Recipe', 'Dashboard', NULL, NULL, 'fas fa-clipboard-list compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_RECIPES', 'NAV_MENU', 'All Recipes', NULL, NULL, 'Recipe', 'Index', NULL, NULL, 'fas fa-book compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_CATEGORIES', 'NAV_MENU', 'Categories', NULL, NULL, 'Category', 'Index', NULL, NULL, 'fas fa-tags compact-icon text-primary', 4, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_SUBCATEGORIES', 'NAV_MENU', 'Sub Categories', NULL, NULL, 'SubCategory', 'Index', NULL, NULL, 'fas fa-list-ul compact-icon text-primary', 5, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_INGREDIENTS', 'NAV_MENU', 'Ingredients', NULL, NULL, 'Master', 'IngredientsList', NULL, NULL, 'fas fa-carrot compact-icon text-primary', 6, 1, 1, NULL, NULL, 0),
    ('NAV_MENU_QR', 'NAV_MENU', 'QR Code Menu', NULL, NULL, 'MenuQR', 'Index', NULL, NULL, 'fas fa-qrcode compact-icon text-success', 7, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS', NULL, 'Settings', 'Administration settings', NULL, NULL, NULL, NULL, NULL, 'fas fa-cogs compact-icon', 9, 1, 1, '#f8b133', NULL, 0),
    ('NAV_SETTINGS_RESTAURANT', 'NAV_SETTINGS', 'Restaurant Settings', NULL, NULL, 'Settings', 'Index', NULL, NULL, 'fas fa-store compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_DAYCLOSING', 'NAV_SETTINGS', 'Day Closing', NULL, NULL, 'DayClosing', 'Index', NULL, NULL, 'fas fa-cash-register compact-icon text-success', 2, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_USERS', 'NAV_SETTINGS', 'Users & Roles', NULL, NULL, 'User', 'UserList', NULL, NULL, 'fas fa-users compact-icon text-primary', 3, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_BANK', 'NAV_SETTINGS', 'Bank Master', NULL, NULL, 'Bank', 'Index', NULL, NULL, 'fas fa-university compact-icon text-primary', 4, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_STATIONS', 'NAV_SETTINGS', 'Stations', NULL, NULL, 'Kitchen', 'Stations', NULL, NULL, 'fas fa-map-signs compact-icon text-primary', 5, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_MENU_BUILDER', 'NAV_SETTINGS', 'Menu Builder', 'Maintain navigation menus', NULL, 'MenuManagement', 'Index', NULL, NULL, 'fas fa-sitemap compact-icon text-info', 6, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_ROLE_MENU', 'NAV_SETTINGS', 'Role Menu Mapping', 'Assign menus to roles', NULL, 'RoleMenuMapping', 'Index', NULL, NULL, 'fas fa-project-diagram compact-icon text-warning', 7, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_ROLE_PERMISSIONS', 'NAV_SETTINGS', 'Role Permission Matrix', 'Grant CRUD permissions', NULL, 'RolePermission', 'Index', NULL, NULL, 'fas fa-user-shield compact-icon text-danger', 8, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_AUDIT', 'NAV_SETTINGS', 'Audit Trail', 'Order modification history', NULL, 'AuditTrail', 'Index', NULL, NULL, 'fas fa-history compact-icon text-warning', 9, 1, 1, NULL, NULL, 0),
    ('NAV_SETTINGS_MAIL', 'NAV_SETTINGS', 'Mail Configuration', 'Email/SMTP server settings', NULL, 'MailConfiguration', 'Index', NULL, NULL, 'fas fa-envelope-open-text compact-icon text-info', 10, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS', NULL, 'Reports', 'Reporting workspace', NULL, NULL, NULL, NULL, NULL, 'fas fa-chart-bar compact-icon', 10, 1, 1, '#f8b133', NULL, 0),
    ('NAV_REPORTS_SALES', 'NAV_REPORTS', 'Sales Reports', NULL, NULL, 'Reports', 'Sales', NULL, NULL, 'fas fa-dollar-sign compact-icon text-primary', 1, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_ORDERS', 'NAV_REPORTS', 'Order Reports', NULL, NULL, 'Reports', 'Orders', NULL, NULL, 'fas fa-shopping-cart compact-icon text-primary', 2, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_COLLECTION', 'NAV_REPORTS', 'Collection Register', NULL, NULL, 'Reports', 'CollectionRegister', NULL, NULL, 'fas fa-cash-register compact-icon text-success', 3, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_CASHCLOSING', 'NAV_REPORTS', 'Cash Closing Report', NULL, NULL, 'Reports', 'CashClosing', NULL, NULL, 'fas fa-money-check-alt compact-icon text-info', 4, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_DISCOUNT', 'NAV_REPORTS', 'Discount Report', NULL, NULL, 'Reports', 'DiscountReport', NULL, NULL, 'fas fa-percentage compact-icon text-primary', 5, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_GST', 'NAV_REPORTS', 'GST Breakup', NULL, NULL, 'Reports', 'GSTBreakup', NULL, NULL, 'fas fa-file-invoice-dollar compact-icon text-primary', 6, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_MENU', 'NAV_REPORTS', 'Menu Analysis', NULL, NULL, 'Reports', 'Menu', NULL, NULL, 'fas fa-utensils compact-icon text-primary', 7, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_KITCHEN', 'NAV_REPORTS', 'Kitchen KOT', NULL, NULL, 'Reports', 'Kitchen', NULL, NULL, 'fas fa-hamburger compact-icon text-primary', 8, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_BAR', 'NAV_REPORTS', 'Bar BOT', NULL, NULL, 'Reports', 'Bar', NULL, NULL, 'fas fa-cocktail compact-icon text-purple', 9, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_CUSTOMERS', 'NAV_REPORTS', 'Customer Reports', NULL, NULL, 'Reports', 'Customers', NULL, NULL, 'fas fa-users compact-icon text-primary', 10, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_FEEDBACK', 'NAV_REPORTS', 'Feedback Survey Report', NULL, NULL, 'Reports', 'FeedbackSurveyReport', NULL, NULL, 'fas fa-poll-h compact-icon text-primary', 11, 1, 1, NULL, NULL, 0),
    ('NAV_REPORTS_FINANCIAL', 'NAV_REPORTS', 'Financial Summary', NULL, NULL, 'Reports', 'Financial', NULL, NULL, 'fas fa-calculator compact-icon text-primary', 12, 1, 1, NULL, NULL, 0);

MERGE dbo.NavigationMenus AS target
USING @Menus AS source
    ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET
        ParentCode = source.ParentCode,
        DisplayName = source.DisplayName,
        Description = source.Description,
        Area = source.Area,
        ControllerName = source.ControllerName,
        ActionName = source.ActionName,
        RouteValues = source.RouteValues,
        CustomUrl = source.CustomUrl,
        IconCss = source.IconCss,
        DisplayOrder = source.DisplayOrder,
        IsActive = source.IsActive,
        IsVisible = source.IsVisible,
        ThemeColor = source.ThemeColor,
        ShortcutHint = source.ShortcutHint,
        OpenInNewTab = source.OpenInNewTab,
        UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Code, ParentCode, DisplayName, Description, Area, ControllerName, ActionName, RouteValues, CustomUrl, IconCss, DisplayOrder, IsActive, IsVisible, ThemeColor, ShortcutHint, OpenInNewTab)
    VALUES (source.Code, source.ParentCode, source.DisplayName, source.Description, source.Area, source.ControllerName, source.ActionName, source.RouteValues, source.CustomUrl, source.IconCss, source.DisplayOrder, source.IsActive, source.IsVisible, source.ThemeColor, source.ShortcutHint, source.OpenInNewTab);

PRINT 'Navigation menu seed complete.';

DECLARE @AdminRoleId INT = (SELECT TOP (1) Id FROM dbo.Roles WHERE Name = 'Administrator');
IF @AdminRoleId IS NOT NULL
BEGIN
    PRINT 'Ensuring Administrator role has full permissions...';

    UPDATE dbo.RoleMenuPermissions
    SET CanView = 1,
        CanAdd = 1,
        CanEdit = 1,
        CanDelete = 1,
        CanApprove = 1,
        CanPrint = 1,
        CanExport = 1,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = 0
    WHERE RoleId = @AdminRoleId;

    INSERT INTO dbo.RoleMenuPermissions (RoleId, MenuId, CanView, CanAdd, CanEdit, CanDelete, CanApprove, CanPrint, CanExport, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
    SELECT @AdminRoleId, nm.Id, 1, 1, 1, 1, 1, 1, 1, SYSUTCDATETIME(), 0, SYSUTCDATETIME(), 0
    FROM dbo.NavigationMenus nm
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.RoleMenuPermissions rmp WHERE rmp.RoleId = @AdminRoleId AND rmp.MenuId = nm.Id
    );
END
ELSE
BEGIN
    PRINT 'WARNING: Administrator role not found. Seed role permissions manually after role creation.';
END;
GO

PRINT 'Navigation authorization schema script completed.';
GO
