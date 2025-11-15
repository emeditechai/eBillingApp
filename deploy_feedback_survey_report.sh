#!/bin/bash

# Feedback Survey Report - Deployment Script
# This script deploys the stored procedure for the Feedback Survey Report

echo "========================================="
echo "Feedback Survey Report Deployment"
echo "========================================="
echo ""

# Database connection details
DB_SERVER="localhost"
DB_NAME="RestaurantDB"
DB_USER="sa"

# Prompt for password
echo "Enter SQL Server password for user '$DB_USER':"
read -s DB_PASSWORD

echo ""
echo "Deploying Feedback Survey Report Stored Procedure..."
echo ""

# Deploy the stored procedure
sqlcmd -S $DB_SERVER -d $DB_NAME -U $DB_USER -P $DB_PASSWORD -i "SQL/FeedbackSurveyReport_SP.sql"

if [ $? -eq 0 ]; then
    echo ""
    echo "✓ Stored Procedure deployed successfully!"
    echo ""
    echo "========================================="
    echo "Deployment completed!"
    echo "========================================="
    echo ""
    echo "You can now access the report from:"
    echo "Reports → Feedback Survey Report"
    echo ""
else
    echo ""
    echo "✗ Error deploying stored procedure"
    echo "Please check the SQL file and database connection"
    echo ""
    exit 1
fi
