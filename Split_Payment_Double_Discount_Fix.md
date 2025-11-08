# Split Payment Double-Discount Bug Fix

## Issue Description
When processing split payments on an order with a previously applied discount, the discount was being doubled, resulting in incorrect totals and negative remaining amounts.

### Observed Behavior
**Example Order (Order #1294)**:
- Subtotal: ₹380.00
- Applied Discount: ₹76.00 (should be ₹76.00)
- **BUG**: Discount showing as -₹152.00 (doubled!)
- GST (10%): ₹30.40
- Total: ₹258.40
- Paid: ₹334.40
- **BUG**: Remaining: -₹76.00 (should be ₹0.00)

### Root Cause
The `ProcessSplitPayments` method in `PaymentController.cs` was **adding** the discount amount to the existing discount in the Orders table:

```sql
DECLARE @NewDiscountAmount DECIMAL(18,2) = @CurrentDiscount + @Disc;
```

This caused the same double-discount issue we previously fixed in the regular `ProcessPayment` method. When a user:
1. Applied discount on Payment/Index page (discount persisted to Orders.DiscountAmount)
2. Proceeded to split payment
3. The split payment logic added the discount again to the already-persisted discount

**Result**: Discount doubled (₹76 + ₹76 = ₹152)

## Implementation Date
November 8, 2025

## Changes Made

### File Modified
`/Controllers/PaymentController.cs`

### Method: ProcessSplitPayments
**Location**: Lines 1100-1137 (discount pre-update section)

### The Fix

**Before** (Lines 1114-1121):
```csharp
DECLARE @NewDiscountAmount DECIMAL(18,2) = @CurrentDiscount + @Disc;
DECLARE @NetSubtotal DECIMAL(18,2) = @CurrentSubtotal - @NewDiscountAmount;
IF @NetSubtotal < 0 SET @NetSubtotal = 0;
DECLARE @NewGSTAmount DECIMAL(18,2) = ROUND(@NetSubtotal * @GSTPerc / 100, 2);
DECLARE @NewTotalAmount DECIMAL(18,2) = ROUND(@NetSubtotal + @NewGSTAmount + @CurrentTipAmount, 0);

UPDATE Orders 
SET DiscountAmount = @NewDiscountAmount, ...
```

**After** (with conditional check):
```csharp
-- Only apply discount if not already persisted (avoid double-application)
-- If discount already exists and matches, skip update
IF @CurrentDiscount = 0 OR ABS(@CurrentDiscount - @Disc) > 0.01
BEGIN
    DECLARE @NewDiscountAmount DECIMAL(18,2) = @Disc; -- Direct assignment, not addition
    DECLARE @NetSubtotal DECIMAL(18,2) = @CurrentSubtotal - @NewDiscountAmount;
    IF @NetSubtotal < 0 SET @NetSubtotal = 0;
    DECLARE @NewGSTAmount DECIMAL(18,2) = ROUND(@NetSubtotal * @GSTPerc / 100, 2);
    DECLARE @NewTotalAmount DECIMAL(18,2) = ROUND(@NetSubtotal + @NewGSTAmount + @CurrentTipAmount, 0);

    UPDATE Orders 
    SET DiscountAmount = @NewDiscountAmount, ...
END
```

### Key Changes
1. **Added Conditional Check**: 
   - Only updates if `@CurrentDiscount = 0` (no discount exists)
   - OR if `ABS(@CurrentDiscount - @Disc) > 0.01` (discount amount differs)

2. **Direct Assignment Instead of Addition**:
   - Changed from: `@CurrentDiscount + @Disc`
   - Changed to: `@Disc`

3. **Wrapped in BEGIN/END Block**:
   - Updates only execute when condition is true
   - Prevents modification when discount already persisted

## How It Works

### Scenario 1: Discount Already Applied on Payment/Index
1. User applies ₹76 discount on Payment/Index → `Orders.DiscountAmount = 76`
2. User proceeds to split payment with same ₹76 discount
3. **Old Logic**: `@NewDiscountAmount = 76 + 76 = 152` ❌
4. **New Logic**: Check finds discount exists and matches → **SKIP UPDATE** ✅
5. Result: Discount remains ₹76 (correct)

### Scenario 2: Discount Entered Directly in Split Payment
1. No discount on Payment/Index → `Orders.DiscountAmount = 0`
2. User enters ₹50 discount in split payment form
3. Condition: `@CurrentDiscount = 0` → TRUE
4. Updates: `@NewDiscountAmount = 50`
5. Result: Discount set to ₹50 (correct)

### Scenario 3: Different Discount Amount
1. Payment/Index had ₹50 discount → `Orders.DiscountAmount = 50`
2. User changes to ₹76 in split payment
3. Condition: `ABS(50 - 76) = 26 > 0.01` → TRUE
4. Updates: `@NewDiscountAmount = 76`
5. Result: Discount updated to ₹76 (correct)

## Technical Details

### Matching Logic from ProcessPayment Fix
This fix mirrors the earlier fix applied to the single `ProcessPayment` method (around line 789), ensuring consistent behavior across both payment flows.

### Tolerance Check (₹0.01)
- Uses `ABS(@CurrentDiscount - @Disc) > 0.01` to handle floating-point precision
- Considers discounts "matching" if within 1 paisa difference
- Prevents unnecessary updates for trivial rounding differences

### Transaction Safety
- Part of existing SQL transaction in ProcessSplitPayments
- Rollback on error maintains data integrity
- No additional transaction logic needed

## Testing Results

### Build Status
✅ **Build Succeeded**

### Expected Behavior After Fix
**Order with Pre-Applied Discount**:
- Subtotal: ₹380.00
- Discount: -₹76.00 (not doubled)
- Discounted Subtotal: ₹304.00
- GST (10%): ₹30.40
- Total: ₹334.40
- After Split Payment of ₹334.40: Remaining ₹0.00 ✅

## Impact Analysis

### Affected Scenarios
1. **Split Payment with Pre-Applied Discount** (Primary Fix)
2. **Split Payment with Manual Discount Entry** (Unaffected)
3. **Split Payment with Modified Discount** (Now handles correctly)

### Not Affected
- Regular single payments (already fixed)
- Orders without discounts
- Cash/card payments without splits
- Discount cancellation

## Related Issues Fixed Previously
1. **ProcessPayment Double-Discount** (Fixed November 8, 2025)
   - Same root cause in single payment flow
   - Fixed by changing `@CurrentDiscount + @Disc` to `@Disc` with conditional check

## Files Modified
1. `/Controllers/PaymentController.cs` - ProcessSplitPayments method (lines 1100-1137)

## Dependencies
- Requires previous fix in ProcessPayment method (line 789)
- Uses same conditional logic pattern
- No database schema changes needed

## Future Recommendations

### Code Consolidation
Consider extracting discount application logic into a shared method to avoid duplication:
```csharp
private void ApplyDiscountToOrder(int orderId, decimal discount, decimal gstPerc, SqlConnection conn, SqlTransaction tx = null)
```

This would:
- Eliminate code duplication between ProcessPayment and ProcessSplitPayments
- Ensure consistent behavior across all payment flows
- Simplify future maintenance

### Audit Trail
Consider logging discount changes:
- When discount is applied
- When discount is skipped (already persisted)
- When discount is modified
- User who made the change

## Verification Steps

### Manual Testing
1. **Test Pre-Applied Discount**:
   - Apply discount on Payment/Index
   - Proceed to split payment
   - Verify discount not doubled
   - Verify totals correct
   
2. **Test Direct Split Discount**:
   - Skip Payment/Index discount
   - Enter discount in split payment
   - Verify discount applied once
   
3. **Test Discount Modification**:
   - Apply ₹50 on Payment/Index
   - Change to ₹100 in split payment
   - Verify final discount is ₹100

### Expected Results
- ✅ Discount amounts match between Payment/Index and split payment
- ✅ No duplicate discount application
- ✅ Remaining amount calculates correctly (₹0 when fully paid)
- ✅ Order completion status updates properly

## Notes
- Fix applied to split payment flow only
- Regular payment flow already fixed in previous commit
- No stored procedure changes required
- Compatible with existing discount approval workflow
- Works with both amount and percentage discounts
