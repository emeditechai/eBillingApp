# BOT (Beverage Order Ticket) - Quick Start Guide

## What is BOT?
BOT is the **Beverage Order Ticket** system that manages bar/beverage orders separately from food orders (KOT). This allows the bar to operate independently with its own workflow, tracking, and billing.

## 🎯 Key Differences: BOT vs KOT

| Feature | BOT (Beverages) | KOT (Food) |
|---------|----------------|------------|
| Ticket Format | BOT-YYYYMM-#### | KOT-YYYYMMDD-#### |
| Dashboard | Bar Dashboard | Kitchen Dashboard |
| Item Type | Drinks, Cocktails, Alcohol | Food items |
| Tax Type | Excise/VAT (alcohol) or GST | GST only |
| Workflow | New → In Progress → Ready → Billed | New → In Progress → Ready → Served |

## 📱 How to Access

1. **Main Navigation** → Click "Bar" menu
2. Select "BOT Dashboard"
3. View all beverage orders organized by status

## 🍹 Creating a Beverage Order

### Step 1: Create Order (Same as Before)
1. Go to **Orders** → **New Order**
2. Select table and guest details
3. Add items to order (food and/or beverages)

### Step 2: Fire Items
1. Click **"Fire Items"** button
2. System automatically:
   - **Bar items** → Create BOT (sent to Bar Dashboard)
   - **Food items** → Create KOT (sent to Kitchen Dashboard)

### Result
- You'll see success message: *"Items fired successfully! Beverage BOT #BOT-202511-0001 and Food KOT #KOT-20251101-0001 created."*
- Bar items now visible in **Bar Dashboard**
- Food items visible in **Kitchen Dashboard**

## 📊 BOT Dashboard

### Statistics Cards
- **New Orders** - Newly created BOTs waiting to start
- **In Progress** - Currently being prepared by bartender
- **Ready** - Completed and ready to serve
- **Billed Today** - BOTs that have been billed
- **Total Active** - All active BOTs (New + In Progress + Ready)
- **Avg Prep Time** - Average preparation time in minutes

### Status Filters
Click buttons to filter BOTs:
- **All** - Show all BOTs
- **New** - Show only new orders
- **In Progress** - Show orders being prepared
- **Ready** - Show completed orders
- **Billed** - Show paid orders

### BOT Cards
Each BOT card shows:
- BOT Number (e.g., BOT-202511-0001)
- Order Number
- Table Name
- Guest Name
- Server Name
- Total Amount
- Time created
- Number of items

### Quick Actions
- **View** - See detailed BOT information
- **Print** - Print BOT for bar staff
- **Next** - Move to next status (only for New/In Progress)

## 📋 BOT Details Page

### Information Sections

**BOT Information**
- BOT Number
- Current Status
- Order details
- Guest and server info
- Timestamps (Created, Served, Billed)

**Amount Details**
- Subtotal
- Tax Amount
- Total Amount

**Status Actions**
- **Start Preparation** - When status is New
- **Mark as Ready** - When status is In Progress
- **Mark as Billed** - When status is Ready
- **Void BOT** - Cancel order (requires reason)

**Items List**
Shows all beverage items with:
- Item name
- Quantity
- Price per unit
- Total amount
- Tax details
- Alcohol indicator (if alcoholic)
- Special instructions

## 🖨️ Printing BOT

### How to Print
1. Click **Print** button on BOT card or details page
2. Printer-friendly window opens
3. Print automatically triggers
4. Close window after printing

### Print Format (80mm Thermal Printer)
```
🍹 BEVERAGE ORDER TICKET
BOT-202511-0001
─────────────────────────────
Order: ORD-001
Table: Table 5
Guest: John Doe
Server: Mike Wilson
Time: 01 Nov 2025 14:30
Status: New
─────────────────────────────
Item                  Qty  Price
Beer                   2   ₹300.00
Cocktail [ALC]         1   ₹450.00
→ Extra ice
Soft Drink             1   ₹100.00
─────────────────────────────
Subtotal:              ₹850.00
Tax:                   ₹153.00
TOTAL:                 ₹1,003.00
─────────────────────────────
Printed: 01 Nov 2025 14:31:05
BAR COPY
─────────────────────────────
Thank you!
```

## 🔄 BOT Workflow

### Status Progression

1. **New/Open** 🆕
   - BOT just created
   - Waiting for bartender to start
   - **Action**: Click "Start Preparation"

2. **In Progress** ⏳
   - Bartender is preparing beverages
   - Items being mixed/poured
   - **Action**: Click "Mark as Ready"

3. **Ready/Served** ✅
   - Beverages completed
   - Ready for service to guest
   - **Action**: Click "Mark as Billed" (after payment)

4. **Billed/Closed** 💰
   - Payment received
   - BOT completed
   - **No further action needed**

5. **Void** ❌
   - BOT cancelled
   - Requires reason
   - **Cannot be reversed**

## 🚨 Voiding a BOT

### When to Void
- Customer changed mind
- Wrong items ordered
- Table cancelled
- Item not available

### How to Void
1. Open BOT Details
2. Click **"Void BOT"** button
3. Enter reason (required)
4. Confirm void action
5. BOT marked as void with timestamp and reason

### Important Notes
- Voided BOTs cannot be un-voided
- Reason is mandatory
- Audit trail logs who voided and why
- Voided BOTs still visible in reports

## 🍺 Alcohol Indicators

### Alcoholic Items
Items marked as alcoholic show:
- **[ALC]** or **Alcoholic** badge
- Subject to Excise duty and VAT
- May require age verification
- Special handling in reports

### Non-Alcoholic Items
- No special badge
- Subject to standard GST
- Soft drinks, juices, mocktails

## 💡 Tips & Best Practices

### For Servers
1. **Always verify** bar items are routed to BOT, not KOT
2. **Print BOT** immediately after firing to bar
3. **Check alcohol badges** for age verification
4. **Update status** promptly as items are prepared
5. **Note special instructions** clearly (ice, no sugar, etc.)

### For Bartenders
1. **Monitor Bar Dashboard** for new BOTs
2. **Start preparation** as soon as BOT arrives
3. **Mark ready** when all items complete
4. **Keep timing efficient** (avg prep time is tracked)
5. **Check special instructions** before preparing

### For Managers
1. **Review dashboard stats** regularly
2. **Monitor average prep times**
3. **Check void reasons** for patterns
4. **Verify alcohol items** properly marked
5. **Audit bills** for tax correctness

## 🔧 Troubleshooting

### BOT Not Created
**Problem**: Fired items but no BOT appeared  
**Solution**: 
- Verify items are assigned to "Bar" group
- Check if Bar group exists in system
- Ensure menuitemgroupID is set correctly

### Wrong Items in BOT
**Problem**: Food items appearing in BOT  
**Solution**:
- Check item's menu group assignment
- Should be "Bar" group for beverages only
- Contact admin to fix item configuration

### Can't Update Status
**Problem**: Status buttons not working  
**Solution**:
- Refresh page
- Check if already at final status (Billed/Void)
- Verify user permissions

### Print Not Working
**Problem**: Print window doesn't open  
**Solution**:
- Allow pop-ups in browser
- Check printer connection
- Try different browser
- Use Print button again

## 📞 Quick Reference

### Navigation Path
Main Menu → **Bar** → **BOT Dashboard**

### Common Shortcuts
- **Ctrl+P** - Print (when in print view)
- **F5** - Refresh dashboard
- **Esc** - Close modal dialogs

### Color Codes
- 🔵 **Blue (Info)** - New orders
- 🟡 **Yellow (Warning)** - In Progress
- 🟢 **Green (Success)** - Ready
- ⚫ **Gray (Secondary)** - Billed
- 🔴 **Red (Danger)** - Void

### Status Codes
- **0** = New/Open
- **1** = In Progress
- **2** = Ready/Served
- **3** = Billed/Closed
- **4** = Void

## 📚 Additional Resources

- Full implementation details: See `BOT_Implementation_Summary.md`
- Database setup: Execute `SQL/BOT_Setup.sql`
- Verification: Run `SQL/BOT_Setup_Verification.sql`
- Support: Contact system administrator

---

**Last Updated:** November 2025  
**Version:** 1.0  
**Module:** BAR/BOT (Beverage Order Ticket System)
