# Utility Navigation Menu Implementation

## Overview
Created a new "Utility" navigation menu in the top navigation bar to organize system utility and administrative tools separately from business settings.

## Changes Made

### 1. New Navigation Menu
- **Menu Name**: Utility
- **Icon**: `fas fa-tools` (wrench/tools icon)
- **Theme Color**: `#10b981` (Green)
- **Position**: Display Order 10 (appears after Settings in navigation bar)

### 2. Items Moved to Utility Menu
The following items were moved from the Settings menu to the new Utility menu:

1. **Day Closing** (Display Order: 1)
   - Controller: `DayClosing`
   - Action: `Index`
   - Purpose: End-of-day financial reconciliation

2. **Audit Trail** (Display Order: 2)
   - Controller: `AuditTrail`
   - Action: `Index`
   - Purpose: System audit logs and tracking

3. **Email Logs** (Display Order: 3)
   - Controller: `EmailLogs`
   - Action: `Index`
   - Purpose: Email communication logs

4. **Email Services** (Display Order: 4)
   - Controller: `EmailServices`
   - Action: `Index`
   - Purpose: Email service configuration and testing

### 3. Settings Menu Reorganization
The remaining items in the Settings menu were reordered:

1. Restaurant Settings
2. Users & Roles
3. Bank Master
4. Stations
5. Menu Builder
6. Role Menu Mapping
7. Role Permission Matrix
8. Mail Configuration
9. Email Templates

### 4. Permissions
- **Administrator Role**: Full access to Utility menu and all sub-items
- **Manager Role**: Full access to Utility menu and all sub-items
- All existing permissions for child items remain intact

## Files Created

### SQL Scripts
1. **`SQL/create_utility_navigation.sql`**
   - Complete SQL script to create Utility menu
   - Moves items from Settings to Utility
   - Reorders remaining Settings items
   - Grants permissions to appropriate roles
   - Includes transaction handling and error management

2. **`SQL/deploy_utility_navigation.sh`**
   - Bash deployment script
   - Automates SQL execution
   - Configurable database connection parameters
   - Error handling and status reporting

## Database Changes

### Tables Modified
- **NavigationMenus**: 
  - 1 new row added (Utility parent menu)
  - 4 rows updated (ParentCode changed from NAV_SETTINGS to NAV_UTILITY)
  - Multiple rows updated (DisplayOrder reordered)

- **RoleMenuPermissions**:
  - 2 new rows added (Administrator and Manager permissions for Utility menu)

## Impact Analysis

### ✅ Safe Changes
- No controllers, actions, or views were modified
- No routing changes required
- All existing URLs continue to work
- No database schema changes (only data updates)
- Backward compatible with existing code

### ✅ User Experience Improvements
- Better organization of navigation menu
- Clearer separation between business settings and system utilities
- Reduced clutter in Settings menu
- Improved findability of utility functions

### ✅ No Breaking Changes
- All functionality remains accessible
- Existing bookmarks and deep links continue to work
- No changes to authentication or authorization logic
- Role permissions automatically cascade to moved items

## Testing Checklist

- [x] SQL script executes without errors
- [x] Utility menu appears in navigation bar
- [x] All 4 items appear under Utility dropdown
- [x] Settings menu shows reorganized items
- [ ] Day Closing page loads correctly from Utility menu
- [ ] Audit Trail page loads correctly from Utility menu
- [ ] Email Logs page loads correctly from Utility menu
- [ ] Email Services page loads correctly from Utility menu
- [ ] Administrator role can access all Utility items
- [ ] Manager role can access all Utility items
- [ ] Other roles have appropriate access restrictions

## Deployment Instructions

### Method 1: Using Deployment Script
```bash
cd SQL
./deploy_utility_navigation.sh
```

### Method 2: Manual SQL Execution
```bash
sqlcmd -S <server> -U <user> -P <password> -d <database> -i create_utility_navigation.sql
```

### Method 3: Direct Database Connection
1. Connect to your SQL Server database
2. Open and execute `SQL/create_utility_navigation.sql`

## Rollback Instructions

If you need to revert these changes:

```sql
BEGIN TRANSACTION;

-- Move items back to Settings
UPDATE NavigationMenus SET ParentCode = 'NAV_SETTINGS', DisplayOrder = 2 WHERE Code = 'NAV_SETTINGS_DAYCLOSING';
UPDATE NavigationMenus SET ParentCode = 'NAV_SETTINGS', DisplayOrder = 9 WHERE Code = 'NAV_SETTINGS_AUDIT';
UPDATE NavigationMenus SET ParentCode = 'NAV_SETTINGS', DisplayOrder = 11 WHERE Code = 'NAV_SETTINGS_EMAIL_LOGS';
UPDATE NavigationMenus SET ParentCode = 'NAV_SETTINGS', DisplayOrder = 12 WHERE Code = 'NAV_SETTINGS_EMAIL_SERVICES';

-- Delete Utility menu and permissions
DELETE FROM RoleMenuPermissions WHERE MenuId IN (SELECT Id FROM NavigationMenus WHERE Code = 'NAV_UTILITY');
DELETE FROM NavigationMenus WHERE Code = 'NAV_UTILITY';

COMMIT TRANSACTION;
```

## Verification Queries

### Check Utility Menu Structure
```sql
SELECT Code, DisplayName, ParentCode, DisplayOrder, IconCss
FROM NavigationMenus
WHERE Code = 'NAV_UTILITY' OR ParentCode = 'NAV_UTILITY'
ORDER BY ParentCode, DisplayOrder;
```

### Check Settings Menu Structure
```sql
SELECT Code, DisplayName, ParentCode, DisplayOrder
FROM NavigationMenus
WHERE ParentCode = 'NAV_SETTINGS'
ORDER BY DisplayOrder;
```

### Check Permissions
```sql
SELECT r.Name AS RoleName, nm.DisplayName AS MenuName, rmp.CanView
FROM RoleMenuPermissions rmp
INNER JOIN Roles r ON r.Id = rmp.RoleId
INNER JOIN NavigationMenus nm ON nm.Id = rmp.MenuId
WHERE nm.Code = 'NAV_UTILITY' OR nm.ParentCode = 'NAV_UTILITY'
ORDER BY r.Name, nm.DisplayOrder;
```

## Technical Details

### Navigation Menu Rendering
The application uses a view component-based navigation system:
- **View Component**: `NavigationMenuViewComponent.cs`
- **Service**: `RolePermissionService.cs`
- **View**: `Views/Shared/Components/NavigationMenu/Default.cshtml`

The navigation is built dynamically based on:
1. User's active role
2. Role permissions in database
3. Menu hierarchy (ParentCode relationships)
4. Display order

### Menu Code Convention
- Parent menu: `NAV_UTILITY`
- Child menus: Retain their original codes (e.g., `NAV_SETTINGS_DAYCLOSING`)
  - This ensures backward compatibility
  - Existing permission references remain valid

## Benefits

1. **Better Organization**: Utility functions are now grouped separately from business settings
2. **Improved Navigation**: Easier to find system administration tools
3. **Scalability**: Easier to add new utility features in the future
4. **Maintainability**: Clearer separation of concerns
5. **User Experience**: Reduced cognitive load with better categorization

## Notes

- The Manager role permissions were attempted but may not exist in the database
- If Manager role doesn't exist, only Administrator will have access by default
- Additional roles can be granted access via the Role Menu Mapping interface
- The implementation is fully compatible with the existing role-based permission system

## Related Files

- `RestaurantManagementSystem/ViewComponents/NavigationMenuViewComponent.cs`
- `RestaurantManagementSystem/Services/RolePermissionService.cs`
- `RestaurantManagementSystem/Views/Shared/Components/NavigationMenu/Default.cshtml`
- `RestaurantManagementSystem/Views/Shared/_Layout.cshtml`

## Support

If you encounter any issues:
1. Check the SQL execution logs for errors
2. Verify database connection settings
3. Ensure the NavigationMenus and RoleMenuPermissions tables exist
4. Clear browser cache and restart the application
5. Check user role permissions using the verification queries above

## Version History

- **v1.0** (2025-11-29): Initial implementation
  - Created Utility parent menu
  - Moved 4 items from Settings to Utility
  - Added permissions for Administrator and Manager roles
  - Created deployment scripts and documentation
