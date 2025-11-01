#!/bin/bash

# BOT KitchenStation Setup Script
# This script runs all required SQL files in the correct order
# to add KitchenStation filtering to BOT system

echo "======================================"
echo "BOT KitchenStation Setup Script"
echo "======================================"
echo ""

# Database connection details (modify as needed)
DB_SERVER="198.38.81.123,1433"
DB_NAME="RestaurantManagementDB"
DB_USER="your_username"
DB_PASSWORD="your_password"

# SQL files to execute in order
SQL_FILES=(
    "SQL/Add_KitchenStation_To_BOT.sql"
    "SQL/Update_GetBOTsByStatus_Procedure.sql"
    "SQL/Update_GetBOTDashboardStats_Procedure.sql"
)

echo "This script will execute the following SQL files:"
for file in "${SQL_FILES[@]}"; do
    echo "  - $file"
done
echo ""
echo "Press Enter to continue or Ctrl+C to cancel..."
read

# Check if sqlcmd is available
if ! command -v sqlcmd &> /dev/null; then
    echo "ERROR: sqlcmd is not installed or not in PATH"
    echo "Please install SQL Server command line tools"
    echo ""
    echo "Alternatively, run the SQL files manually in this order:"
    for file in "${SQL_FILES[@]}"; do
        echo "  $file"
    done
    exit 1
fi

# Execute each SQL file
for sql_file in "${SQL_FILES[@]}"; do
    if [ ! -f "$sql_file" ]; then
        echo "ERROR: File not found: $sql_file"
        exit 1
    fi
    
    echo ""
    echo "Executing: $sql_file"
    echo "--------------------------------------"
    
    sqlcmd -S "$DB_SERVER" -d "$DB_NAME" -U "$DB_USER" -P "$DB_PASSWORD" -i "$sql_file"
    
    if [ $? -eq 0 ]; then
        echo "✓ SUCCESS: $sql_file executed successfully"
    else
        echo "✗ ERROR: Failed to execute $sql_file"
        echo "Please check the error message above and fix before continuing"
        exit 1
    fi
done

echo ""
echo "======================================"
echo "All SQL files executed successfully!"
echo "======================================"
echo ""
echo "Next steps:"
echo "1. Build the application: dotnet build"
echo "2. Restart the application"
echo "3. Test Bar Order with Fire to Bar functionality"
echo "4. Verify BOT tickets appear in Bar Dashboard only"
echo ""
echo "For detailed testing instructions, see:"
echo "BOT_KitchenStation_Implementation.md"
echo ""
