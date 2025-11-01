-- Check OrderItems table structure
USE [dev_Restaurant]
GO

PRINT '=== OrderItems Table Columns ==='
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.OrderItems')
ORDER BY c.column_id;
GO

PRINT ''
PRINT '=== Check table names with Ticket in them ==='
SELECT 
    name AS TableName
FROM sys.tables
WHERE name LIKE '%Ticket%'
ORDER BY name;
GO
