-- Add Email Templates to Navigation Menu
USE dev_Restaurant;
GO

-- Insert navigation menu entry for Email Templates
IF NOT EXISTS (SELECT 1 FROM NavigationMenus WHERE Code = 'NAV_SETTINGS_EMAIL_TEMPLATES')
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
        'NAV_SETTINGS_EMAIL_TEMPLATES',   -- Code
        'NAV_SETTINGS',                   -- ParentCode (Settings menu)
        'Email Templates',                -- DisplayName
        'Create and manage email templates for campaigns', -- Description
        'EmailTemplates',                 -- ControllerName
        'Index',                          -- ActionName
        'fas fa-file-alt compact-icon text-success', -- IconCss
        13,                               -- DisplayOrder (after Email Services which is 12)
        1,                                -- IsActive
        1,                                -- IsVisible
        GETDATE(),                        -- CreatedAt
        GETDATE()                         -- UpdatedAt
    );
    
    PRINT 'Navigation menu entry for Email Templates created successfully';
END
ELSE
BEGIN
    PRINT 'Navigation menu entry for Email Templates already exists';
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
WHERE n.Code IN ('NAV_SETTINGS', 'NAV_SETTINGS_MAIL_CONFIG', 'NAV_SETTINGS_EMAIL_LOGS', 'NAV_SETTINGS_EMAIL_SERVICES', 'NAV_SETTINGS_EMAIL_TEMPLATES')
ORDER BY 
    CASE WHEN n.ParentCode IS NULL THEN 0 ELSE 1 END,
    n.DisplayOrder;
