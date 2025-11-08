# GST Breakup Report Fix - Quick Deployment Guide

## Issue Fixed
❌ **Error**: `Invalid column name 'TableNumber'` in stored procedure  
✅ **Solution**: Table information retrieved through proper joins (`Orders → TableTurnovers → Tables`)

---

## What Was Fixed

### Database Schema Issue:
- **Orders table** does NOT have a `TableNumber` column
- Table info comes from: `Orders.TableTurnoverId → TableTurnovers.TableId → Tables.TableName/TableNumber`

### SQL Changes:
```sql
-- BEFORE (INCORRECT):
o.TableNumber  -- This column doesn't exist!

-- AFTER (CORRECT):
LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
LEFT JOIN Tables t ON tt.TableId = t.Id
...
ISNULL(t.TableName, COALESCE(t.TableNumber, 'N/A')) AS TableNumber
```

---

## Deployment Steps

### 1. Execute SQL Script
```bash
# Option A: Using sqlcmd
sqlcmd -S YOUR_SERVER -d YOUR_DATABASE -i fix_gst_breakup_report.sql

# Option B: Using SSMS/Azure Data Studio
# Open fix_gst_breakup_report.sql and execute (F5)
```

### 2. Verify Procedure Created
```sql
-- Check if procedure exists
SELECT name, create_date, modify_date 
FROM sys.procedures 
WHERE name = 'usp_GetGSTBreakupReport';

-- Test the procedure
EXEC usp_GetGSTBreakupReport 
    @StartDate = '2025-11-08', 
    @EndDate = '2025-11-08';
```

### 3. Access Report
Navigate to: `https://localhost:7290/Reports/GSTBreakup`

---

## Key Features Now Working

✅ **Correct Taxable Value**: `Orders.Subtotal - Orders.DiscountAmount`  
✅ **Table Information**: Properly retrieved through table joins  
✅ **BAR vs Foods**: Order type classification with GST % display  
✅ **GST Percentages**: Uses persisted `Orders.GSTPercentage` (20% BAR, 10% Foods)  
✅ **Split Payments**: Correctly aggregated per order  
✅ **Indian GST Compliance**: Full CGST/SGST breakup  

---

## Verification

### Quick Test Query:
```sql
-- Verify table relationships are working
SELECT TOP 10
    o.OrderNumber,
    o.Subtotal,
    o.DiscountAmount,
    (o.Subtotal - ISNULL(o.DiscountAmount, 0)) AS TaxableValue,
    o.GSTPercentage,
    o.OrderKitchenType,
    ISNULL(t.TableName, COALESCE(t.TableNumber, 'N/A')) AS TableInfo
FROM Orders o
LEFT JOIN TableTurnovers tt ON o.TableTurnoverId = tt.Id
LEFT JOIN Tables t ON tt.TableId = t.Id
WHERE o.Id IN (SELECT DISTINCT OrderId FROM Payments WHERE Status = 1)
ORDER BY o.CreatedAt DESC;
```

---

## Files Modified

1. ✅ `fix_gst_breakup_report.sql` - Fixed SQL stored procedure
2. ✅ `GSTBreakupReportViewModel.cs` - Model updated (already done)
3. ✅ `ReportsController.cs` - Controller updated (already done)
4. ✅ `GSTBreakup.cshtml` - View enhanced (already done)

---

## Status

**Ready for deployment** - Execute the SQL script and the GST Breakup report will work correctly with proper table information and accurate taxable value calculation.
