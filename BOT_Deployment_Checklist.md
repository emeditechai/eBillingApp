# BOT Module - Deployment Checklist

## ✅ Pre-Deployment Verification

### Code Changes ✓
- [x] Database schema SQL scripts created
- [x] Domain models implemented  
- [x] BOTController created with all actions
- [x] OrderController modified for routing logic
- [x] BOT views created (Dashboard, Details, Print)
- [x] Navigation updated with Bar menu
- [x] Build succeeds without errors

### Files Created
```
SQL/
├── BOT_Setup.sql                        ✓ Database schema & procedures
└── BOT_Setup_Verification.sql           ✓ Setup verification queries

Models/
└── BOTModels.cs                         ✓ Domain models

Controllers/
├── BOTController.cs                     ✓ New controller
└── OrderController.cs                   ✓ Modified (routing logic)

Views/
├── BOT/
│   ├── Dashboard.cshtml                 ✓ Main dashboard
│   ├── Details.cshtml                   ✓ BOT details
│   └── Print.cshtml                     ✓ Print view
└── Shared/
    └── _Layout.cshtml                   ✓ Modified (Bar menu)

Documentation/
├── BOT_Implementation_Summary.md        ✓ Technical documentation
└── BOT_Quick_Start_Guide.md            ✓ User guide
```

## 🗄️ Database Setup

### Step 1: Backup Current Database
```sql
-- Create backup before making changes
BACKUP DATABASE [YourDatabaseName] 
TO DISK = 'C:\Backups\PreBOT_Backup_20251101.bak'
WITH INIT, COMPRESSION;
```

### Step 2: Execute BOT Schema Script
```bash
# Option 1: SQL Server Management Studio
# - Open SQL/BOT_Setup.sql
# - Review the script
# - Execute against target database

# Option 2: Command Line
sqlcmd -S ServerName -d DatabaseName -i SQL/BOT_Setup.sql
```

### Step 3: Verify Installation
```bash
# Run verification script
sqlcmd -S ServerName -d DatabaseName -i SQL/BOT_Setup_Verification.sql
```

Expected output:
- All 5 tables show "EXISTS"
- All 6 stored procedures show "EXISTS"
- MenuItems.IsAlcoholic shows "EXISTS"

### Step 4: Create Bar Menu Group
```sql
-- Check if Bar group exists
SELECT * FROM menuitemgroup WHERE itemgroup = 'Bar';

-- If not exists, create it
IF NOT EXISTS (SELECT 1 FROM menuitemgroup WHERE itemgroup = 'Bar')
BEGIN
    INSERT INTO menuitemgroup (itemgroup, is_active, GST_Perc)
    VALUES ('Bar', 1, 18.00);
    PRINT 'Bar menu item group created';
END
GO

-- Get the Bar group ID
SELECT ID, itemgroup FROM menuitemgroup WHERE itemgroup = 'Bar';
```

### Step 5: Assign Bar Items to Group
```sql
-- Get Bar group ID (replace X with actual ID from Step 4)
DECLARE @BarGroupId INT = X; -- Replace X with Bar group ID

-- Update existing bar items
UPDATE MenuItems 
SET menuitemgroupID = @BarGroupId
WHERE Name IN (
    'Beer',
    'Wine',
    'Cocktail',
    'Mocktail',
    'Whiskey',
    'Vodka',
    'Rum',
    'Gin',
    'Tequila',
    'Brandy',
    'Champagne'
    -- Add more as needed
);

-- Verify assignments
SELECT Name, menuitemgroupID 
FROM MenuItems 
WHERE menuitemgroupID = @BarGroupId;
```

### Step 6: Mark Alcoholic Items
```sql
-- Get Bar group ID
DECLARE @BarGroupId INT = (SELECT ID FROM menuitemgroup WHERE itemgroup = 'Bar');

-- Mark alcoholic beverages
UPDATE MenuItems 
SET IsAlcoholic = 1
WHERE menuitemgroupID = @BarGroupId
AND Name IN (
    'Beer',
    'Wine',
    'Whiskey',
    'Vodka',
    'Rum',
    'Gin',
    'Tequila',
    'Brandy',
    'Champagne'
    -- Add more alcoholic items
);

-- Mark non-alcoholic beverages
UPDATE MenuItems 
SET IsAlcoholic = 0
WHERE menuitemgroupID = @BarGroupId
AND Name IN (
    'Mocktail',
    'Soft Drink',
    'Juice',
    'Water',
    'Coffee',
    'Tea'
    -- Add more non-alcoholic items
);

-- Verify
SELECT Name, IsAlcoholic 
FROM MenuItems 
WHERE menuitemgroupID = @BarGroupId
ORDER BY IsAlcoholic DESC, Name;
```

## 🚀 Application Deployment

### Step 1: Stop Application
```bash
# If running as service
sudo systemctl stop restaurantapp

# If running in terminal
# Press Ctrl+C to stop
```

### Step 2: Deploy New Code
```bash
# Pull latest code from repository
git pull origin main

# Or copy files manually if not using git
# Copy all changed files to deployment directory
```

### Step 3: Build Application
```bash
cd /path/to/RestaurantManagementSystem/RestaurantManagementSystem
dotnet build -c Release
```

Expected: Build succeeded with 0 errors

### Step 4: Start Application
```bash
# Production mode
dotnet run --project RestaurantManagementSystem.csproj --launch-profile Production

# Or start service
sudo systemctl start restaurantapp
```

### Step 5: Verify Application Started
```bash
# Check if running
curl http://localhost:5290/

# Or
netstat -tulpn | grep :5290
```

## 🧪 Testing Phase

### Test 1: Navigation
- [ ] Open application in browser
- [ ] Verify "Bar" menu appears in navigation (between Kitchen and Online)
- [ ] Click Bar → BOT Dashboard
- [ ] Dashboard loads without errors
- [ ] Statistics cards display (all zeros initially is OK)

### Test 2: Create Bar-Only Order
- [ ] Go to Orders → New Order
- [ ] Select table
- [ ] Add ONLY bar items (e.g., 2 Beers, 1 Cocktail)
- [ ] Click "Fire Items"
- [ ] Success message shows: "Beverage BOT #BOT-202511-0001 created"
- [ ] Go to Bar → BOT Dashboard
- [ ] Verify BOT appears with correct items
- [ ] Kitchen Dashboard has NO tickets

### Test 3: Create Food-Only Order
- [ ] Go to Orders → New Order
- [ ] Select table
- [ ] Add ONLY food items (e.g., Pizza, Pasta)
- [ ] Click "Fire Items"
- [ ] Success message shows: "KOT #KOT-20251101-0001 created"
- [ ] Go to Kitchen → Dashboard
- [ ] Verify KOT appears (existing flow)
- [ ] Bar Dashboard has NO new BOTs

### Test 4: Create Mixed Order
- [ ] Go to Orders → New Order
- [ ] Select table
- [ ] Add bar items AND food items
- [ ] Click "Fire Items"
- [ ] Success message shows BOTH: "Beverage BOT #... and Food KOT #... created"
- [ ] Verify BOT in Bar Dashboard
- [ ] Verify KOT in Kitchen Dashboard
- [ ] Both contain correct items

### Test 5: BOT Workflow
- [ ] Open any BOT from Dashboard
- [ ] Click "Start Preparation" → Status changes to "In Progress"
- [ ] Click "Mark as Ready" → Status changes to "Ready"
- [ ] Click "Mark as Billed" → Status changes to "Billed"
- [ ] Verify timestamps recorded

### Test 6: BOT Print
- [ ] Open any BOT
- [ ] Click "Print" button
- [ ] Print window opens
- [ ] Verify format (80mm thermal)
- [ ] All items displayed correctly
- [ ] Alcohol badges show for alcoholic items
- [ ] Print functionality works

### Test 7: Void BOT
- [ ] Open any BOT (New or In Progress status)
- [ ] Click "Void BOT"
- [ ] Enter reason: "Customer cancelled"
- [ ] Confirm void
- [ ] Verify BOT status changes to "Void"
- [ ] Void reason recorded
- [ ] BOT no longer appears in active list

### Test 8: Audit Trail
```sql
-- Check audit records created
SELECT * FROM BOT_Audit ORDER BY Timestamp DESC;
```
- [ ] CREATE action logged when BOT created
- [ ] UPDATE actions logged for status changes
- [ ] PRINT action logged when printed
- [ ] VOID action logged with reason

### Test 9: Dashboard Stats
- [ ] Create multiple BOTs in different statuses
- [ ] Go to BOT Dashboard
- [ ] Verify statistics cards:
  - New count correct
  - In Progress count correct
  - Ready count correct
  - Billed today count correct
  - Total Active correct
  - Avg prep time calculated

### Test 10: Status Filters
- [ ] Click "New" filter → Only New BOTs shown
- [ ] Click "In Progress" → Only In Progress shown
- [ ] Click "Ready" → Only Ready shown
- [ ] Click "Billed" → Only Billed shown
- [ ] Click "All" → All BOTs shown

## 📊 Post-Deployment Monitoring

### Day 1: Immediate Checks
```sql
-- BOTs created today
SELECT COUNT(*) as BOTsCreated
FROM BOT_Header
WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE);

-- Status distribution
SELECT Status, COUNT(*) as Count
FROM BOT_Header
GROUP BY Status;

-- Average preparation time
SELECT AVG(DATEDIFF(MINUTE, CreatedAt, ServedAt)) as AvgPrepMinutes
FROM BOT_Header
WHERE ServedAt IS NOT NULL;

-- Audit activity
SELECT Action, COUNT(*) as Count
FROM BOT_Audit
WHERE CAST(Timestamp AS DATE) = CAST(GETDATE() AS DATE)
GROUP BY Action;

-- Voided BOTs (check for patterns)
SELECT BOT_No, VoidReason, VoidedAt
FROM BOT_Header
WHERE Status = 4
ORDER BY VoidedAt DESC;
```

### Week 1: Performance Review
- [ ] Review average preparation times
- [ ] Check void patterns and reasons
- [ ] Verify no duplicate BOT numbers
- [ ] Confirm tax calculations correct
- [ ] Review audit logs for anomalies
- [ ] Check database table sizes
- [ ] Monitor application performance

### Metrics to Track
| Metric | Target | Formula |
|--------|--------|---------|
| BOT Creation Success Rate | > 99% | (Successful / Total Attempts) × 100 |
| Avg Preparation Time | < 10 min | AVG(ServedAt - CreatedAt) |
| Void Rate | < 5% | (Voided / Total) × 100 |
| Dashboard Load Time | < 2 sec | Browser network tab |
| Print Success Rate | > 98% | Manual tracking |

## 🐛 Troubleshooting Guide

### Issue: BOT not created when firing items
**Check:**
1. Bar group exists: `SELECT * FROM menuitemgroup WHERE itemgroup = 'Bar'`
2. Items assigned to Bar group: `SELECT * FROM MenuItems WHERE menuitemgroupID = X`
3. Error logs in application
4. Database connection working

**Fix:**
- Create Bar group if missing
- Assign items to Bar group
- Check application logs

### Issue: Wrong items in BOT/KOT
**Check:**
1. Item menuitemgroupID assignments
2. ClassifyOrderItems logic in OrderController

**Fix:**
- Update item group assignments in database
- Verify Bar group ID

### Issue: Status update fails
**Check:**
1. UpdateBOTStatus stored procedure exists
2. User permissions
3. BOT not already at final status

**Fix:**
- Re-run BOT_Setup.sql if procedures missing
- Check database user permissions

### Issue: Print window doesn't open
**Check:**
1. Browser pop-up blocker
2. JavaScript errors in console

**Fix:**
- Allow pop-ups for site
- Try different browser

### Issue: Dashboard statistics incorrect
**Check:**
1. GetBOTDashboardStats procedure
2. BOT_Header data integrity

**Fix:**
- Re-execute stored procedure creation
- Verify BOT records have correct Status values

## 📞 Support Contacts

### Technical Support
- **Database Issues:** DBA Team
- **Application Errors:** Development Team
- **User Training:** Restaurant Manager

### Escalation Path
1. First: Restaurant Manager / Supervisor
2. Second: System Administrator
3. Third: Development Team

## ✅ Final Checklist

### Pre-Go-Live
- [ ] Database backup completed
- [ ] BOT_Setup.sql executed successfully
- [ ] Bar menu group created
- [ ] Bar items assigned and marked
- [ ] Application deployed and running
- [ ] All 10 tests passed
- [ ] Staff trained on BOT workflow
- [ ] Documentation distributed
- [ ] Support contacts confirmed

### Go-Live
- [ ] Announce BOT module active
- [ ] Monitor first orders closely
- [ ] Have technical support on standby
- [ ] Collect user feedback
- [ ] Log any issues immediately

### Post-Go-Live (First Week)
- [ ] Daily monitoring of metrics
- [ ] Review void reasons daily
- [ ] Check average prep times
- [ ] Verify billing accuracy
- [ ] Address user feedback
- [ ] Document lessons learned

## 📝 Sign-Off

**Database Setup Verified By:**
- Name: ________________
- Date: ________________
- Signature: ________________

**Application Deployment Verified By:**
- Name: ________________
- Date: ________________
- Signature: ________________

**Testing Completed By:**
- Name: ________________
- Date: ________________
- Signature: ________________

**Go-Live Approved By:**
- Name: ________________
- Date: ________________
- Signature: ________________

---

**Deployment Date:** _____________  
**Version:** 1.0  
**Module:** BAR/BOT (Beverage Order Ticket System)  
**Status:** ✅ READY FOR DEPLOYMENT
