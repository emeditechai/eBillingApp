# OrderItems per-item GST save/update (Order Details)

Date: 2026-01-04

## Goal
Persist menu-item-wise GST details in `dbo.OrderItems` when adding/editing items from the Order Details page, without changing the existing order-level GST logic.

## New/Updated Columns (dbo.OrderItems)
- `isGstApplicable` (bit)
- `GST_Per` (numeric(12,2))
- `GST_Amount` (numeric(12,2))
- `CGST_Perc` (numeric(12,2))
- `CGST_Amount` (numeric(12,2))
- `SGST_Perc` (numeric(12,2))
- `SGST_Amount` (numeric(12,2))

## Rules Implemented
1. **GST % must follow existing logic** (same as order totals):
   - Determine whether order is **BAR** (inclusive GST) vs **Foods** (exclusive GST) using the same detection logic already used in `UpdateOrderFinancials`.
   - Get GST % from `dbo.RestaurantSettings`:
     - Foods: `DefaultGSTPercentage`
     - Bar: `BarGSTPerc` (fallback to `DefaultGSTPercentage` if `BarGSTPerc` column doesn’t exist)

2. **GST applicability must be menu-item-wise**:
   - `OrderItems.isGstApplicable` is set from `MenuItems.IsGstApplicable`.
   - If not applicable, all per-item GST % and amounts are stored as `0`.

3. **Order-level + Payment GST respects `OrderItems.isGstApplicable`**:
  - GST is calculated **only** on the GST-applicable portion of the order.
  - Non-GST items (`isGstApplicable = 0`) do **not** contribute to `Orders.TaxAmount` / GST in payment calculations.

3. **Per-item GST amount calculation**
   - If Foods (exclusive):
     - `GST_Amount = ROUND(Subtotal * GST_Per / 100, 2)`
   - If Bar (inclusive):
     - Extract GST component from `Subtotal`:
     - `GST_Amount = ROUND(Subtotal - ROUND(Subtotal / (1 + GST_Per/100), 2), 2)`
   - Split equally:
     - `CGST_Perc = GST_Per/2`, `SGST_Perc = GST_Per/2`
     - `CGST_Amount = ROUND(GST_Amount/2, 2)`
     - `SGST_Amount = GST_Amount - CGST_Amount` (ensures exact sum)

## Code Changes
Changes are in:
- [RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs](RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs)
- [RestaurantManagementSystem/RestaurantManagementSystem/Controllers/PaymentController.cs](RestaurantManagementSystem/RestaurantManagementSystem/Controllers/PaymentController.cs)

### New helper
- `UpdateOrderItemGstDetails(orderId, connection, transaction)`
  - Updates OrderItems columns **only if** the required GST columns exist (schema-safe)
  - Uses `MenuItems.IsGstApplicable` for applicability
  - Uses `RestaurantSettings` + BAR/Foods logic for GST %

### Hook points (where it runs)
- `AddItem` (after inserting item, before `UpdateOrderFinancials`)
- `UpdateOrderItemQty` (after updating qty/subtotal)
- `UpdateMultipleOrderItems` (Order Details "Save" button)
- `SubmitOrder`
- `CancelOrderItem`

### Payment alignment
- Single payment (`ProcessPayment`) calculates GST using the GST-applicable share derived from `OrderItems.isGstApplicable`.
- Split payment (`ProcessSplitPayments`) does the same when calculating fresh GST (and also when updating Orders during discount application).

### Process Payment screen (GET/UI preview)
- The Process Payment page ([RestaurantManagementSystem/RestaurantManagementSystem/Views/Payment/ProcessPayment.cshtml](RestaurantManagementSystem/RestaurantManagementSystem/Views/Payment/ProcessPayment.cshtml)) previously recomputed GST in JavaScript as `discountedSubtotal * GST%`.
- It now recomputes GST as `discountedSubtotal * GST% * GstApplicableShare`, where `GstApplicableShare` is derived from `OrderItems.isGstApplicable` (schema-safe via `COL_LENGTH`).

### BAR orders
- BAR orders are supported end-to-end (inclusive pricing):
  - `Orders.Subtotal` is stored as GST-exclusive base for applicable items.
  - Payment GET fallback GST calculation (when Orders GST columns aren’t present/populated) uses BAR GST% and the same `isGstApplicable` share logic.

## Notes / Compatibility
- The helper checks existence of GST columns in `dbo.OrderItems` before running.
- Existing order-total GST logic (`UpdateOrderFinancials`) is untouched.

## Quick verification checklist
1. Add a GST-applicable item → OrderItems GST fields populated.
2. Add a non-GST item (`MenuItems.IsGstApplicable = 0`) → per-item GST fields become 0.
3. Edit quantity / Save Order Details → per-item GST recalculated.
4. Bar order vs Foods order → per-item GST follows inclusive vs exclusive behavior.
