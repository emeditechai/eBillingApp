# Payment GST Persistence Fix - Complete Implementation

## Issue Description

**Problem**: Orders table was correctly storing GST percentage (20% for BAR orders, 10% for Foods), but when processing payments, the Payments table was showing incorrect GST percentages (10% instead of 20% for BAR orders).

**Root Cause**: The payment processing logic in `PaymentController.cs` was reading GST percentage from `RestaurantSettings.DefaultGSTPercentage` instead of using the persisted value from `Orders.GSTPercentage`.

## Solution Overview

Updated both `ProcessPayment` and `ProcessSplitPayments` methods to read GST percentage from the `Orders.GSTPercentage` column (which is correctly populated by `UpdateOrderFinancials` in `OrderController.cs`), with fallback to `DefaultGSTPercentage` only when the order doesn't have a persisted value.

---

## Changes Made

### 1. **ProcessPayment Method** (`PaymentController.cs`, ~line 440)

**Before**:
```csharp
// Get GST percentage from settings
using (var gstCmd = new Microsoft.Data.SqlClient.SqlCommand(
    "SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", gstConnection))
{
    var result = gstCmd.ExecuteScalar();
    if (result != null && result != DBNull.Value)
    {
        paymentGstPercentage = Convert.ToDecimal(result);
    }
}

// Get order subtotal (amount before GST)
using (var subtotalCmd = new Microsoft.Data.SqlClient.SqlCommand(
    "SELECT Subtotal FROM Orders WHERE Id = @OrderId", gstConnection))
{
    subtotalCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
    var subtotalResult = subtotalCmd.ExecuteScalar();
    if (subtotalResult != null && subtotalResult != DBNull.Value)
    {
        orderSubtotal = Convert.ToDecimal(subtotalResult);
    }
}
```

**After**:
```csharp
// Get order subtotal and persisted GST percentage (BAR orders have 20%, Foods have default %)
using (var orderCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
    SELECT 
        ISNULL(o.Subtotal, 0) AS Subtotal,
        CASE 
            WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'GSTPercentage')
                AND o.GSTPercentage IS NOT NULL AND o.GSTPercentage > 0 
            THEN o.GSTPercentage
            ELSE (SELECT ISNULL(DefaultGSTPercentage, 5.0) FROM dbo.RestaurantSettings)
        END AS GSTPercentage
    FROM Orders o
    WHERE o.Id = @OrderId", gstConnection))
{
    orderCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
    using (var reader = orderCmd.ExecuteReader())
    {
        if (reader.Read())
        {
            orderSubtotal = reader.GetDecimal(0);
            paymentGstPercentage = reader.GetDecimal(1);
        }
    }
}
```

**Key Changes**:
- Combined two separate queries into one efficient query
- Read `Orders.GSTPercentage` directly (which contains 20% for BAR, 10% for Foods)
- Only fallback to `DefaultGSTPercentage` if `GSTPercentage` column doesn't exist or is NULL/0
- Removed duplicate GST fetching code that appeared earlier in the method

---

### 2. **ProcessSplitPayments Method** (`PaymentController.cs`, ~line 1015)

**Before**:
```csharp
// Use persisted GST percentage if available, otherwise fallback to default
if (persistedGSTPerc > 0)
{
    gstPerc = persistedGSTPerc;
}

// Fallback to default GST if not persisted
if (gstPerc == 5.0m || gstPerc == 0m)
{
    using (var gstCmd = new SqlCommand("SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", conn))
    {
        var r = gstCmd.ExecuteScalar();
        if (r != null && r != DBNull.Value) 
        {
            decimal defaultGST = Convert.ToDecimal(r);
            if (gstPerc == 0m) gstPerc = defaultGST; // Only override if not persisted
        }
    }
}
```

**Problem**: The condition `if (gstPerc == 5.0m || gstPerc == 0m)` was flawed - it would re-query settings even when `gstPerc` was already correctly set to a persisted value.

**After**:
```csharp
// Use persisted GST percentage if available (BAR=20%, Foods=10%, etc.)
if (persistedGSTPerc > 0)
{
    gstPerc = persistedGSTPerc;
}

// Fallback to default GST ONLY if not persisted (0 or NULL in Orders.GSTPercentage)
if (gstPerc == 0m || gstPerc == 5.0m) // 5.0 is initial default, need to check if persisted
{
    using (var gstCmd = new SqlCommand("SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", conn))
    {
        var r = gstCmd.ExecuteScalar();
        if (r != null && r != DBNull.Value) 
        {
            decimal defaultGST = Convert.ToDecimal(r);
            // Only use default if Orders.GSTPercentage was 0 (not persisted yet)
            // Read actual persisted value again to avoid overwriting valid data
            using (var recheckCmd = new SqlCommand("SELECT ISNULL(GSTPercentage, 0) FROM Orders WHERE Id = @OrderId", conn))
            {
                recheckCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                var persistedCheck = recheckCmd.ExecuteScalar();
                decimal actualPersisted = (persistedCheck != null && persistedCheck != DBNull.Value) ? Convert.ToDecimal(persistedCheck) : 0m;
                
                // Only override if truly not persisted (0 or NULL)
                if (actualPersisted == 0m)
                {
                    gstPerc = defaultGST;
                }
                else
                {
                    gstPerc = actualPersisted; // Use persisted value (could be 20% for BAR, 10% for Foods, etc.)
                }
            }
        }
    }
}
```

**Key Changes**:
- Added recheck logic to verify if `Orders.GSTPercentage` is actually persisted
- Only use `DefaultGSTPercentage` if `Orders.GSTPercentage` is truly 0 or NULL
- Prevents overwriting valid persisted GST percentages (like 20% for BAR orders)

---

## How It Works (Complete Flow)

### Order Creation → Payment Processing

1. **Order Creation** (`OrderController.cs` → `UpdateOrderFinancials`):
   - Detects if order is BAR (via `OrderKitchenType` or `KitchenTickets.KitchenStation`)
   - Reads appropriate GST from settings:
     - BAR orders: `RestaurantSettings.BarGSTPerc` (20%)
     - Foods orders: `RestaurantSettings.DefaultGSTPercentage` (10%)
   - Calculates GST, CGST, SGST amounts
   - **Persists to Orders table**:
     - `GSTPercentage` = 20% or 10%
     - `CGSTPercentage` = 10% or 5%
     - `SGSTPercentage` = 10% or 5%
     - `GSTAmount`, `CGSTAmount`, `SGSTAmount`

2. **Payment Processing** (`PaymentController.cs` → `ProcessPayment`):
   - **NOW**: Reads `Orders.GSTPercentage` (correct BAR/Foods percentage)
   - ~~**BEFORE**: Read `RestaurantSettings.DefaultGSTPercentage` (always 10%)~~
   - Uses persisted percentage for all GST calculations
   - **Saves to Payments table**:
     - `GST_Perc` = 20% (for BAR) or 10% (for Foods)
     - `CGST_Perc` = 10% or 5%
     - `SGST_Perc` = 10% or 5%
     - `GSTAmount`, `CGSTAmount`, `SGSTAmount` (calculated with correct %)

3. **Split Payment Processing** (`PaymentController.cs` → `ProcessSplitPayments`):
   - Reads `Orders.GSTPercentage` with improved fallback logic
   - Each split payment record inherits correct GST percentage
   - All payment records show consistent GST data

---

## Expected Results

### BAR Order Example:
| Stage | Location | GST % | CGST % | SGST % | GST Amount (on ₹1000) |
|-------|----------|-------|--------|--------|----------------------|
| **Order Creation** | `Orders.GSTPercentage` | 20.00 | 10.00 | 10.00 | ₹200.00 |
| **Payment** | `Payments.GST_Perc` | 20.00 | 10.00 | 10.00 | ₹200.00 |

### Foods Order Example:
| Stage | Location | GST % | CGST % | SGST % | GST Amount (on ₹1000) |
|-------|----------|-------|--------|--------|----------------------|
| **Order Creation** | `Orders.GSTPercentage` | 10.00 | 5.00 | 5.00 | ₹100.00 |
| **Payment** | `Payments.GST_Perc` | 10.00 | 5.00 | 5.00 | ₹100.00 |

---

## Testing Checklist

- [x] **Code Changes**: Updated ProcessPayment and ProcessSplitPayments methods
- [x] **Build**: Application builds successfully
- [x] **Application Start**: Running on https://localhost:7290
- [ ] **BAR Order Test**:
  1. Create new BAR order (with BAR items)
  2. Verify `Orders.GSTPercentage` = 20%
  3. Process payment
  4. Verify `Payments.GST_Perc` = 20%
  5. Check GST amounts match (20% calculation)
- [ ] **Foods Order Test**:
  1. Create new Foods order
  2. Verify `Orders.GSTPercentage` = 10%
  3. Process payment
  4. Verify `Payments.GST_Perc` = 10%
  5. Check GST amounts match (10% calculation)
- [ ] **Split Payment Test**:
  1. Create order (BAR or Foods)
  2. Process split payment
  3. Verify all payment records have correct GST %

---

## Database Verification Queries

### Check Order GST Persistence:
```sql
SELECT 
    Id AS OrderId,
    OrderNumber,
    Subtotal,
    GSTPercentage AS [GST %],
    GSTAmount AS [GST Amount],
    CGSTPercentage AS [CGST %],
    SGSTPercentage AS [SGST %],
    TotalAmount,
    OrderKitchenType
FROM Orders
WHERE Id = <YOUR_ORDER_ID>
ORDER BY Id DESC;
```

### Check Payment GST Values:
```sql
SELECT 
    Id AS PaymentId,
    OrderId,
    Amount AS [Payment Amount],
    GST_Perc AS [GST %],
    GSTAmount AS [GST Amount],
    CGST_Perc AS [CGST %],
    SGST_Perc AS [SGST %],
    CGSTAmount,
    SGSTAmount,
    Status,
    CreatedAt
FROM Payments
WHERE OrderId = <YOUR_ORDER_ID>
ORDER BY CreatedAt DESC;
```

### Compare Orders vs Payments GST:
```sql
SELECT 
    o.Id AS OrderId,
    o.OrderNumber,
    o.OrderKitchenType,
    o.GSTPercentage AS [Order GST %],
    p.GST_Perc AS [Payment GST %],
    CASE 
        WHEN ABS(o.GSTPercentage - p.GST_Perc) < 0.01 THEN '✓ Match'
        ELSE '✗ MISMATCH'
    END AS [Status],
    o.GSTAmount AS [Order GST Amt],
    p.GSTAmount AS [Payment GST Amt]
FROM Orders o
INNER JOIN Payments p ON o.Id = p.OrderId
WHERE p.Status = 1 -- Approved payments only
ORDER BY o.Id DESC;
```

---

## Files Modified

1. **`Controllers/PaymentController.cs`**:
   - Line ~440-500: Updated `ProcessPayment` to read `Orders.GSTPercentage`
   - Line ~1015-1070: Fixed `ProcessSplitPayments` fallback logic

---

## Related Documentation

- `add_gst_columns_to_orders.sql`: Database migration script for GST columns
- `GST_Display_Fix_Summary.md`: Previous GST display fixes
- `Payment_GST_Integration_Summary.md`: Initial payment GST integration
- `Payment_Subtotal_GST_Fix_Summary.md`: Payment subtotal calculation fix

---

## Summary

This fix ensures **complete GST percentage consistency** across the entire order → payment flow:

✅ **Orders table**: Correctly stores BAR (20%) vs Foods (10%) GST  
✅ **Payments table**: Now uses persisted GST from Orders (not hardcoded default)  
✅ **Split Payments**: Correctly inherits GST from Orders table  
✅ **Reports & Bills**: Will show accurate GST data from Payments table  

**Result**: BAR orders will consistently show 20% GST throughout the system, Foods orders will show 10% GST, ensuring accurate financial reporting and compliance.
