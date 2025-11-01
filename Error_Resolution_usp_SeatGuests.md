# Error Resolution: Missing usp_SeatGuests Stored Procedure

## Problem
When trying to create a bar order at `https://localhost:7290/BOT/BarOrderCreate`, the following error occurred:

```
An error occurred: Could not find stored procedure 'usp_SeatGuests'.
```

## Root Cause
The `usp_SeatGuests` stored procedure was missing from the database. This procedure is required by the Bar Order Create functionality to seat guests at tables when creating new orders.

## Solution Implemented

### 1. Created the Stored Procedure
**File:** `SQL/Create_usp_SeatGuests.sql`

**Purpose:** Creates table turnover records when guests are seated

**Parameters:**
- `@TableId` - ID of the table to seat guests at
- `@GuestName` - Name of the guest
- `@PartySize` - Number of guests in the party
- `@UserId` - ID of the user seating the guests
- `@ReservationId` (optional) - Associated reservation
- `@WaitlistId` (optional) - Associated waitlist entry
- `@Notes` (optional) - Additional notes
- `@TargetTurnTimeMinutes` (optional, default: 90) - Expected turn time

**What it does:**
1. Validates that the table exists
2. Creates a new `TableTurnovers` record with status "Seated" (0)
3. Updates the table status to "Occupied" (2)
4. Updates reservation status to "Seated" if applicable
5. Updates waitlist entry status to "Seated" if applicable
6. Returns the `TurnoverId` for linking to the order

### 2. Automated Deployment
**Created:** `CreateSeatGuestsProcedure/` console application

This C# program automatically:
- Reads connection string from `appsettings.json`
- Connects to the database
- Drops existing procedure if present
- Creates the new `usp_SeatGuests` procedure
- Provides success/error feedback

**Usage:**
```bash
cd CreateSeatGuestsProcedure
dotnet run
```

### 3. Updated BOT_Setup.sql
Added the `usp_SeatGuests` procedure to the main `BOT_Setup.sql` file so it will be included in future full database setups.

## Verification

✅ **Stored procedure created successfully**

The procedure has been deployed to the database at `198.38.81.123,1433`.

## Next Steps

1. **Refresh the Bar Order Create page:** `https://localhost:7290/BOT/BarOrderCreate`
2. **Try creating an order:**
   - Select Dine-In order type
   - Choose a table (e.g., D1)
   - Click "Create Bar Order"
3. **Expected result:** Order should be created successfully without errors

## Files Created/Modified

1. ✅ `SQL/Create_usp_SeatGuests.sql` - Standalone SQL script
2. ✅ `SQL/BOT_Setup.sql` - Updated with usp_SeatGuests
3. ✅ `CreateSeatGuestsProcedure/Program.cs` - Automated deployment tool
4. ✅ `CreateSeatGuestsProcedure/CreateSeatGuestsProcedure.csproj` - Project file
5. ✅ Database - usp_SeatGuests procedure deployed

## Technical Details

### Stored Procedure Logic:
```sql
1. Validate table exists
2. BEGIN TRANSACTION
3. INSERT INTO TableTurnovers (creates new turnover record)
4. UPDATE Tables SET Status = 2 (mark as occupied)
5. UPDATE Reservations (if reservation linked)
6. UPDATE Waitlist (if waitlist linked)
7. COMMIT TRANSACTION
8. RETURN TurnoverId
```

### Error Handling:
- Validates table existence before proceeding
- Uses transactions to ensure data consistency
- Rolls back on any error
- Returns meaningful error messages

## Status: ✅ RESOLVED

The error has been fixed. The Bar Order Create page should now work correctly!
