#!/bin/bash
# =============================================
# Deploy Utility Navigation Menu
# =============================================

set -e

# Database connection details
DB_SERVER="${DB_SERVER:-198.38.81.123,1433}"
DB_USER="${DB_USER:-sa}"
DB_PASSWORD="${DB_PASSWORD:-asdf@1234}"
DB_NAME="${DB_NAME:-dev_Restaurant}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SQL_FILE="$SCRIPT_DIR/create_utility_navigation.sql"

echo "=========================================="
echo "Utility Navigation Menu Deployment"
echo "=========================================="
echo ""
echo "Server: $DB_SERVER"
echo "Database: $DB_NAME"
echo "SQL Script: $SQL_FILE"
echo ""

if [ ! -f "$SQL_FILE" ]; then
    echo "Error: SQL file not found: $SQL_FILE"
    exit 1
fi

echo "Deploying Utility navigation menu..."
echo ""

cat "$SQL_FILE" | sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" -C -b

if [ $? -eq 0 ]; then
    echo ""
    echo "=========================================="
    echo "✓ Deployment completed successfully!"
    echo "=========================================="
    echo ""
    echo "The Utility menu is now available with:"
    echo "  • Day Closing"
    echo "  • Audit Trail"
    echo "  • Email Logs"
    echo "  • Email Services"
    echo ""
    echo "Please restart your application to see the changes."
else
    echo ""
    echo "=========================================="
    echo "✗ Deployment failed!"
    echo "=========================================="
    exit 1
fi
