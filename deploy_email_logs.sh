#!/bin/bash

# Email Log Implementation Deployment Script
# This script deploys the Email Log feature to track all email sending attempts

echo "=========================================="
echo "Email Log Feature Deployment"
echo "=========================================="
echo ""

# Check if we're in the correct directory
if [ ! -d "RestaurantManagementSystem" ]; then
    echo "Error: Please run this script from the Restaurantapp root directory"
    exit 1
fi

# Step 1: Create Email Log table
echo "Step 1: Creating Email Log database table..."
sqlcmd -S 198.38.81.123,1433 -d dev_Restaurant -U dev_Restaurant -P 'admin@emedhi' -i SQL/create_email_log_table.sql
if [ $? -eq 0 ]; then
    echo "✓ Email Log table created successfully"
else
    echo "✗ Failed to create Email Log table"
    exit 1
fi
echo ""

# Step 2: Add Email Logs navigation menu
echo "Step 2: Adding Email Logs navigation menu..."
sqlcmd -S 198.38.81.123,1433 -d dev_Restaurant -U dev_Restaurant -P 'admin@emedhi' -i SQL/add_email_logs_navigation.sql
if [ $? -eq 0 ]; then
    echo "✓ Email Logs navigation menu added successfully"
else
    echo "✗ Failed to add Email Logs navigation menu"
    exit 1
fi
echo ""

# Step 3: Build the project
echo "Step 3: Building the project..."
dotnet build RestaurantManagementSystem/RestaurantManagementSystem/RestaurantManagementSystem.csproj
if [ $? -eq 0 ]; then
    echo "✓ Project built successfully"
else
    echo "✗ Project build failed"
    exit 1
fi
echo ""

echo "=========================================="
echo "Deployment completed successfully!"
echo "=========================================="
echo ""
echo "Next Steps:"
echo "1. Restart your application"
echo "2. Navigate to Settings → Email Logs to view email history"
echo "3. Test email sending from Mail Configuration page"
echo "4. Check Email Logs to see success/failure entries"
echo ""
echo "Features:"
echo "• Automatic logging of all email attempts"
echo "• Success and failure tracking with error details"
echo "• SMTP configuration used for each attempt"
echo "• Processing time measurement"
echo "• Filtering by status and email address"
echo "• Detailed view of email body and error messages"
echo ""
