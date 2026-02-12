# Order Number Generation Deferred to First Item Save (Food/Bar/POS)

Date: 12 Feb 2026

## Why this change
Previously, the system generated and consumed an `OrderNumber` when the user created an order header.
If the user left/escaped without adding any menu items, an order number was still consumed.

This change defers `OrderNumber` assignment until **the first menu item is actually saved**.

## New behavior (expected)
- Creating an order (Food Order / Bar Order / POS Order) creates the order header **without consuming an order number**.
- On the **first item add/save**, the system assigns the next `OrderNumber` in format `ORD-YYYYMMDD-XXXX`.
- Subsequent item saves do **not** change the order number.

Note: The order header row may still exist without items (table turnover, audit, etc.). The key improvement is that **no order number is occupied** until at least one item is saved.

## Where the change is implemented

### Database (stored procedures)
- `usp_CreateOrder` now inserts `Orders.OrderNumber = ''` (blank)
- First item save assigns `OrderNumber`:
  - In `usp_AddOrderItem` (for form-based Add Item flow)
  - In app logic for bulk item save (`UpdateMultipleOrderItems`) which is used heavily by POS and the enhanced order-details UI

SQL upgrade script:
- See [SQL/2026-02-12_Defer_OrderNumber_To_FirstItem.sql](SQL/2026-02-12_Defer_OrderNumber_To_FirstItem.sql)

### Application (C#)
- Order creation success messages now handle a blank order number gracefully.
- Bulk item-save endpoint assigns order number when inserting at least one new item:
  - [RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs](RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs)

## Affected flows
- Food Order: create order -> add item -> order number gets assigned on first item add
- Bar Order: create order -> add item -> order number gets assigned on first item add
- POS Order: create order -> add item (via `/Order/UpdateMultipleOrderItems`) -> order number gets assigned on first item add

## Deployment / upgrade steps
1. Apply SQL changes to your DB:
   - Run [SQL/2026-02-12_Defer_OrderNumber_To_FirstItem.sql](SQL/2026-02-12_Defer_OrderNumber_To_FirstItem.sql)
2. Deploy the application changes.
3. Validate:
   - Create an order and exit without items → no `ORD-...` number should be consumed.
   - Create an order and add the first item → order gets `ORD-YYYYMMDD-XXXX`.

## Notes / compatibility
- The schema in this repo defines `Orders.OrderNumber` as `NOT NULL`. To support deferred numbering without schema changes, we store a blank string (`''`) until assignment.
- Recent-order display SQL was updated to treat blank order numbers as “missing” and fallback to `ORD-{Id}` for UI/report display.
- Order dashboards (Food + Bar) were updated to **exclude** orders where `OrderNumber` is blank, so abandoned/un-numbered orders don’t appear until the first item is saved.

## Quick sanity scenarios
- Food: create -> immediately cancel/leave -> check DB: `Orders.OrderNumber=''`
- Food: add item -> check DB: `Orders.OrderNumber like 'ORD-%'`
- POS: create via AJAX -> add item -> refresh UI -> top bar should show the assigned order number after refresh
