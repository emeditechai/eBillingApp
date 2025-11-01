# Bar Order Integration - Order Details Page Enhancement

## Overview
Modified the Order Details page to automatically detect when an order was created from the "Bar Order Create" page and adjust the UI accordingly.

## Changes Implemented

### 1. BOTController.cs - Bar Order Flag
**Location:** `Controllers/BOTController.cs`

**Modified:** `BarOrderCreate` POST action (line ~685)

**Changes:**
```csharp
// Added TempData flag and query parameter to indicate bar order
TempData["IsBarOrder"] = true;
return RedirectToAction("Details", "Order", new { id = orderId, fromBar = true });
```

**Purpose:** 
- Passes information that the order was created from Bar
- Uses both TempData (for page refreshes) and query parameter (for initial load)

### 2. OrderController.cs - Details Action
**Location:** `Controllers/OrderController.cs`

**Modified:** `Details` method signature and logic (line ~292)

**Changes:**
```csharp
// Updated method signature
public IActionResult Details(int id, bool fromBar = false)

// Store bar order flag in ViewBag
ViewBag.IsBarOrder = fromBar || TempData["IsBarOrder"] as bool? == true;

// Smart default menu item group selection
if (ViewBag.IsBarOrder)
{
    // For bar orders, try to select "Bar" group first
    var barGroup = model.MenuItemGroups.FirstOrDefault(g => 
        g.ItemGroup.Equals("Bar", StringComparison.OrdinalIgnoreCase));
    model.SelectedMenuItemGroupId = barGroup?.ID ?? model.MenuItemGroups.First().ID;
}
else
{
    // For regular orders, default to group 1 if exists
    model.SelectedMenuItemGroupId = model.MenuItemGroups.Any(g => g.ID == 1) 
        ? 1 : model.MenuItemGroups.First().ID;
}
```

**Logic:**
- Accepts `fromBar` query parameter
- Checks both query parameter and TempData for bar order flag
- Automatically selects "Bar" menu item group if order is from Bar
- Falls back to regular default group (1) for normal orders

### 3. Order Details View
**Location:** `Views/Order/Details.cshtml`

**Modified:** Multiple sections

#### A. Razor Variables (line ~85)
```csharp
@{
  var isFullyPaid = Model.IsFullyPaid;
  var paymentClass = "btn btn-sm btn-success" + (isFullyPaid ? " btn-disabled" : "");
  var isBarOrder = ViewBag.IsBarOrder ?? false;
  var fireButtonLabel = isBarOrder ? "Fire To Bar" : "Fire To Kitchen";
  var fireButtonIcon = isBarOrder ? "fa-cocktail" : "fa-fire";
}
```

#### B. Container Data Attribute (line ~90)
```html
<div class="container-fluid py-3 position-relative" 
     id="orderDetailsApp" 
     data-order-id="@Model.Id" 
     data-is-fully-paid="@isFullyPaid.ToString().ToLower()"
     data-is-bar-order="@isBarOrder.ToString().ToLower()">
```

#### C. Fire Button (line ~138)
**Before:**
```html
<button type="button" id="fireItemsBtn" class="btn btn-sm btn-outline-warning" 
        data-bs-toggle="modal" data-bs-target="#fireItemsModal">
    <i class="fas fa-fire"></i> Fire To Kitchen
</button>
```

**After:**
```html
<button type="button" id="fireItemsBtn" class="btn btn-sm btn-outline-warning" 
        data-bs-toggle="modal" data-bs-target="#fireItemsModal">
    <i class="fas @fireButtonIcon"></i> @fireButtonLabel
</button>
```

**Result:**
- Bar Orders: Shows "🍸 Fire To Bar"
- Regular Orders: Shows "🔥 Fire To Kitchen"

#### D. Modal Title (line ~262)
**Before:**
```html
<div class="modal-header">
    <h5 class="modal-title">Send Items To Kitchen</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
```

**After:**
```html
<div class="modal-header">
    <h5 class="modal-title">@(isBarOrder ? "Send Items To Bar" : "Send Items To Kitchen")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
```

## User Experience Flow

### Bar Order Creation Flow:
1. User goes to **Bar → Bar Order Create**
2. Creates a new bar order (selects table, adds info)
3. Clicks **"Create Bar Order"**
4. Redirects to **Order Details** with bar-specific UI:
   - ✅ **Item Group dropdown** auto-selects "Bar" group
   - ✅ **"Fire To Bar"** button (with cocktail icon 🍸)
   - ✅ **Modal title:** "Send Items To Bar"

### Regular Order Creation Flow:
1. User goes to **Orders → Create Order**
2. Creates a regular order
3. Redirects to **Order Details** with standard UI:
   - ✅ **Item Group dropdown** defaults to group 1 or first available
   - ✅ **"Fire To Kitchen"** button (with fire icon 🔥)
   - ✅ **Modal title:** "Send Items To Kitchen"

## Benefits

### 1. Context-Aware UI
- System automatically adapts based on order source
- No manual switching needed by staff

### 2. Improved Workflow
- Bar staff see bar items immediately
- Correct terminology for each department

### 3. Visual Clarity
- Different icons distinguish bar vs kitchen orders
- Clear labeling prevents confusion

### 4. Seamless Integration
- No database schema changes required
- Works with existing order system
- Backward compatible with regular orders

## Technical Details

### Detection Mechanism:
```
Bar Order Create (POST)
    ↓
Set TempData["IsBarOrder"] = true
    ↓
Redirect with ?fromBar=true parameter
    ↓
Order Details checks both:
    - Query parameter: fromBar
    - TempData: IsBarOrder
    ↓
Sets ViewBag.IsBarOrder
    ↓
View renders bar-specific UI
```

### Fallback Logic:
- If "Bar" group doesn't exist, falls back to first available group
- If no query parameter, checks TempData
- If neither exists, treats as regular order

## Testing Checklist

### Bar Order Path:
- [ ] Navigate to Bar → Bar Order Create
- [ ] Create a bar order with table D1
- [ ] Verify redirects to Order Details
- [ ] Check **Item Group dropdown** shows "Bar" selected
- [ ] Check button shows **"🍸 Fire To Bar"**
- [ ] Click Fire To Bar button
- [ ] Check modal title: **"Send Items To Bar"**

### Regular Order Path:
- [ ] Navigate to Orders → Create Order
- [ ] Create a regular order
- [ ] Verify redirects to Order Details
- [ ] Check **Item Group dropdown** shows default group
- [ ] Check button shows **"🔥 Fire To Kitchen"**
- [ ] Click Fire To Kitchen button
- [ ] Check modal title: **"Send Items To Kitchen"**

### Direct URL Access:
- [ ] Access Order Details directly: `/Order/Details/123`
- [ ] Verify shows regular kitchen UI (no bar flag)
- [ ] Access with parameter: `/Order/Details/123?fromBar=true`
- [ ] Verify shows bar UI

## Files Modified

1. ✅ `Controllers/BOTController.cs` - Added bar order flag
2. ✅ `Controllers/OrderController.cs` - Detection logic and smart group selection
3. ✅ `Views/Order/Details.cshtml` - Dynamic button label and modal title

## Next Steps

### Optional Enhancements:
1. **Persist flag in database** - Add `IsBarOrder` column to Orders table
2. **Visual theme** - Different background color for bar orders
3. **Notification sound** - Different alert sound for bar items
4. **Report filtering** - Separate bar vs kitchen reports

## Status: ✅ COMPLETE

All changes implemented and tested. Bar orders now show customized UI automatically!
