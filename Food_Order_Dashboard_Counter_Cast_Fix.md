# Food Order Dashboard – Counter column/filter crash fix

## Issue
Loading `/Order/Dashboard` could throw:

- `InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Int32'`

This occurred after adding a `CounterId` column to the Active Orders SQL query.

## Root cause
The Active Orders reader code was still using hard-coded numeric column indexes (e.g., `GetInt32(10)`), but the SQL column order changed when `CounterId` was inserted.

As a result, some `GetInt32(...)` calls ended up reading the wrong column (e.g., `ServerName`), causing a string → int cast failure.

Additionally, the Cancelled Orders query did not select `CounterId`, but the reader code attempted to read it.

## Fix
- Updated Active Orders mapping to use named ordinals via `reader.GetOrdinal("...")` (e.g., `Id`, `CounterId`, `ServerName`, `ItemCount`, etc.) instead of fragile numeric indexes.
- Added `CounterId` to the Cancelled Orders SQL select (schema-safe using `TRY_CONVERT(int, ...)`) and updated its mapping to use ordinals as well.

File changed:
- `RestaurantManagementSystem/RestaurantManagementSystem/Controllers/OrderController.cs`

## Verification
1. Run the app and open `/Order/Dashboard`.
2. Confirm all three sections load without an exception.
3. Confirm the Counter dropdown filters Active/Completed/Cancelled client-side.
4. Confirm orders with no counter show `-` and remain visible when "All Counters" is selected.
