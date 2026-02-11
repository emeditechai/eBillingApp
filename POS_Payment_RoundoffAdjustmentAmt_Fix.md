# POS Payment – Persist RoundoffAdjustmentAmt

## Problem
On `/Order/POSOrder`, the UI shows a bill round-off amount (e.g., Total ₹52.50 → Payable (Rounded) ₹53.00 → Round Off ₹0.50).

However, when completing payment from the POS Order page, the inserted row in `dbo.Payments` was saving `RoundoffAdjustmentAmt` as `0`.

## Root cause
The POS Order page uses a **rounded** remaining amount for cashier UX.

Server-side `Order.RemainingAmount` is computed as:
- `PayableTotal = ROUND(Order.TotalAmount, 0)` (nearest rupee)
- `RemainingAmount = PayableTotal - SUM(Payments.Amount + TipAmount + RoundoffAdjustmentAmt)`

So for a ₹52.50 bill with no payments, `RemainingAmount` becomes `₹53.00`.

The POS JS computed payment roundoff as:
- `RoundoffAdjustmentAmt = (EnteredAmount - RemainingAmount)`

That becomes `53.00 - 53.00 = 0.00`, even though the bill’s true roundoff is `+0.50`.

## Fix
In `PaymentController.ProcessPayment` (POST), the server now normalizes the POS payment inputs when needed:

- Computes canonical remaining:
  - `CanonicalRemaining = Orders.TotalAmount - SUM(approved Payments.Amount + TipAmount)`
- Computes implied roundoff:
  - `ImpliedRoundoff = SubmittedAmount - CanonicalRemaining`
- When the client submits `RoundoffAdjustmentAmt = 0` and it looks like a POS settlement (within ±0.50), the controller sets:
  - `model.OriginalAmount = CanonicalRemaining`
  - `model.RoundoffAdjustmentAmt = ImpliedRoundoff`

This ensures `dbo.Payments.RoundoffAdjustmentAmt` is saved correctly for POS payments.

## Verification
1. Create an order where total is not a whole rupee (e.g., ₹52.50).
2. Complete payment from `/Order/POSOrder`.
3. Verify in DB:
   - Latest `Payments` row has `RoundoffAdjustmentAmt = +0.50` (or the applicable amount).
4. Print POS bill and confirm roundoff line matches.

## Notes
- Complementary payments are excluded from this normalization.
- The normalization is non-blocking: if anything fails, payment still proceeds.
