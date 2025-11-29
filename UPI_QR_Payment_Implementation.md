# UPI QR Code Payment Implementation

## Overview
Implemented comprehensive UPI QR code payment feature allowing customers to scan and pay their bill amount directly using any UPI-enabled payment app (Google Pay, PhonePe, Paytm, BHIM, etc.).

## Implementation Date
December 2024

## Features Implemented

### 1. UPI Settings Management (Admin Configuration)
- **Location**: Settings → UPI Settings
- **URL**: `/UPISettings/Index`
- **Features**:
  - Enable/disable UPI payments with toggle switch
  - Configure UPI ID (e.g., `restaurant@paytm`, `9876543210@ybl`)
  - Set payee name (restaurant or business name)
  - Real-time validation of UPI ID format
  - Examples and helpful tips for configuration

### 2. QR Code Display - Payment Page
- **Location**: Payment/Index page
- **Display Conditions**:
  - UPI must be enabled in settings
  - Order must have remaining balance > ₹0
- **Features**:
  - Large, scannable QR code (250x250px)
  - Shows exact remaining amount
  - Lists supported UPI apps
  - Displays payee name for verification
  - Positioned between payment summary and action buttons

### 3. QR Code Display - PrintBill (A4 Format)
- **Location**: Payment/PrintBill page
- **Display Conditions**: Same as payment page
- **Features**:
  - Print-friendly QR code (200x200px)
  - Blue-bordered card with prominent styling
  - Clear payment instructions
  - Amount and payee details
  - Prints with receipt automatically

### 4. QR Code Display - POS Receipt (80mm)
- **Location**: Payment/PrintPOS page
- **Display Conditions**: Same as payment page
- **Features**:
  - Compact QR code (160px width) for narrow receipts
  - Centered layout optimized for thermal printers
  - Minimal text to save paper
  - Amount and supported apps listed

## Technical Architecture

### Database Changes

#### New Table: `UPISettings`
```sql
CREATE TABLE UPISettings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UPIId NVARCHAR(100) NOT NULL,
    PayeeName NVARCHAR(200) NOT NULL,
    IsEnabled BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedBy INT NULL,
    CONSTRAINT FK_UPISettings_Users FOREIGN KEY (UpdatedBy) REFERENCES Users(UserId)
);

-- Insert default disabled record
INSERT INTO UPISettings (UPIId, PayeeName, IsEnabled, UpdatedBy)
VALUES ('restaurant@paytm', 'Restaurant Name', 0, 1);
```

### Code Components

#### 1. Models (`Models/UPIModels.cs`)
- **UPISettings**: Entity model for database table
- **UPISettingsViewModel**: View model for form binding with validation

#### 2. Service (`Services/UPIQRCodeService.cs`)
- **GenerateUPIPaymentUrl()**: Creates UPI deep link in standard format
  - Format: `upi://pay?pa=<UPI_ID>&pn=<PAYEE_NAME>&am=<AMOUNT>&cu=INR&tn=<NOTE>`
- **GenerateQRCodeBase64()**: Converts QR code bitmap to base64 string
- **GenerateUPIQRCodeDataUrl()**: Generates complete data URL for `<img src>`

#### 3. Controller (`Controllers/UPISettingsController.cs`)
- **Index (GET)**: Display UPI settings configuration page
- **Update (POST)**: Save UPI settings with validation
  - Validates UPI ID contains '@' symbol
  - Validates required fields
  - Updates timestamp and user

#### 4. Payment Integration (`Controllers/PaymentController.cs`)
- Modified **GetPaymentViewModel()** method
- Loads UPI settings from database
- Generates QR code if enabled and remaining amount > 0
- Adds UPI data to PaymentViewModel

#### 5. View Models (`Models/PaymentViewModels.cs`)
Extended **PaymentViewModel** with:
- `UPIEnabled` (bool): Whether UPI payments are enabled
- `UPIQRCodeDataUrl` (string): Data URL for QR code image
- `UPIId` (string): UPI ID for display
- `UPIPayeeName` (string): Payee name for verification

#### 6. Views
- **Views/UPISettings/Index.cshtml**: Admin settings page with toggle and form
- **Views/Payment/Index.cshtml**: Updated with UPI QR card section
- **Views/Payment/PrintBill.cshtml**: Updated with print-friendly QR section
- **Views/Payment/PrintPOS.cshtml**: Updated with compact QR for thermal printer

### Dependencies

#### NuGet Package
- **QRCoder** v1.7.0
  - Purpose: Generate QR codes from strings
  - Platform Notes: Uses Windows-specific APIs (System.Drawing)
  - Works on macOS despite warnings

## UPI Deep Link Format

The system generates standard UPI payment URLs that work with all major UPI apps:

```
upi://pay?pa=<UPI_ID>&pn=<PAYEE_NAME>&am=<AMOUNT>&cu=INR&tn=<TRANSACTION_NOTE>
```

**Parameters:**
- `pa` (payee address): UPI ID of the restaurant
- `pn` (payee name): Display name of restaurant
- `am` (amount): Exact amount to be paid (remaining balance)
- `cu` (currency): INR (Indian Rupees)
- `tn` (transaction note): Order number for reference

**Example:**
```
upi://pay?pa=restaurant@paytm&pn=My%20Restaurant&am=1250.00&cu=INR&tn=Order%20ORD-12345
```

## Configuration Guide

### Initial Setup (Admin)

1. **Navigate to Settings**
   - Click "Settings" in the navigation menu
   - Select "UPI Settings"

2. **Enable UPI Payments**
   - Toggle the "Enable UPI Payments" switch to ON (green)

3. **Configure UPI ID**
   - Enter your UPI ID in format: `username@provider`
   - Examples:
     - `restaurant@paytm` (Paytm Business)
     - `9876543210@ybl` (PhonePe/Google Pay)
     - `yourbusiness@okaxis` (Google Pay for Business)

4. **Set Payee Name**
   - Enter your restaurant/business name
   - This will be displayed to customers when they scan

5. **Save Configuration**
   - Click "Save Settings"
   - System will validate UPI ID format
   - Success message confirms settings saved

### Testing

1. **Create a Test Order**
   - Create an order with items
   - Navigate to Payment page

2. **Verify QR Code Display**
   - Ensure "Scan to Pay" card appears
   - QR code should be visible and scannable
   - Remaining amount should be correct

3. **Test Scanning**
   - Open any UPI app (Google Pay, PhonePe, etc.)
   - Scan the QR code
   - Verify:
     - Amount is pre-filled correctly
     - Payee name matches configuration
     - Transaction note shows order number

4. **Test Print Views**
   - Click "Print Bill (A4)" - verify QR appears
   - Click "Print Receipt (POS)" - verify QR appears in thermal format

### Disabling UPI Payments

1. Navigate to UPI Settings
2. Toggle "Enable UPI Payments" to OFF (gray)
3. Click "Save Settings"
4. QR codes will no longer appear on payment pages or prints

## User Experience

### Customer Journey

1. **At Payment Screen**
   - Customer sees their order summary
   - Below summary, "Scan to Pay" card displays:
     - Large QR code
     - Exact amount to pay
     - Supported payment apps
     - Restaurant name

2. **Scanning Process**
   - Customer opens their UPI app
   - Scans QR code using in-app scanner
   - App automatically:
     - Fills payment amount
     - Shows restaurant name
     - Adds order reference
   - Customer verifies and confirms payment

3. **Post-Payment**
   - Staff receives payment notification
   - Staff marks payment received in system
   - Customer gets payment confirmation

### Staff Experience

1. **Configuration** (One-time setup)
   - Admin enables UPI in settings
   - Adds UPI ID and business name
   - Tests with a sample transaction

2. **Daily Operations**
   - QR codes appear automatically on unpaid orders
   - No additional steps required
   - Customer scans and pays
   - Staff receives UPI notification
   - Staff records payment in system

3. **Printed Bills**
   - QR code prints automatically on:
     - A4 bills (customer requests printed bill)
     - POS receipts (thermal printer)
   - Customer can take bill home and pay later

## Security Considerations

### Data Protection
- UPI ID stored in database with restricted access
- Only admin users can modify UPI settings
- No customer payment data stored by the system

### Payment Verification
- System does NOT automatically verify payments
- Staff must confirm UPI payment receipt before marking paid
- Payment reconciliation should match UPI transaction history

### Best Practices
1. **Use Business UPI IDs**: Prefer merchant/business UPI accounts
2. **Verify Payments**: Always confirm UPI notification before marking paid
3. **Daily Reconciliation**: Match UPI transaction history with recorded payments
4. **Secure Settings**: Restrict access to Settings → UPI Settings page

## Limitations & Known Issues

### Platform Warnings
- QRCoder library uses Windows-specific APIs (System.Drawing)
- Generates 4 compiler warnings (CA1416) - these are non-blocking
- Application works correctly on macOS despite warnings
- For true cross-platform support, consider migrating to SkiaSharp

### Current Limitations
1. **Manual Payment Confirmation**: System doesn't auto-verify UPI payments
2. **Single UPI ID**: Only one UPI ID can be configured at a time
3. **No Payment Gateway Integration**: Direct UPI deep links only (no webhook callbacks)

### Future Enhancements (Possible)
- [ ] Add multiple UPI IDs (different for different payment types)
- [ ] Integrate UPI payment gateway for automatic verification
- [ ] Add payment expiry time to QR codes
- [ ] Store UPI transaction IDs for reconciliation
- [ ] Generate payment links for SMS/WhatsApp sharing
- [ ] Add QR code branding/logo in center

## Troubleshooting

### QR Code Not Appearing

**Symptoms**: QR code card doesn't show on payment page

**Checklist**:
1. ✅ UPI Settings enabled? (Settings → UPI Settings → toggle ON)
2. ✅ Remaining amount > ₹0? (QR only shows for unpaid balance)
3. ✅ UPI ID configured? (Must have valid UPI ID saved)
4. ✅ Payee name set? (Required field)

**Solution**: Navigate to Settings → UPI Settings and verify all fields

### QR Code Not Scanning

**Symptoms**: UPI app can't read QR code

**Checklist**:
1. ✅ QR code image clear and not pixelated?
2. ✅ Adequate lighting for camera to focus?
3. ✅ UPI app up to date?
4. ✅ Camera permissions granted to UPI app?

**Solution**: 
- Try different UPI app (Google Pay, PhonePe, Paytm)
- Increase screen brightness if on digital display
- Print receipt if digital display not working

### Wrong Amount in QR Code

**Symptoms**: UPI app shows different amount than expected

**Checklist**:
1. ✅ Check `Model.RemainingAmount` value
2. ✅ Verify no recent payments unrecorded
3. ✅ Refresh payment page to get latest data

**Solution**: 
- Reload the payment page (F5)
- Verify all payments are recorded in system
- Check payment history section

### Settings Page Not Accessible

**Symptoms**: Can't find or access UPI Settings

**Checklist**:
1. ✅ Logged in as admin user?
2. ✅ Navigation permissions granted?
3. ✅ Direct URL: `/UPISettings/Index`

**Solution**: Contact system administrator to grant Settings access

## Database Queries for Troubleshooting

### Check Current UPI Settings
```sql
SELECT * FROM UPISettings ORDER BY Id DESC;
```

### View Recent UPI Setting Changes
```sql
SELECT 
    u.UPIId, 
    u.PayeeName, 
    u.IsEnabled, 
    u.UpdatedAt,
    usr.Username as UpdatedByUser
FROM UPISettings u
LEFT JOIN Users usr ON u.UpdatedBy = usr.UserId
ORDER BY u.UpdatedAt DESC;
```

### Find Orders with Remaining Balance (Need QR Codes)
```sql
SELECT 
    o.OrderId,
    o.OrderNumber,
    o.TotalAmount,
    (SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p WHERE p.OrderId = o.OrderId) as PaidAmount,
    (o.TotalAmount - (SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p WHERE p.OrderId = o.OrderId)) as RemainingAmount
FROM Orders o
WHERE o.OrderStatus IN (1, 2) -- Active/Completed but not fully paid
HAVING (o.TotalAmount - (SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p WHERE p.OrderId = o.OrderId)) > 0
ORDER BY o.CreatedAt DESC;
```

## File Changes Summary

### New Files Created
1. `SQL/create_upi_settings_table.sql` - Database schema
2. `Models/UPIModels.cs` - Data models
3. `Services/UPIQRCodeService.cs` - QR generation service
4. `Controllers/UPISettingsController.cs` - Settings management
5. `Views/UPISettings/Index.cshtml` - Settings UI

### Modified Files
1. `Models/PaymentViewModels.cs` - Added UPI properties
2. `Controllers/PaymentController.cs` - Added UPI loading in GetPaymentViewModel()
3. `Views/Payment/Index.cshtml` - Added QR card section
4. `Views/Payment/PrintBill.cshtml` - Added printable QR section
5. `Views/Payment/PrintPOS.cshtml` - Added thermal receipt QR section

### Configuration Files
1. `RestaurantManagementSystem.csproj` - Added QRCoder package reference

## Deployment Notes

### Pre-Deployment Checklist
- [ ] QRCoder v1.7.0 NuGet package installed
- [ ] UPISettings table created in database
- [ ] Default record inserted in UPISettings table
- [ ] All files compiled successfully (warnings are acceptable)
- [ ] Test QR code generation with sample UPI ID

### Deployment Steps
1. **Stop Application** (if running)
2. **Database Migration**: Execute `create_upi_settings_table.sql`
3. **Build Application**: `dotnet build` (verify success)
4. **Publish Application**: `dotnet publish -c Release`
5. **Deploy Files**: Copy published files to production
6. **Configure UPI Settings**: 
   - Access `/UPISettings/Index`
   - Enable UPI payments
   - Set your business UPI ID
   - Set payee name
   - Test with sample order
7. **Verify**: Test QR code scanning with live UPI app

### Rollback Plan
If issues occur:
1. Disable UPI in settings (toggle OFF)
2. QR codes will stop appearing
3. All other functionality remains intact
4. No data loss - settings preserved in database

## Support & Maintenance

### Regular Maintenance
- **Weekly**: Verify UPI ID is active and receiving payments
- **Monthly**: Reconcile UPI transaction history with system payments
- **Quarterly**: Test QR code scanning across different devices/apps

### Common Support Questions

**Q: Can customers pay more than the bill amount?**
A: No, the QR code pre-fills the exact remaining amount. UPI apps enforce this amount.

**Q: What if customer's UPI app doesn't scan QR codes?**
A: They can manually enter the UPI ID and amount. All UPI apps support manual entry.

**Q: Can we change UPI ID later?**
A: Yes, navigate to Settings → UPI Settings and update anytime. Existing QR codes will use new ID immediately.

**Q: Do old printed bills with QR codes still work?**
A: Yes, if the UPI ID hasn't changed. If changed, old QR codes will direct to old UPI ID.

**Q: Is there a transaction fee?**
A: UPI payment fees (if any) depend on your payment provider and merchant agreement. The system doesn't add fees.

## Contact & Credits

**Implementation Date**: December 2024  
**Developer**: GitHub Copilot (Claude Sonnet 4.5)  
**Project**: Restaurant Management System  
**UPI Standard**: NPCI UPI Specification  
**QR Library**: QRCoder v1.7.0 by Raffael Herrmann

---

## Appendix: UPI Deep Link Reference

### Standard Parameters
| Parameter | Description | Required | Example |
|-----------|-------------|----------|---------|
| `pa` | Payee Address (UPI ID) | Yes | `restaurant@paytm` |
| `pn` | Payee Name | Yes | `My Restaurant` |
| `am` | Amount | Yes | `1250.50` |
| `cu` | Currency | Yes | `INR` |
| `tn` | Transaction Note | No | `Order ORD-123` |
| `tr` | Transaction Reference | No | `REF123456` |
| `url` | URL for additional info | No | `https://example.com` |

### URL Encoding
Special characters in parameters must be URL-encoded:
- Space → `%20`
- `@` → `%40` (usually not encoded in pa parameter)
- `#` → `%23`

### Testing UPI Links
Test generated links using online UPI validators or by scanning with actual UPI apps.

---

*End of Documentation*
