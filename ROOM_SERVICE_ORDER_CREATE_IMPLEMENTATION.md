# Room Service (Hotel) – Create Order Enhancement

Date: 2025-12-21

## Summary
Adds a new Order Type **Room Service** on the Food Order Create page (Orders → Create) without affecting existing Dine-In/Takeout/Delivery or Online flows.

Because `OrderType = 3` is already used as **Online** in this codebase, Room Service is implemented as:

- **Room Service = 4**

## UI/UX (Create Order)
File: RestaurantManagementSystem/RestaurantManagementSystem/Views/Order/Create.cshtml

When Order Type = **Room Service (4)**, a new section becomes visible:

- **Hotel Branch** (dropdown)
- **Room No** (dropdown)
- **Booking No** (textbox)

Behavior:
1. On selecting **Room Service**, branches are loaded from `usp_GetBranchfromHotel`.
2. After selecting a **Hotel Branch**, rooms are loaded from `sp_GetCheckedInOccupiedRooms @BranchID`.
3. Selecting a **Room No** auto-populates:
   - Booking No (disabled)
   - Customer Name / Phone (and Email if empty)
4. Alternatively, user can enter **Booking No** and blur the field to auto-select the matching room and populate guest details.

Note:
- Booking No is set to **readonly** (not disabled) so it is still posted and saved into `Orders.HBookingNo`.
- Room No dropdown labels are shown as `RoomNo - GuestName` for quick identification.

Client validation:
- Room Service requires Branch.
- Requires either Room No or Booking No.

## Backend endpoints
File: RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs

New JSON endpoints:
- `GET /Order/GetHotelBranches`
  - Executes `usp_GetBranchfromHotel`
  - Returns `{ success, data: [{ branchId, branch }] }`

- `GET /Order/GetCheckedInOccupiedRooms?branchId=...`
  - Executes `sp_GetCheckedInOccupiedRooms @BranchID`
  - Returns `{ success, data: [{ bookingId, bookingNo, branchId, roomId, roomNo, guestName, guestPhone, guestEmailId, plannedCheckoutDate }] }`

## Persisting to Orders
File: RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs

On Create (POST), when `OrderType == 4`:
- Validates `HBranchId` and (RoomId or BookingNo).
- If BookingNo provided but IDs are missing, it attempts to resolve room/booking by calling `sp_GetCheckedInOccupiedRooms` for the selected branch.
- Persists the following columns **if they exist** (safe column checks):
  - `H_BranchID`  ← `HBranchId`
  - `RoomID`      ← `RoomId`
  - `HBookingID`  ← `HBookingId`
  - `HBookingNo`  ← `HBookingNo`

Guest info is stored into existing columns via the normal `usp_CreateOrder` parameters:
- `CustomerName`
- `CustomerPhone`
- `CustomerEmailId`

## Model changes
File: RestaurantManagementSystem/RestaurantManagementSystem/Models/OrderViewModels.cs

`CreateOrderViewModel` adds:
- `int? HBranchId`
- `int? RoomId`
- `int? HBookingId`
- `string HBookingNo`

## OrderType display mappings updated
Room Service (4) was added to display switches in:
- RestaurantManagementSystem/RestaurantManagementSystem/Models/OrderModels.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Models/KitchenModels.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/PaymentController.cs
- RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs (summary/dashboard)

Also updated menu pricing selection so Room Service uses DeliveryPrice where applicable.

## Notes / Next
- If you later share a dedicated SP to resolve booking/room by booking no, we can switch to that for faster lookups.
- If you want Booking No to auto-match *while typing* (instead of on blur), we can change the event to `input` with a small debounce.
