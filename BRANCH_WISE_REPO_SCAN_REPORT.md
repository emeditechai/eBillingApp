# Branch-wise Implementation (Repo + DB Scan Report)

This document is a repo-specific assessment for adding a **Restaurant Branch** concept across login → all pages → all transactions, with **admin consolidated (all-branches) reporting**.

It is based on scanning this workspace (C# controllers/views + SQL scripts). It does **not** implement anything; it’s a step-by-step workaround / rollout plan.

---

## 1) What the scan shows (current state)

**Repo size (quick stats)**
- C# files: ~197
- Controllers: ~49
- Razor views: ~166
- SQL scripts: ~153

**Where data access lives**
- A lot of SQL is written inline inside controllers (not a centralized repository layer).
- Stored procedures are also used (at least ~91 call sites found), so branch filtering must be done in both:
  - controller inline SQL
  - stored procedures / views

**High-traffic transactional areas** (most likely to need strict branch filtering)
- Orders / POS: `OrderController`, `OrderController_NewItems`, and related SQL procs.
- Payments / split bills / settlement: `PaymentController` + `Payments_Setup.sql`.
- Bar (BOT module): `BOTController` + `BOT_Setup.sql`.
- Kitchen tickets: `KitchenController` + `Kitchen_Setup.sql`.
- Reports: `ReportsController` + multiple report stored procedures.
- Table service & reservations: `TableServiceController`, `ReservationController` + setup scripts.

**Important discovery: “BranchID” already exists but for Hotel Room Service**
This repo already uses a *hotel* branch concept:
- `Orders.H_BranchID` (and other hotel-related columns like booking/room)
- View: `vw_RoomService_PendingSettlement` maps `o.H_BranchID AS BranchID`
- Procedure: `usp_GetRoomServicePendingSettlementDetails(@BranchID)` filters on `o.H_BranchID = @BranchID`
- UI: `Views/Order/Create.cshtml` has `hotelBranch` dropdown + `/Order/GetCheckedInOccupiedRooms?branchId=...`

**Implication:**
- Do **not** reuse `H_BranchID` for restaurant branches.
- Introduce a **separate restaurant branch key**, e.g. `Orders.BranchId` (recommended), and keep hotel integration intact.

---

## 2) Design decision you must confirm (before coding)

You need to decide these 4 items up-front, because they change both schema and UI behavior:

1) **Branch scope model**
   - **Strict branch isolation**: every transactional table row belongs to exactly one branch.
   - Admin may read all branches; normal users only their branch.

2) **Branch selection UX**
   - Option A: user is tied to a single branch (no switch UI).
   - Option B (your request): user can **switch branch** (session-based active branch).

3) **Master data (Menu/Prices) strategy**
   - Global masters shared across branches (simpler) vs.
   - Branch-specific menu/pricing (more complex but common for chains)

4) **Counter strategy**
   - `Counters` are almost always branch-specific (recommended), because POS counter belongs to a physical location.

---

## 3) Recommended branch data model (DB)

### 3.1 Core tables
Create:
- `Branches (Id, BranchCode, BranchName, IsActive, CreatedAt, UpdatedAt, ...)`
- `UserBranches (UserId, BranchId, IsDefault, IsActive, ...)`

Add:
- `Users.DefaultBranchId` (optional but practical)

### 3.2 Transactional tables that should get `BranchId`
At minimum:
- `Orders` (most important)  
- `OrderItems` (optional if always joined from Orders, but recommended for indexing / safety)
- `Payments`, `SplitBills`, `SplitBillItems`
- `TableTurnovers`, `ServerAssignments`
- `Reservations`, `Waitlist`, `Tables` (or at least tables/turnovers)
- BOT module: `BOT_Header`, `BOT_Detail`, `BOT_Bills`, `BOT_Payments`
- Kitchen module: `KitchenTickets`, `KitchenTicketItems` (and related)
- Day closing / cashier: `CashierDayOpening`, `CashierDayClose`, `DayLockAudit`
- Online ordering: `OnlineOrders`, `OnlineOrderItems` (and related)

### 3.3 Foreign keys and indexes (performance + correctness)
- Add FK: `... BranchId → Branches.Id`
- Add indexes:
  - `Orders(BranchId, CreatedAt)`
  - `Orders(BranchId, Status)`
  - `Payments(BranchId, CreatedAt)` or `Payments(OrderId)` + join filter
  - `KitchenTickets(BranchId, Status, CreatedAt)`
  - `BOT_Header(BranchId, Status, CreatedAt)`

---

## 4) Application approach (ASP.NET Core MVC)

### 4.1 Where to store the active branch
This app already uses Session for POS counter selection (`POS.SelectedCounterId`). Reuse the same pattern:
- Session keys:
  - `BRANCH.ActiveBranchId`
  - `BRANCH.ActiveBranchName` (optional)

### 4.2 How to pick the branch at login
Recommended flow:
- After successful login:
  - fetch allowed branches for user
  - if only 1 branch → set session active branch automatically
  - if multiple branches → show a “Select Branch” screen once, then store in session

### 4.3 Enforcing branch scope
Because SQL is often inline in controllers, the safest pattern is:
- Make a **single helper** that resolves active branch id (or throws)
- Every query that reads/writes transactions must include:
  - `... WHERE BranchId = @BranchId`
  - or `JOIN Orders o ... WHERE o.BranchId = @BranchId`

For Admin consolidated reporting:
- allow `@BranchId = NULL` meaning “all branches”
- BUT only if user has an `Admin`/`SuperAdmin` role/claim

---

## 5) Repo impact map (what you will need to touch)

### 5.1 Controllers that are branch-sensitive (highest priority)
These controllers were found referencing Orders/Payments and are the main risk for cross-branch leakage:
- `OrderController.cs`
- `OrderController_NewItems.cs`
- `PaymentController.cs`
- `ReportsController.cs`
- `BOTController.cs`
- `KitchenController.cs`
- `OnlineOrderController.cs`
- `ReservationController.cs`
- `TableServiceController.cs`
- `AuditTrailController.cs`

### 5.2 SQL scripts / procedures (examples)
You will need to branch-scope:
- Order creation / item add / dashboards / recent orders
- Payment info + payment processing
- BOT dashboard / listing / settlement
- Kitchen dashboards / tickets by status
- Reports procedures (sales/discount/GST/collection register/order report/etc.)
- Day closing procedures

### 5.3 Existing Hotel Room Service branch fields (do not break)
Files that currently depend on hotel branch (`H_BranchID`) and should remain separate:
- `RestaurantManagementSystem/RestaurantManagementSystem/SQL/usp_GetRoomServicePendingSettlementDetails.sql`
- `RestaurantManagementSystem/RestaurantManagementSystem/SQL/vw_RoomService_PendingSettlement.sql`
- `RestaurantManagementSystem/RestaurantManagementSystem/Views/Order/Create.cshtml`
- `RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs` (room-service flow)
- `RestaurantManagementSystem/RestaurantManagementSystem/Controllers/PaymentController.cs` (room-service settlement references)

Recommendation:
- Keep hotel-related parameter naming as `HotelBranchId` or `HBranchId` in C# models to reduce confusion.
- Introduce restaurant `BranchId` for restaurant module.

---

## 6) Step-by-step workaround / rollout plan (safe phases)

### Phase 0 — Finalize rules (1–2 days)
- Confirm master data strategy: global vs branch-specific menu/prices.
- Confirm whether order numbers are global across branches or per-branch.
- Confirm whether admin can switch branch and/or view all.

### Phase 1 — DB foundation (2–4 days)
- Create `Branches` and `UserBranches`.
- Add nullable `BranchId` columns to **top-level** transaction tables first (Orders, Payments, BOT, Kitchen, Reservations).
- Backfill `BranchId` for existing rows:
  - If you only have one branch today: set all existing rows to that branch.
  - If multiple branches already exist implicitly (by Counter or hotel branch): define mapping rules explicitly.
- Add indexes on `(BranchId, CreatedAt/Status)`.

### Phase 2 — App “active branch” plumbing (2–4 days)
- Add a branch selector after login (only when user has >1 allowed branch).
- Store active branch in session (`BRANCH.ActiveBranchId`).
- Add a small helper (single source of truth) to get active branch in controllers.

### Phase 3 — Enforce branch on write paths (3–7 days)
Do this before read paths to prevent new cross-branch records.
- Order creation: insert `Orders.BranchId = @BranchId`.
- Add/update order items: ensure `OrderId` belongs to the active branch.
- Payments: ensure payment is applied only to an order in the active branch.
- BOT/Kitchen: ensure created rows inherit branch from the order.

### Phase 4 — Enforce branch on read paths (5–10 days)
- Dashboards (food + bar + kitchen): filter by active branch.
- POS views and active order lists: filter by branch.
- Reports:
  - Normal users: filter by branch only.
  - Admin: add optional branch filter + “All branches” option.

### Phase 5 — Make BranchId NOT NULL + add FKs (1–2 days)
After app is fully writing correct BranchId:
- Make `BranchId` required on core transactional tables.
- Add strict foreign keys.

### Phase 6 — Hardening + QA (ongoing)
- Add automated checks (or at least manual test cases) for:
  - user from Branch A cannot view Branch B orders
  - payment cannot be applied across branches
  - reports totals match per-branch and all-branches

---

## 7) High-risk areas / gotchas (specific to this repo)

1) **Two branch concepts**
   - Hotel room-service uses `H_BranchID` already.
   - Restaurant branch must be separate to avoid logic conflicts.

2) **Inline SQL scattered across controllers**
   - Branch conditions are easy to miss; expect iterative hardening.

3) **Counters & POS**
   - POS uses selected counter in session.
   - Recommended: `Counters` should have `BranchId`, and selected counter must belong to active branch.

4) **Reports stored procedures**
   - Many reports are SQL procedures; each needs either:
     - mandatory `@BranchId` parameter, or
     - optional `@BranchId` with admin-only all-branch mode.

5) **Backfill rules**
   - If historical data cannot be mapped to a branch, you’ll need a “Legacy/Unknown” branch.

---

## 8) Practical next step (if you want me to continue the scan)

If you want an even more actionable deliverable, I can generate a second document that lists:
- each controller action that needs `BranchId` filter
- each stored procedure that queries `Orders/Payments` and needs a `@BranchId` input

Tell me your decision for these 2 items and I’ll tailor that list:
1) Menu data is **global** or **branch-specific**?
2) Reports: Admin needs **All branches at once** or **switch branch only**?
