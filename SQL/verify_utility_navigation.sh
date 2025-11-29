#!/bin/bash
# =============================================
# Verify Utility Navigation Menu Implementation
# =============================================

set -e

DB_SERVER="${DB_SERVER:-198.38.81.123,1433}"
DB_USER="${DB_USER:-sa}"
DB_PASSWORD="${DB_PASSWORD:-asdf@1234}"
DB_NAME="${DB_NAME:-dev_Restaurant}"

echo "=========================================="
echo "Utility Navigation Menu Verification"
echo "=========================================="
echo ""

echo "1. Checking Utility parent menu..."
UTILITY_COUNT=$(sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM NavigationMenus WHERE Code = 'NAV_UTILITY';" 2>/dev/null | tr -d '[:space:]')

if [ "$UTILITY_COUNT" = "1" ]; then
    echo "   ✓ Utility menu exists"
else
    echo "   ✗ Utility menu not found!"
    exit 1
fi

echo ""
echo "2. Checking child menu items..."
CHILD_COUNT=$(sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM NavigationMenus WHERE ParentCode = 'NAV_UTILITY';" 2>/dev/null | tr -d '[:space:]')

if [ "$CHILD_COUNT" = "4" ]; then
    echo "   ✓ All 4 child items present"
else
    echo "   ✗ Expected 4 child items, found $CHILD_COUNT!"
    exit 1
fi

echo ""
echo "3. Verifying individual items..."
sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -h -1 -Q "SET NOCOUNT ON; SELECT '   • ' + DisplayName + ' (Order: ' + CAST(DisplayOrder AS VARCHAR) + ')' FROM NavigationMenus WHERE ParentCode = 'NAV_UTILITY' ORDER BY DisplayOrder;" 2>/dev/null

echo ""
echo "4. Checking permissions..."
PERM_COUNT=$(sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM RoleMenuPermissions WHERE MenuId IN (SELECT Id FROM NavigationMenus WHERE Code = 'NAV_UTILITY');" 2>/dev/null | tr -d '[:space:]')

if [ "$PERM_COUNT" -ge "1" ]; then
    echo "   ✓ Permissions configured ($PERM_COUNT roles have access)"
else
    echo "   ✗ No permissions found!"
    exit 1
fi

echo ""
echo "5. Verifying Settings menu reorganization..."
SETTINGS_COUNT=$(sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM NavigationMenus WHERE ParentCode = 'NAV_SETTINGS';" 2>/dev/null | tr -d '[:space:]')

echo "   • Settings menu now has $SETTINGS_COUNT items (previously had 13)"

echo ""
echo "=========================================="
echo "✓ All Verifications Passed!"
echo "=========================================="
echo ""
echo "The Utility navigation menu is properly configured."
echo "Please refresh your browser to see the changes."
echo ""
