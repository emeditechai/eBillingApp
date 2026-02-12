# Branch-wise Implementation — Exact Touch List (Menu/Pricing Branch-wise + Admin All-Branches Reporting)

Decisions confirmed (12 Feb 2026):
- **Menu/Pricing is branch-wise**
- **Admin reporting shows all branches at once** (consolidated)

This is a repo-specific “what to change” list (controllers + SQL procedures) based on scanning this workspace.

---

## A) DB changes (must-do)

### A1) New tables
1) `dbo.Branches`
   - `Id INT IDENTITY PK`
   - `BranchCode NVARCHAR(20) UNIQUE`
   - `BranchName NVARCHAR(120)`
   - `IsActive BIT`, `CreatedAt`, `UpdatedAt`

2) `dbo.UserBranches`
   - `UserId INT FK -> Users.Id`
   - `BranchId INT FK -> Branches.Id`
   - `IsDefault BIT`, `IsActive BIT`, timestamps
   - Unique constraint `(UserId, BranchId)`

### A2) BranchId columns (recommended minimum)

**Restaurant transactional**
- `Orders.BranchId` (do NOT reuse `H_BranchID`)
- `Payments.BranchId` (or enforce via join from Orders; still recommended)
- `OrderItems.BranchId` (recommended for performance & safety)
- `SplitBills.BranchId`, `SplitBillItems.BranchId`
- `TableTurnovers.BranchId`, `ServerAssignments.BranchId`
- `Reservations.BranchId`, `Waitlist.BranchId`, `Tables.BranchId`
- Kitchen: `KitchenTickets.BranchId`, `KitchenTicketItems.BranchId`
- Bar/BOT: `BOT_Header.BranchId`, `BOT_Detail.BranchId`, `BOT_Bills.BranchId`, `BOT_Payments.BranchId`
- Day closing: `CashierDayOpening.BranchId`, `CashierDayClose.BranchId`, `DayLockAudit.BranchId`
- Online ordering: `OnlineOrders.BranchId`, `OnlineOrderItems.BranchId`, `WebhookEvents.BranchId` (if needed)

**Menu + Pricing (branch-wise requirement)**
At least one of these models must be chosen. Recommended is “branch-scoped menu rows”.
- `Categories.BranchId`
- `SubCategories.BranchId`
- `MenuItems.BranchId`
- `Modifiers.BranchId`
- Any mapping tables used by menu/kitchen (e.g. `MenuItemModifiers`, `MenuItemKitchenStations`) should either:
  - inherit from `MenuItems`, or
  - store `BranchId` for faster filtering.

### A3) Indexes (performance)
- `Orders(BranchId, CreatedAt DESC)`
- `Orders(BranchId, Status, CreatedAt DESC)`
- `MenuItems(BranchId, IsAvailable, CategoryId)`
- `Payments(BranchId, CreatedAt DESC)`
- `KitchenTickets(BranchId, Status, CreatedAt DESC)`
- `BOT_Header(BranchId, Status, CreatedAt DESC)`

### A4) Important: existing Hotel Room-Service branch stays separate
This repo already uses **hotel** branch fields (`Orders.H_BranchID`) for room-service settlement.
- Keep those flows unchanged.
- Restaurant branch must be `Orders.BranchId` (new).

---

## B) App plumbing (must-do)

### B1) Session keys
Add active branch selection (similar to existing POS counter session approach):
- `BRANCH.ActiveBranchId`
- `BRANCH.ActiveBranchName` (optional)

### B2) Login / switch branch
Implement:
- After login: load allowed branches for user.
- If 1 branch → set it.
- If many → force select branch (once) then proceed.
- Provide a “Switch Branch” option (clears cached lists where needed).

### B3) Enforcement rule
For non-admin users:
- Every read/write must be filtered by `BranchId = ActiveBranchId`.

For admin:
- Most pages still work in single active branch mode.
- Reports allow **All branches** consolidated:
  - pass `@BranchId = NULL` (or `0`) to report procedures.
  - only if admin role/permission.

---

## C) Controllers to modify (repo scan result)

These controllers were identified as touching Orders/Payments and will need branch enforcement.

### C1) Orders & POS
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs
Actions seen in scan (non-exhaustive):
- `Dashboard`, `Create`, `Details`, `GetOrderDashboard`
- POS: `POSOrder`, `SetPOSCounter`, `CreatePOSOrder`, `CreatePOSOrderAjax`, `UpdateMultipleOrderItems`
- Items: `AddItem`, `QuickAddMenuItem`, `FireItems`, `SubmitOrder`
- Hotel-only: `GetHotelBranches`, `GetCheckedInOccupiedRooms` (keep using `H_BranchID` path)

What to change:
- On create order: set `Orders.BranchId = ActiveBranchId` (restaurant orders)
- On add/update items: validate the order belongs to branch
- On listing dashboards: filter by branch
- Keep room-service (OrderType=4) flow separate from restaurant branches.

**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController_NewItems.cs
- Same branch rules as above for any order-item write APIs.

### C2) Payments
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/PaymentController.cs
Actions seen:
- `Index`, `ProcessPayment`, `ProcessSplitPayments`
- `VoidPayment`, approvals (`ApprovePayment`, `RejectPayment`, `ApprovePaymentAjax`, `RejectPaymentAjax`)
- Dashboards: `Dashboard`, `BarDashboard`
- Print flows: `Print`, `PrintBill`, `PrintPOS`

What to change:
- Any payment operation must confirm the `OrderId` belongs to active branch.
- Payment listing/dashboard queries must filter by branch.
- If you add `Payments.BranchId`, always populate it from the order’s branch.

### C3) Bar / BOT
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/BOTController.cs
Actions seen:
- `Dashboard`, `Tickets`, `Details`, `UpdateStatus`, `Void`
- `BarOrderCreate`, `BarOrderDashboard`

What to change:
- Ensure BOT header/detail rows are created with `BranchId` (inherit from Orders).
- Filter BOT dashboards and reports by branch.

### C4) Kitchen
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/KitchenController.cs
Actions seen:
- `Dashboard`, `Tickets`, `TicketDetails`
- status updates + station management (`Stations`, `SaveStation`, etc.)

What to change:
- Filter kitchen ticket lists/dashboard by branch.
- Station management: decide whether kitchen stations are branch-wise (almost always yes).

### C5) Reports (Admin all-branches)
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/ReportsController.cs
Actions seen:
- `Sales`, `Orders`, `Menu`, `Customers`, `Financial`
- `Kitchen`, `Bar`
- `DiscountReport`, `GSTBreakup`, `CollectionRegister`, `CashClosing`, `FeedbackSurveyReport`

What to change:
- Add branch selector in report UI:
  - Normal users: fixed to active branch (no “All”).
  - Admin: allow `All branches`.
- Pass `@BranchId` into stored procedures:
  - `@BranchId = ActiveBranchId` normally
  - `@BranchId = NULL` for admin consolidated

### C6) Tables/Reservations
**Files:**
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/ReservationController.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/TableServiceController.cs

What to change:
- Tables/Reservations/Waitlist/Turnovers must be branch-scoped.
- All lists and lookups filtered by branch.

### C7) Online ordering
**File:** RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OnlineOrderController.cs
Actions seen:
- `Dashboard`, `Index`, `Details`, `SyncOrder`, webhook receiver

What to change:
- Decide whether an online source is branch-wise. Usually: **yes**.
- Store `BranchId` on online orders and enforce during sync.

### C8) Menu + Pricing (branch-wise requirement)
**Files:**
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/MenuManagementController.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/MenuController.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/CategoryController.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/SubCategoryController.cs

What to change:
- Every list/create/edit/delete must use active branch.
- If Admin needs to manage multiple branches:
  - either switch branch while managing menu, or
  - provide branch dropdown on menu pages.

---

## D) Stored procedures to update (from SQL scan)

### D1) Orders
- `dbo.usp_CreateOrder` → add `@BranchId` and insert to `Orders.BranchId`
- `dbo.usp_AddOrderItem` → validate order branch; optionally set `OrderItems.BranchId`
- `dbo.usp_FireOrderItems` → filter/validate by branch
- `usp_GetRecentOrdersForDashboard` / dashboards → filter by branch

### D2) Payments
- `dbo.usp_GetOrderPaymentInfo` → enforce that requested order belongs to branch
- `dbo.usp_ProcessPayment` → enforce/derive branch
- `dbo.usp_CreateSplitBill` → enforce/derive branch
- `dbo.usp_VoidPayment` → enforce/derive branch

### D3) BOT
- `dbo.GetBOTDashboardStats`, `dbo.GetBOTsByStatus`, `dbo.GetBOTDetails` → filter by branch
- `dbo.GetNextBOTNumber` → decide if sequence is per-branch (recommended)
- `dbo.UpdateBOTStatus`, `dbo.VoidBOT` → validate branch
- `dbo.usp_GetBarBOTReport` → add `@BranchId` (nullable for admin) and filter.

### D4) Kitchen
- `GetKitchenDashboardStats`, `GetKitchenTicketsByStatus`, `GetFilteredKitchenTickets`, `GetKitchenTicketDetails` → add `@BranchId`
- `UpdateKitchenTicketsForOrder` / `dbo.UpdateKitchenTicketsForOrder` → validate branch
- `MarkAllTicketsReady` → branch filter

### D5) Reports (admin consolidated)
Add `@BranchId INT = NULL` to each report SP and use pattern:
- `WHERE (@BranchId IS NULL OR o.BranchId = @BranchId)`

Procedures found in scan:
- `usp_GetSalesReport`
- `usp_GetOrderReport`
- `usp_GetDiscountReport`
- `dbo.usp_GetGSTBreakupReport`
- `dbo.usp_GetCollectionRegister`
- `usp_GetFinancialSummary`
- `usp_GetCashClosingReport`
- `usp_GetHomeDashboardStats` / `usp_GetHomeDashboardStatsEnhanced` (if totals should be branch-wise)
- `dbo.usp_GetCustomerAnalysis`
- `dbo.usp_GetMenuAnalysis`

### D6) Menu + Pricing (branch-wise)
Procedures found in scan:
- `dbo.sp_GetAllMenuItems`
- `dbo.sp_GetMenuItemById`
- `dbo.sp_CreateMenuItem`
- `dbo.sp_UpdateMenuItem`
- `dbo.sp_PublishMenuItem`
- `dbo.sp_GetAllModifiers`
- `dbo.sp_ApprovePriceChange`
- plus recipe-related SPs (`dbo.sp_ManageRecipe`, etc.)

What to change:
- Add `@BranchId` to all menu/proc read/write.
- Ensure any uniqueness constraints consider branch (e.g., item codes/names).

### D7) Tables/Reservations
- `dbo.usp_SeatGuests`, `dbo.usp_AssignServerToTable`, `dbo.usp_StartTableTurnover`, `dbo.usp_UpdateTableTurnoverStatus`
- `dbo.usp_UpsertReservation`, `dbo.usp_UpsertWaitlist`, `dbo.usp_UpsertTable`

Add branch enforcement and prevent cross-branch table assignment.

### D8) Hotel room-service (leave as-is)
- `dbo.usp_GetRoomServicePendingSettlementDetails`
- `dbo.vw_RoomService_PendingSettlement`

These are hotel-specific and depend on `Orders.H_BranchID`.

---

## E) Recommended implementation order (workable sequence)
1) DB: create `Branches`, `UserBranches`.
2) Add `BranchId` to menu tables first (because menu/pricing is branch-wise).
3) Add `BranchId` to `Orders` and enforce on create/add items.
4) Add branch enforcement to payments.
5) Add branch enforcement to BOT + Kitchen.
6) Reports: add `@BranchId` param (nullable) and enable admin consolidated.
7) Backfill existing rows to a default branch.
8) Make `BranchId` NOT NULL + FK constraints.

---

## F) Next optional deliverable
If you want, I can generate a ready-to-run SQL migration skeleton that:
- creates `Branches`, `UserBranches`
- adds `BranchId` columns
- adds indexes
- shows safe backfill template
