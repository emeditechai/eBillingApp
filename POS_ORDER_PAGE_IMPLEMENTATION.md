# POS Order Page (Takeout/Delivery) – Implementation Notes

## Goal
Provide a single-screen POS-style workflow to:
- Create **Takeout (1)** or **Delivery (2)** orders
- Add/edit/cancel items on the same screen
- Continue to payment via the existing payment flow

This feature is intentionally **not added to navigation**.

## Routes
- `GET /Order/POSOrder` – opens the POS Order page (create mode)
- `GET /Order/POSOrder?orderId={id}` – opens existing order in POS mode
- `POST /Order/CreatePOSOrder` – creates a Takeout/Delivery order using existing stored procedure logic

Existing endpoints reused:
- `POST /Order/UpdateMultipleOrderItems?orderId={id}` – bulk update + add items (JSON body, CSRF header)
- `POST /Order/CancelItem` – cancel a single order item (form URL encoded)
- `GET /Payment/ProcessPayment?orderId={id}` – pay now (existing screen)

Optional (already present in controller, used by UI refresh):
- `GET /Order/GetPOSOrderJson?orderId={id}` – returns current items + totals

## Constraints (Server Enforced)
- Only **OrderType 1 (Takeout)** and **2 (Delivery)** are supported.
- Delivery requires address.

## Backend Flow (Unchanged)
- Order creation uses `usp_CreateOrder`.
- Item add/edit uses existing controller action `UpdateMultipleOrderItems`, which recalculates:
  - per-item GST persistence (`UpdateOrderItemGstDetails`)
  - order totals (`UpdateOrderFinancials`)
- Cancellation uses existing `CancelItem` action.
- Payment uses existing `PaymentController` screens.

## Files
- Controller: RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs
  - `POSOrder`, `CreatePOSOrder`, `GetPOSOrderJson`
- View: RestaurantManagementSystem/RestaurantManagementSystem/Views/Order/POSOrder.cshtml

## Navigation
To show **POS Order** under the existing **Orders** dropdown, run:
- RestaurantManagementSystem/RestaurantManagementSystem/SQL/add_pos_order_navigation.sql

This creates `NAV_ORDERS_POS` and copies role permissions from `NAV_ORDERS_CREATE` so the same roles can see it.

## Notes
- Menu item dropdown is server-rendered from `MenuItems` (schema-safe if `IsAvailable` column is missing).
- No navigation link is added; access the page directly by URL.
