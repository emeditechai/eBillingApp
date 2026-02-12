# POS Order Discount (Amount / Percent) — One-Screen Implementation

## Goal
Allow cashiers to apply a discount **directly on the POS Order screen** (single-screen flow) as either:
- **Amount** (₹)
- **Percentage** (%)

This matches the existing “standard” backend mechanism already used on the Payment screen and persists the discount into the Order so GST/totals update correctly.

---

## Where Implemented

### UI
- Page: `RestaurantManagementSystem/Views/Order/POSOrder.cshtml`
- Location: Right panel → **Cart & Payment** → Totals section

Added a **Discount (Optional)** control block containing:
- Amount/Percent toggle
- Discount entry input
- **Apply** button (turns into **Edit** after applying)
- **Cancel** button (visible only when a discount exists)

The page already displays totals:
- `Subtotal`, `GST`, `Discount`, `Total`, `Payable (Rounded)`, `Round Off`

The new discount controls live just below the Discount row.

---

## Backend (Reused / No New DB Changes)

### Endpoints Used (existing)
- `POST /Payment/ApplyDiscount`
  - Params: `orderId`, `discount`, `discountType` (`amount`|`percent`)
  - Persists discount into `Orders.DiscountAmount`
  - Recalculates GST + totals via `PaymentController.UpdateOrderFinancials`

- `POST /Payment/CancelDiscount`
  - Params: `orderId`
  - Sets `Orders.DiscountAmount = 0`
  - Recalculates GST + totals via `PaymentController.UpdateOrderFinancials`

These endpoints are already used by the Payment flow (`/Payment/Index/{id}`), so POS uses the same standard persistence logic.

### Important Behavior: Replace (Not Stack)
`PaymentController.ApplyDiscount` adds the new discount onto any existing discount in the order.

On POS, to provide an “edit/update” experience without stacking, the POS UI does:
1. If an order already has a discount > 0 → call `CancelDiscount`
2. Then call `ApplyDiscount` with the new value

So the discount is effectively **replaced** (not accumulated).

---

## Client-Side Flow (One Screen)

### Apply
1. User selects `Amount` or `Percent`
2. Enters value and clicks **Apply**
3. POS calls `/Payment/ApplyDiscount` with anti-forgery token
4. On success, POS calls `refresh()` (already on the page)
   - `refresh()` fetches `/Order/GetPOSOrderJson?orderId=...`
   - Updates totals and cart without navigation
5. UI locks and Apply button becomes **Edit**

### Edit
- Click **Edit** to unlock the input and change the discount.
- Clicking **Apply** again will **replace** the previous discount (Cancel then Apply).

### Cancel
1. Click **Cancel**
2. POS calls `/Payment/CancelDiscount`
3. On success, POS calls `refresh()` and hides Cancel button

---

## Security
- Both endpoints require `[ValidateAntiForgeryToken]`
- POS sends:
  - Header: `RequestVerificationToken`
  - Body: `__RequestVerificationToken`

Token is retrieved from the existing hidden anti-forgery field already present on the POS page.

---

## Data Persisted
- `Orders.DiscountAmount` — persisted discount amount (₹)

GST and totals are recalculated server-side and returned via `GetPOSOrderJson`.

---

## Testing Checklist

1. Open POS Order page: `/Order/POSOrder`
2. Create a Takeout/Delivery order and add items
3. Apply discount as **Amount**:
   - Enter e.g. `50` → Apply
   - Verify totals update immediately (no page navigation)
4. Apply discount as **Percent**:
   - Click Edit, select Percent, enter `10` → Apply
   - Verify discount and GST recalculated
5. Cancel discount:
   - Click Cancel → totals return to no-discount values
6. Proceed with payment and ensure remaining/payable logic stays correct

---

## Files Changed
- `RestaurantManagementSystem/Views/Order/POSOrder.cshtml`
- `POS_Order_Discount_Implementation.md`
