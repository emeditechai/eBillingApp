# Kitchen ↔ Waiter Item Comments

## Overview
Adds a light-weight communication loop between kitchen ticket screens and the order details page. Kitchen users can leave per-item comments after a ticket is fired; wait staff can read the conversation directly inside Order > Details without disrupting existing flows.

## Database
Run `create_kitchen_item_comments_table.sql` to provision `dbo.KitchenItemComments` (safe no-op if it already exists).

Schema columns:
- `OrderId`, `OrderItemId`, `KitchenTicketId`, `KitchenTicketItemId`
- `CommentText` (NVARCHAR(1000))
- `CreatedByUserId`, `CreatedByName`, `CreatedAt`

## Application Changes
- **Models**: Added `KitchenItemComment` plus comment collections on `OrderItemViewModel` and `KitchenTicketItemViewModel`.
- **Controllers**:
  - `KitchenController` loads/saves comments and exposes `AddKitchenItemComment` (POST).
  - `OrderController` pulls all comments for an order so FOH sees synced history.
- **Views**:
  - `Kitchen/TicketDetails`: shows existing comments per ticket item with an inline form to post new ones.
  - `Order/Details`: renders recent kitchen comments under each item so waiters have context at a glance.

## Usage
1. Fire items as usual.
2. On the kitchen ticket, use the new "Add comment" field under any item to message the floor team.
3. Wait staff refresh (or auto-refresh) order details to view the comments inline.

No existing payment, firing, or status flows were altered.
