#!/bin/bash

# Email Services Navigation Deployment Script
# Reads credentials from appsettings.json and deploys the navigation menu

echo "=========================================="
echo "Email Services Navigation Deployment"
echo "=========================================="

# Set the base path
BASE_PATH="/Users/abhikporel/dev/Restaurantapp"
APP_SETTINGS="$BASE_PATH/RestaurantManagementSystem/RestaurantManagementSystem/appsettings.json"
SQL_FILE="$BASE_PATH/SQL/add_email_services_navigation.sql"

# Extract credentials from appsettings.json
echo "Reading credentials from appsettings.json..."

# Extract connection string
CONNECTION_STRING=$(grep -A 1 "DefaultConnection" "$APP_SETTINGS" | grep "Server" | sed 's/.*"Server=tcp:\([^;]*\);.*User Id=\([^;]*\);Password=\([^;]*\);.*/\1|\2|\3/')

# Parse the connection string
SERVER=$(echo "$CONNECTION_STRING" | cut -d'|' -f1)
USER=$(echo "$CONNECTION_STRING" | cut -d'|' -f2)
PASSWORD=$(echo "$CONNECTION_STRING" | cut -d'|' -f3)
DATABASE="dev_Restaurant"

echo "Server: $SERVER"
echo "User: $USER"
echo "Database: $DATABASE"
echo ""

# Deploy the SQL script
echo "Deploying Email Services navigation..."
sqlcmd -S "$SERVER" -U "$USER" -P "$PASSWORD" -d "$DATABASE" -i "$SQL_FILE" -C

if [ $? -eq 0 ]; then
    echo ""
    echo "=========================================="
    echo "✓ Email Services navigation deployed successfully!"
    echo "=========================================="
else
    echo ""
    echo "=========================================="
    echo "✗ Deployment failed. Please check the error messages above."
    echo "=========================================="
    exit 1
fi
