#!/bin/bash

# BOT Setup Execution Script
# This script executes the BOT_Setup.sql file on your SQL Server database

echo "========================================="
echo "BOT Module Database Setup"
echo "========================================="
echo ""

# Database connection details (modify these)
SERVER="198.38.81.123,1433"
DATABASE="YourDatabaseName"  # Change this to your actual database name
USERNAME="YourUsername"       # Change this to your username
PASSWORD="YourPassword"       # Change this to your password

# SQL file path
SQL_FILE="/Users/abhikporel/dev/Restaurantapp/RestaurantManagementSystem/RestaurantManagementSystem/SQL/BOT_Setup.sql"

echo "Connecting to: $SERVER"
echo "Database: $DATABASE"
echo "SQL File: $SQL_FILE"
echo ""
echo "Executing BOT_Setup.sql..."
echo ""

# Execute the SQL script
sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -d "$DATABASE" -i "$SQL_FILE"

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================="
    echo "✅ BOT Setup completed successfully!"
    echo "========================================="
    echo ""
    echo "Next steps:"
    echo "1. Go to: http://localhost:7290/BOT/CheckSetup"
    echo "2. Verify all components show 'EXISTS'"
    echo "3. Create Bar menu group if missing"
    echo "4. Go to BOT Dashboard"
    echo ""
else
    echo ""
    echo "========================================="
    echo "❌ Error executing BOT Setup"
    echo "========================================="
    echo ""
    echo "Please check:"
    echo "- Database connection details are correct"
    echo "- You have permissions to create tables/procedures"
    echo "- SQL Server is accessible from this machine"
    echo ""
fi
