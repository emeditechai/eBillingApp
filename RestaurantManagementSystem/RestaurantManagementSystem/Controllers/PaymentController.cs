using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace RestaurantManagementSystem.Controllers
{
    public partial class PaymentController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ILogger<PaymentController> _logger;

        // Helper to get merged table display name for an order
        private string GetMergedTableDisplayName(int orderId, string existingTableName)
        {
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT STRING_AGG(t.TableName, ' + ') WITHIN GROUP (ORDER BY t.TableName)
                        FROM OrderTables ot
                        INNER JOIN Tables t ON ot.TableId = t.Id
                        WHERE ot.OrderId = @OrderId", connection);
                    cmd.Parameters.AddWithValue("@OrderId", orderId);
                    var aggregated = cmd.ExecuteScalar() as string;
                    
                    if (string.IsNullOrWhiteSpace(aggregated))
                        return existingTableName; // No merged tables, return original
                    
                    // If there's both a primary table and merged tables, combine without duplicates
                    if (!string.IsNullOrWhiteSpace(existingTableName) && !aggregated.Contains(existingTableName))
                        return existingTableName + " + " + aggregated;
                    
                    return aggregated; // Return merged table names
                }
            }
            catch
            {
                return existingTableName; // Fallback to existing if error
            }
        }

        public PaymentController(IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }
        
        // Payment Dashboard
        public IActionResult Index(int id)
        {
            var model = GetPaymentViewModel(id);
            
            if (model == null)
            {
                return NotFound();
            }

            // Read BillFormat from RestaurantSettings to control which print buttons are shown
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT TOP 1 BillFormat FROM dbo.RestaurantSettings", conn))
                    {
                        var val = cmd.ExecuteScalar();
                        ViewBag.BillFormat = (val != null && val != DBNull.Value) ? val.ToString() : "A4";
                    }
                }
            }
            catch
            {
                ViewBag.BillFormat = "A4"; // default
            }
            

            
            return View(model);
        }
        
        // Fix fully paid orders that are stuck in active status
        public IActionResult FixPaidOrderStatus(int orderId)
        {
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Orders 
                        SET Status = 3, -- Completed
                            CompletedAt = GETDATE(),
                            UpdatedAt = GETDATE()
                        WHERE Id = @OrderId 
                        AND Status < 3 -- Not already completed
                        AND (
                            SELECT ISNULL(SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)), 0) 
                            FROM Payments 
                            WHERE OrderId = @OrderId AND Status = 1
                        ) >= TotalAmount", connection))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Order status updated to Completed successfully.";
                        }
                        else
                        {
                            TempData["InfoMessage"] = "Order is either already completed or not fully paid.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating order status: {ex.Message}";
            }
            
            return RedirectToAction("Index", new { id = orderId });
        }
        
        // Fix all fully paid orders that are stuck in active status
        public IActionResult FixAllPaidOrders()
        {
            try
            {
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Orders 
                        SET Status = 3, -- Completed
                            CompletedAt = GETDATE(),
                            UpdatedAt = GETDATE()
                        WHERE Status < 3 -- Not already completed
                        AND Id IN (
                            SELECT o.Id
                            FROM Orders o
                            WHERE o.Status < 3
                            AND (
                                SELECT ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0) 
                                FROM Payments p 
                                WHERE p.OrderId = o.Id AND p.Status = 1
                            ) >= o.TotalAmount
                        )", connection))
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();
                        TempData["SuccessMessage"] = $"Fixed {rowsAffected} fully paid orders that were stuck in active status.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error fixing paid orders: {ex.Message}";
            }
            
            return RedirectToAction("Dashboard", "Order");
        }
        
        // Process Payment
        public IActionResult ProcessPayment(int orderId)
        {
            // Get payment view model with GST calculations
            var paymentViewModel = GetPaymentViewModel(orderId);
            if (paymentViewModel == null)
            {
                return NotFound();
            }
            
            var model = new ProcessPaymentViewModel
            {
                OrderId = orderId,
                OrderNumber = paymentViewModel.OrderNumber,
                TotalAmount = paymentViewModel.TotalAmount, // This now includes GST
                RemainingAmount = paymentViewModel.RemainingAmount, // This is Total - Paid (including GST)
                Amount = paymentViewModel.RemainingAmount, // Default to remaining amount
                Subtotal = paymentViewModel.Subtotal, // base amount before GST
                GSTPercentage = paymentViewModel.GSTPercentage // dynamic GST %
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                // Ensure UPI and Complementary methods exist
                using (var ensureCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
-- Ensure UPI method exists
IF NOT EXISTS (SELECT 1 FROM PaymentMethods WHERE Name='UPI')
BEGIN
    INSERT INTO PaymentMethods (Name, DisplayName, IsActive, RequiresCardInfo, RequiresCardPresent, RequiresApproval)
    VALUES ('UPI','UPI',1,0,0,0);
END

-- Ensure Complementary method exists
IF NOT EXISTS (SELECT 1 FROM PaymentMethods WHERE Name='Complementary')
BEGIN
    INSERT INTO PaymentMethods (Name, DisplayName, IsActive, RequiresCardInfo, RequiresCardPresent, RequiresApproval)
    VALUES ('Complementary','Complementary (100% Discount)',1,0,0,1);
END", connection))
                {
                    ensureCmd.ExecuteNonQuery();
                }
                
                // Get available payment methods
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name, DisplayName, RequiresCardInfo
                    FROM PaymentMethods
                    WHERE IsActive = 1
                    ORDER BY DisplayName", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailablePaymentMethods.Add(new SelectListItem
                            {
                                Value = reader.GetInt32(0).ToString(),
                                Text = reader.GetString(2)
                            });
                            if (reader.GetString(1).Equals("UPI", StringComparison.OrdinalIgnoreCase))
                            {
                                model.IsUPIPayment = true; // marker for JS (initial load none selected so not used yet)
                            }
                        }
                    }
                }
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult ProcessPayment(ProcessPaymentViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Validate payment method requires card info
                    bool requiresCardInfo = false;
                    string paymentMethodName = string.Empty;
                    
                    // Read approval settings to decide if discounts or card payments need approval
                    bool discountApprovalRequired = false;
                    bool cardPaymentApprovalRequired = false;
                    try
                    {
                        using (var settingsConn = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                        {
                            settingsConn.Open();
                            using (var settingsCmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT TOP 1 IsDiscountApprovalRequired, IsCardPaymentApprovalRequired FROM dbo.RestaurantSettings ORDER BY Id DESC", settingsConn))
                            {
                                using (var rs = settingsCmd.ExecuteReader())
                                {
                                    if (rs.Read())
                                    {
                                        if (!rs.IsDBNull(0)) discountApprovalRequired = rs.GetBoolean(0);
                                        if (!rs.IsDBNull(1)) cardPaymentApprovalRequired = rs.GetBoolean(1);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If settings read fails, default to existing behavior (no extra approvals)
                        discountApprovalRequired = false;
                        cardPaymentApprovalRequired = false;
                    }

                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                            SELECT Name, RequiresCardInfo FROM PaymentMethods WHERE Id = @PaymentMethodId", connection))
                        {
                            command.Parameters.AddWithValue("@PaymentMethodId", model.PaymentMethodId);
                            using (var rdr = command.ExecuteReader())
                            {
                                if (rdr.Read())
                                {
                                    paymentMethodName = rdr.GetString(0);
                                    requiresCardInfo = rdr.GetBoolean(1);
                                }
                            }
                        }
                    }
                    
                    // Validate card info if required
                    if (requiresCardInfo)
                    {
                        if (string.IsNullOrEmpty(model.LastFourDigits))
                        {
                            ModelState.AddModelError("LastFourDigits", "Last four digits of card are required for this payment method.");
                            return View(model);
                        }
                        
                        if (string.IsNullOrEmpty(model.CardType))
                        {
                            ModelState.AddModelError("CardType", "Card type is required for this payment method.");
                            return View(model);
                        }
                    }
                    
                    // Calculate GST information before processing payment
                    decimal gstPercentage = 5.0m; // Default
                    decimal subtotal = 0m;
                    decimal gstAmount = 0m;
                    decimal cgstAmount = 0m;
                    decimal sgstAmount = 0m;
                    decimal amountExclGST = 0m;
                    
                    // Get GST percentage and order subtotal
                    using (Microsoft.Data.SqlClient.SqlConnection gstConnection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        gstConnection.Open();
                        
                        // Get GST percentage from settings
                        using (Microsoft.Data.SqlClient.SqlCommand gstCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            "SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", gstConnection))
                        {
                            var result = gstCmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                gstPercentage = Convert.ToDecimal(result);
                            }
                        }
                        
                        // Get order subtotal for GST calculation
                        using (Microsoft.Data.SqlClient.SqlCommand subtotalCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            "SELECT Subtotal FROM Orders WHERE Id = @OrderId", gstConnection))
                        {
                            subtotalCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                            var subtotalResult = subtotalCmd.ExecuteScalar();
                            if (subtotalResult != null && subtotalResult != DBNull.Value)
                            {
                                subtotal = Convert.ToDecimal(subtotalResult);
                            }
                        }
                    }
                    
                    // Get GST percentage and order details
                    decimal paymentGstPercentage = 5.0m; // Default fallback
                    decimal orderSubtotal = 0m;
                    using (var gstConnection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        gstConnection.Open();
                        
                        // Get GST percentage from settings
                        using (var gstCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", gstConnection))
                        {
                            var result = gstCmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                paymentGstPercentage = Convert.ToDecimal(result);
                            }
                        }
                        
                        // Get order subtotal (amount before GST)
                        using (var subtotalCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT Subtotal FROM Orders WHERE Id = @OrderId", gstConnection))
                        {
                            subtotalCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                            var subtotalResult = subtotalCmd.ExecuteScalar();
                            if (subtotalResult != null && subtotalResult != DBNull.Value)
                            {
                                orderSubtotal = Convert.ToDecimal(subtotalResult);
                            }
                        }
                    }
                    
                    // NEW CORRECT PROCESS: Apply discount on subtotal, then recalculate GST
                    // Step 1: Apply discount on original subtotal (excludes GST)
                    decimal discountAmount = model.DiscountAmount;
                    decimal discountedSubtotal = orderSubtotal - discountAmount;
                    
                    // Step 2: Calculate GST on the discounted subtotal
                    decimal paymentGstAmount = Math.Round(discountedSubtotal * paymentGstPercentage / 100m, 2, MidpointRounding.AwayFromZero);
                    
                    // Step 3: Calculate final amounts
                    decimal paymentAmountExclGST = discountedSubtotal; // This is the subtotal after discount
                    decimal totalPaymentAmountWithGST = discountedSubtotal + paymentGstAmount; // Final amount customer pays
                    
                    // Check for Complementary payment method - ensure discount is properly set
                    if (paymentMethodName.Equals("Complementary", StringComparison.OrdinalIgnoreCase))
                    {
                        // For Complementary, ensure 100% discount is applied
                        discountAmount = orderSubtotal; // Full subtotal amount as discount
                        discountedSubtotal = 0; // After 100% discount, subtotal is 0
                        paymentGstAmount = 0; // No GST on a zero subtotal
                        paymentAmountExclGST = 0; // Zero subtotal after discount
                        totalPaymentAmountWithGST = 0; // Zero total to pay
                        
                        // If the model didn't already have the discount set to full amount
                        model.DiscountAmount = discountAmount;
                    }
                    
                    // Step 4: Split GST into CGST and SGST (equal split)
                    decimal paymentCgstPercentage = paymentGstPercentage / 2m;
                    decimal paymentSgstPercentage = paymentGstPercentage / 2m;
                    decimal paymentCgstAmount = Math.Round(paymentGstAmount / 2m, 2, MidpointRounding.AwayFromZero);
                    decimal paymentSgstAmount = paymentGstAmount - paymentCgstAmount; // Ensures total adds up exactly
                    
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("[dbo].[usp_ProcessPayment]", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            
                            command.Parameters.AddWithValue("@OrderId", model.OrderId);
                            command.Parameters.AddWithValue("@PaymentMethodId", model.PaymentMethodId);
                            // Send canonical (pre-round) payment amount to the DB; the UI shows 'Total to Process' (rounded) to cashier/customer.
                            var paymentAmountToStore = model.OriginalAmount > 0 ? model.OriginalAmount : totalPaymentAmountWithGST;
                            command.Parameters.AddWithValue("@Amount", paymentAmountToStore); // Store canonical payment amount (discounted subtotal + GST)
                            command.Parameters.AddWithValue("@TipAmount", model.TipAmount);
                            command.Parameters.AddWithValue("@ReferenceNumber", string.IsNullOrEmpty(model.ReferenceNumber) ? (object)DBNull.Value : model.ReferenceNumber);
                            command.Parameters.AddWithValue("@LastFourDigits", string.IsNullOrEmpty(model.LastFourDigits) ? (object)DBNull.Value : model.LastFourDigits);
                            command.Parameters.AddWithValue("@CardType", string.IsNullOrEmpty(model.CardType) ? (object)DBNull.Value : model.CardType);
                            command.Parameters.AddWithValue("@AuthorizationCode", string.IsNullOrEmpty(model.AuthorizationCode) ? (object)DBNull.Value : model.AuthorizationCode);
                            command.Parameters.AddWithValue("@Notes", string.IsNullOrEmpty(model.Notes) ? (object)DBNull.Value : model.Notes);
                            command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                            command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                            
                            // Add GST-related parameters
                            command.Parameters.AddWithValue("@GSTAmount", paymentGstAmount);
                            command.Parameters.AddWithValue("@CGSTAmount", paymentCgstAmount);
                            command.Parameters.AddWithValue("@SGSTAmount", paymentSgstAmount);
                            command.Parameters.AddWithValue("@DiscAmount", model.DiscountAmount);
                            command.Parameters.AddWithValue("@GST_Perc", paymentGstPercentage);
                            command.Parameters.AddWithValue("@CGST_Perc", paymentCgstPercentage);
                            command.Parameters.AddWithValue("@SGST_Perc", paymentSgstPercentage);
                            command.Parameters.AddWithValue("@Amount_ExclGST", paymentAmountExclGST); // Amount excluding GST
                            // Roundoff adjustment (client calculated)
                            command.Parameters.AddWithValue("@RoundoffAdjustmentAmt", model.RoundoffAdjustmentAmt);
                            
                            // Note: ForceApproval will be handled after payment creation if discount is applied
                            
                            // If UPI selected store reference in ReferenceNumber if not provided separately
                            if (paymentMethodName.Equals("UPI", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(model.UPIReference))
                            {
                                // override ReferenceNumber param value
                                command.Parameters["@ReferenceNumber"].Value = model.UPIReference;
                            }
                            
                            using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int paymentId = reader.GetInt32(0);
                                    int paymentStatus = reader.GetInt32(1);
                                    string message = reader.GetString(2);
                                    
                                    if (paymentId > 0)
                                    {
                                        // Decide whether this payment should be pending based on settings and payment details
                                        // New rule: If a discount was applied, respect the discount-approval setting only.
                                        // That is, when discounts DO NOT require approval (discountApprovalRequired == false),
                                        // the payment must NOT be forced pending even if the payment method (e.g. card)
                                        // normally requires approval. This ensures that when Discount Approval is disabled,
                                        // a full payment (including discount) completes the order immediately.
                                        bool needsApproval = false;

                                        if (model.DiscountAmount > 0)
                                        {
                                            // Only require approval for discount payments when discount approvals are enabled
                                            needsApproval = discountApprovalRequired;
                                        }
                                        else
                                        {
                                            // No discount involved — fall back to card-approval rules
                                            if (requiresCardInfo && cardPaymentApprovalRequired)
                                            {
                                                needsApproval = true;
                                            }
                                        }

                                        // If needsApproval is true, ensure the payment is pending (Status = 0)
                                        // Otherwise, ensure the payment is approved (Status = 1)
                                        if (needsApproval)
                                        {
                                            if (paymentStatus == 1)
                                            {
                                                // Update DB to mark pending
                                                reader.Close();
                                                using (var forceApprovalCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    UPDATE Payments 
                                                    SET Status = 0, 
                                                        UpdatedAt = GETDATE(),
                                                        Notes = CASE 
                                                            WHEN Notes IS NULL OR Notes = '' THEN @Note
                                                            ELSE Notes + ' | ' + @Note
                                                        END
                                                    WHERE Id = @PaymentId", connection))
                                                {
                                                    string note = "Requires approval";
                                                    if (model.DiscountAmount > 0) note = $"Discount applied - requires approval";
                                                    forceApprovalCmd.Parameters.AddWithValue("@PaymentId", paymentId);
                                                    forceApprovalCmd.Parameters.AddWithValue("@Note", note);
                                                    forceApprovalCmd.ExecuteNonQuery();
                                                }
                                                paymentStatus = 0; // Update local variable to reflect pending status
                                            }
                                        }
                                        else
                                        {
                                            // Ensure payment is approved if it was created as pending by payment method or DB defaults
                                            if (paymentStatus == 0)
                                            {
                                                reader.Close();
                                                using (var approveCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    UPDATE Payments 
                                                    SET Status = 1, 
                                                        UpdatedAt = GETDATE()
                                                    WHERE Id = @PaymentId", connection))
                                                {
                                                    approveCmd.Parameters.AddWithValue("@PaymentId", paymentId);
                                                    approveCmd.ExecuteNonQuery();
                                                }
                                                paymentStatus = 1;
                                            }
                                        }

                                        if (paymentStatus == 1) // Approved
                                        {
                                            TempData["SuccessMessage"] = "Payment processed successfully.";
                                            // If approved, attempt to mark order as completed when fully paid
                                            try
                                            {
                                                if (!reader.IsClosed) reader.Close();

                                                // Log order / payments sums and complete only if approved payments cover total (within tolerance)
                                                using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    SELECT o.TotalAmount,
                                                        ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 1), 0) AS ApprovedSum,
                                                        ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 0), 0) AS PendingSum
                                                    FROM Orders o
                                                    WHERE o.Id = @OrderId
                                                ", connection))
                                                {
                                                    checkCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                    using (var r2 = checkCmd.ExecuteReader())
                                                    {
                                                        if (r2.Read())
                                                        {
                                                            decimal orderTotal = r2.IsDBNull(0) ? 0m : r2.GetDecimal(0);
                                                            decimal approvedSum = r2.IsDBNull(1) ? 0m : r2.GetDecimal(1);
                                                            decimal pendingSum = r2.IsDBNull(2) ? 0m : r2.GetDecimal(2);
                                                            _logger?.LogInformation("ProcessPayment: order {OrderId} total={OrderTotal} approvedSum={ApprovedSum} pendingSum={PendingSum}", model.OrderId, orderTotal, approvedSum, pendingSum);

                                                            if (approvedSum >= orderTotal - 0.05m)
                                                            {
                                                                using (var completeCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                                    UPDATE Orders
                                                                    SET Status = 3,
                                                                        CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                                        UpdatedAt = GETDATE()
                                                                    WHERE Id = @OrderId AND Status < 3
                                                                ", connection))
                                                                {
                                                                    completeCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                                    completeCmd.ExecuteNonQuery();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                _logger?.LogInformation("ProcessPayment: order {OrderId} not completed after payment - shortfall={Shortfall}", model.OrderId, Math.Max(0, (double)(orderTotal - approvedSum)));
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch { /* ignore order update failures */ }
                                            
                                                    // Persist aggregate roundoff into Orders.RoundoffAdjustmentAmt so order-level
                                                    // roundoff is easily queryable (user added this column to Orders table).
                                                    try
                                                    {
                                                        if (!reader.IsClosed) reader.Close();
                                                        using (var roundoffSumCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                            SELECT ISNULL(SUM(ISNULL(RoundoffAdjustmentAmt,0)), 0) FROM Payments WHERE OrderId = @OrderId AND Status = 1
                                                        ", connection))
                                                        {
                                                            roundoffSumCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                            var sumObj = roundoffSumCmd.ExecuteScalar();
                                                            decimal totalRoundoffForOrder = 0m;
                                                            if (sumObj != null && sumObj != DBNull.Value)
                                                            {
                                                                totalRoundoffForOrder = Convert.ToDecimal(sumObj);
                                                            }

                                                            using (var updateOrderRoundoffCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                                UPDATE Orders SET RoundoffAdjustmentAmt = @Roundoff, UpdatedAt = GETDATE() WHERE Id = @OrderId
                                                            ", connection))
                                                            {
                                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@Roundoff", totalRoundoffForOrder);
                                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                                updateOrderRoundoffCmd.ExecuteNonQuery();
                                                            }
                                                        }
                                                    }
                                                    catch { /* ignore roundoff persistence failures to avoid blocking payment success */ }
                                        }
                                        else // Pending
                                        {
                                            if (model.DiscountAmount > 0 && discountApprovalRequired)
                                            {
                                                TempData["InfoMessage"] = $"Payment with discount of ₹{model.DiscountAmount:F2} requires approval. It has been saved as pending.";
                                            }
                                            else if (requiresCardInfo && cardPaymentApprovalRequired)
                                            {
                                                TempData["InfoMessage"] = "Card payment requires approval. It has been saved as pending.";
                                            }
                                            else
                                            {
                                                TempData["InfoMessage"] = "Payment requires approval. It has been saved as pending.";
                                            }
                                        }

                                        // If discount provided update order with proper GST recalculation
                                        if (model.DiscountAmount > 0)
                                        {
                                            if (!reader.IsClosed) reader.Close();
                                            using (var discountCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                -- Get current order values
                                                DECLARE @CurrentDiscount DECIMAL(18,2);
                                                DECLARE @CurrentSubtotal DECIMAL(18,2);
                                                DECLARE @CurrentTipAmount DECIMAL(18,2);
                                                
                                                SELECT @CurrentDiscount = ISNULL(DiscountAmount, 0),
                                                       @CurrentSubtotal = Subtotal,
                                                       @CurrentTipAmount = ISNULL(TipAmount, 0)
                                                FROM Orders 
                                                WHERE Id = @OrderId;
                                                
                                                -- Calculate new values based on discount applied to subtotal
                                                DECLARE @NewDiscountAmount DECIMAL(18,2) = @CurrentDiscount + @Disc;
                                                DECLARE @NetSubtotal DECIMAL(18,2) = @CurrentSubtotal - @NewDiscountAmount;
                                                DECLARE @NewGSTAmount DECIMAL(18,2) = ROUND(@NetSubtotal * @GSTPerc / 100, 2);
                                                DECLARE @NewTotalAmount DECIMAL(18,2) = @NetSubtotal + @NewGSTAmount + @CurrentTipAmount;
                                                
                                                -- Update order with recalculated amounts
                                                UPDATE Orders 
                                                SET DiscountAmount = @NewDiscountAmount, 
                                                    UpdatedAt = GETDATE(),
                                                    TaxAmount = @NewGSTAmount,
                                                    TotalAmount = @NewTotalAmount
                                                    
                                                WHERE Id = @OrderId", connection))
                                            {
                                                discountCmd.Parameters.AddWithValue("@Disc", model.DiscountAmount);
                                                discountCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                discountCmd.Parameters.AddWithValue("@GSTPerc", paymentGstPercentage);
                                                discountCmd.ExecuteNonQuery();
                                            }
                                            // Re-check order completion after discount/total recalculation
                                            try
                                            {
                                                using (var orderUpdateAfterDiscountCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                    UPDATE Orders
                                                    SET Status = 3, -- Completed
                                                        CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                        UpdatedAt = GETDATE()
                                                    WHERE Id = @OrderId
                                                      AND Status < 3
                                                      AND (
                                                          TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 1), 0)
                                                          ) <= 0.05
                                                ", connection))
                                                {
                                                    orderUpdateAfterDiscountCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                    orderUpdateAfterDiscountCmd.ExecuteNonQuery();
                                                }
                                            }
                                            catch { /* ignore order completion re-check failures */ }
                                        }
                                        // FINAL SAFETY CHECK: If the order is fully paid considering both approved and pending
                                        // payments (this covers discount payments that are saved as pending), mark the order
                                        // completed so fully-paid orders don't remain in Active state. We intentionally
                                        // include payments with Status IN (0,1) here but leave discount approval workflow
                                        // (payment status) unchanged.
                                        try
                                        {
                                            if (!reader.IsClosed) reader.Close();
                                            // If discount approvals are required, do NOT count pending payments that have a discount
                                            string finalSql;
                                            if (discountApprovalRequired)
                                            {
                                                finalSql = @"UPDATE Orders
                                                SET Status = 3,
                                                    CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                    UpdatedAt = GETDATE()
                                                WHERE Id = @OrderId
                                                  AND Status < 3
                                                  AND (
                                                      TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND (Status = 1 OR (Status = 0 AND ISNULL(DiscAmount,0) = 0))), 0)
                                                  ) <= 0.05";
                                            }
                                            else
                                            {
                                                // When discount approvals are NOT required, count all payments (regardless of Status)
                                                // towards the order total for the purpose of marking the order Completed. This
                                                // ensures that any payment method (card, UPI, cash, etc.) that results in the
                                                // order being fully paid will cause the order to be completed immediately.
                                                finalSql = @"UPDATE Orders
                                                SET Status = 3,
                                                    CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                    UpdatedAt = GETDATE()
                                                WHERE Id = @OrderId
                                                  AND Status < 3
                                                  AND (
                                                      TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId), 0)
                                                  ) <= 0.05";

                                            }

                                            using (var finalCompleteCmd = new Microsoft.Data.SqlClient.SqlCommand(finalSql, connection))
                                            {
                                                finalCompleteCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                int rows = finalCompleteCmd.ExecuteNonQuery();

                                                // If no rows were affected, try a slightly more tolerant fallback to handle
                                                // small numeric/rounding differences between server and client math. This
                                                // fallback only runs when the first attempt didn't mark the order completed.
                                                if (rows == 0)
                                                {
                                                    string fallbackSql;
                                                    if (discountApprovalRequired)
                                                    {
                                                        fallbackSql = @"UPDATE Orders
                                                        SET Status = 3,
                                                            CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                            UpdatedAt = GETDATE()
                                                        WHERE Id = @OrderId
                                                          AND Status < 3
                                                          AND (
                                                              TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND (Status = 1 OR (Status = 0 AND ISNULL(DiscAmount,0) = 0))), 0)
                                                          ) <= 0.50";
                                                    }
                                                    else
                                                    {
                                                        fallbackSql = @"UPDATE Orders
                                                        SET Status = 3,
                                                            CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                            UpdatedAt = GETDATE()
                                                        WHERE Id = @OrderId
                                                          AND Status < 3
                                                          AND (
                                                              TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId), 0)
                                                          ) <= 0.50";
                                                    }

                                                    using (var fallbackCmd = new Microsoft.Data.SqlClient.SqlCommand(fallbackSql, connection))
                                                    {
                                                        fallbackCmd.Parameters.AddWithValue("@OrderId", model.OrderId);
                                                        fallbackCmd.ExecuteNonQuery();
                                                    }
                                                }
                                            }
                                        }
                                        catch { /* don't block the happy path if this fails */ }
                                        return RedirectToAction("Index", new { id = model.OrderId });
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("", message);
                                    }
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Failed to process payment.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            // If we get here, something went wrong - repopulate the model
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details again
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        o.OrderNumber, 
                        o.TotalAmount, 
                        (o.TotalAmount - ISNULL(SUM(p.Amount + p.TipAmount + ISNULL(p.RoundoffAdjustmentAmt,0)), 0)) AS RemainingAmount
                    FROM Orders o
                    LEFT JOIN Payments p ON o.Id = p.OrderId AND p.Status = 1 -- Approved payments only
                    WHERE o.Id = @OrderId
                    GROUP BY o.OrderNumber, o.TotalAmount", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", model.OrderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderNumber = reader.GetString(0);
                            model.TotalAmount = reader.GetDecimal(1);
                            model.RemainingAmount = reader.GetDecimal(2);
                        }
                    }
                }
                
                // Get available payment methods
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT Id, Name, DisplayName, RequiresCardInfo
                    FROM PaymentMethods
                    WHERE IsActive = 1
                    ORDER BY DisplayName", connection))
                {
                    model.AvailablePaymentMethods.Clear();
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.AvailablePaymentMethods.Add(new SelectListItem
                            {
                                Value = reader.GetInt32(0).ToString(),
                                Text = reader.GetString(2),
                                Selected = reader.GetInt32(0) == model.PaymentMethodId
                            });
                            
                            if (reader.GetInt32(0) == model.PaymentMethodId)
                            {
                                model.IsCardPayment = reader.GetBoolean(3);
                            }
                        }
                    }
                }
            }
            
            return View(model);
        }
        
        // Void Payment
        public IActionResult VoidPayment(int id)
        {
            var model = new VoidPaymentViewModel
            {
                PaymentId = id
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        p.Id,
                        p.OrderId,
                        o.OrderNumber,
                        p.Amount,
                        p.TipAmount,
                        pm.DisplayName,
                        p.CreatedAt
                    FROM 
                        Payments p
                    INNER JOIN 
                        Orders o ON p.OrderId = o.Id
                    INNER JOIN
                        PaymentMethods pm ON p.PaymentMethodId = pm.Id
                    WHERE 
                        p.Id = @PaymentId", connection))
                {
                    command.Parameters.AddWithValue("@PaymentId", id);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderId = reader.GetInt32(1);
                            model.OrderNumber = reader.GetString(2);
                            model.PaymentAmount = reader.GetDecimal(3);
                            model.TipAmount = reader.GetDecimal(4);
                            model.PaymentMethodDisplay = reader.GetString(5);
                            model.PaymentDate = reader.GetDateTime(6);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult VoidPayment(VoidPaymentViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_VoidPayment", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            
                            command.Parameters.AddWithValue("@PaymentId", model.PaymentId);
                            command.Parameters.AddWithValue("@Reason", model.Reason);
                            command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                            command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                            
                            using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int result = reader.GetInt32(0);
                                    string message = reader.GetString(1);
                                    
                                    if (result > 0)
                                    {
                                        // Recalculate order discount/ tax / total from remaining non-voided payments
                                        try
                                        {
                                            if (!reader.IsClosed) reader.Close();
                                            using (var recalcCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                DECLARE @OrderId INT = @OrderIdParam;
                                                DECLARE @TotalDiscount DECIMAL(18,2) = ISNULL((SELECT SUM(ISNULL(DiscAmount,0)) FROM Payments WHERE OrderId = @OrderId AND Status <> 3), 0);
                                                DECLARE @Subtotal DECIMAL(18,2) = ISNULL((SELECT Subtotal FROM Orders WHERE Id = @OrderId), 0);
                                                DECLARE @Tip DECIMAL(18,2) = ISNULL((SELECT TipAmount FROM Orders WHERE Id = @OrderId), 0);
                                                DECLARE @GSTPerc DECIMAL(10,4) = ISNULL((SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings), 0);
                                                DECLARE @NetSubtotal DECIMAL(18,2) = @Subtotal - @TotalDiscount;
                                                IF @NetSubtotal < 0 SET @NetSubtotal = 0;
                                                DECLARE @NewTax DECIMAL(18,2) = 0;
                                                IF @GSTPerc > 0 SET @NewTax = ROUND(@NetSubtotal * @GSTPerc / 100.0, 2);
                                                DECLARE @NewTotal DECIMAL(18,2) = @NetSubtotal + @NewTax + @Tip;
                                                -- Also persist aggregated roundoff from approved payments so UI shows correct Paid amount
                                                DECLARE @TotalRoundoff DECIMAL(18,2) = ISNULL((SELECT SUM(ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status <> 3), 0);
                                                UPDATE Orders
                                                SET DiscountAmount = @TotalDiscount,
                                                    TaxAmount = @NewTax,
                                                    TotalAmount = @NewTotal,
                                                    RoundoffAdjustmentAmt = @TotalRoundoff,
                                                    UpdatedAt = GETDATE()
                                                WHERE Id = @OrderId;", connection))
                                            {
                                                recalcCmd.Parameters.AddWithValue("@OrderIdParam", model.OrderId);
                                                recalcCmd.ExecuteNonQuery();
                                            }
                                        }
                                        catch
                                        {
                                            // Recalc failure should not block void success; log if logger available
                                            try { _logger?.LogWarning("Failed to recalculate order totals after voiding payment {PaymentId}", model.PaymentId); } catch { }
                                        }

                                        TempData["SuccessMessage"] = "Payment voided successfully.";
                                        return RedirectToAction("Index", new { id = model.OrderId });
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("", message);
                                    }
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Failed to void payment.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            return View(model);
        }
        
        // Approve Payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApprovePayment(int id)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Payments 
                        SET Status = 1, 
                            UpdatedAt = GETDATE(),
                            ProcessedBy = @ProcessedBy,
                            ProcessedByName = @ProcessedByName
                        WHERE Id = @PaymentId AND Status = 0;
                        
                        SELECT @@ROWCOUNT AS RowsAffected, OrderId FROM Payments WHERE Id = @PaymentId;", connection))
                    {
                        command.Parameters.AddWithValue("@PaymentId", id);
                        command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                        command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int rowsAffected = reader.GetInt32("RowsAffected");
                                int orderId = reader.GetInt32("OrderId");
                                
                                if (rowsAffected > 0)
                                {
                                    TempData["SuccessMessage"] = "Payment approved successfully.";

                                    // Determine whether caller was Dashboard so we can redirect there after processing
                                    string returnUrl = Request.Headers["Referer"].ToString();
                                    bool callerWasDashboard = !string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Payment/Dashboard");

                                    // After approval, ensure order status is updated if fully paid
                                    try
                                    {
                                        if (!reader.IsClosed) reader.Close();

                                        using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT o.TotalAmount,
                                                ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 1), 0) AS ApprovedSum,
                                                ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 0), 0) AS PendingSum
                                            FROM Orders o
                                            WHERE o.Id = @OrderId
                                        ", connection))
                                        {
                                            checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                                            using (var r2 = checkCmd.ExecuteReader())
                                            {
                                                if (r2.Read())
                                                {
                                                    decimal orderTotal = r2.IsDBNull(0) ? 0m : r2.GetDecimal(0);
                                                    decimal approvedSum = r2.IsDBNull(1) ? 0m : r2.GetDecimal(1);
                                                    decimal pendingSum = r2.IsDBNull(2) ? 0m : r2.GetDecimal(2);
                                                    _logger?.LogInformation("ApprovePayment: order {OrderId} total={OrderTotal} approvedSum={ApprovedSum} pendingSum={PendingSum}", orderId, orderTotal, approvedSum, pendingSum);

                                                    // If approved payments cover the order (within tolerance), mark completed
                                                    if (approvedSum >= orderTotal - 0.05m)
                                                    {
                                                        using (var completeCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                            UPDATE Orders
                                                            SET Status = 3,
                                                                CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                                UpdatedAt = GETDATE()
                                                            WHERE Id = @OrderId AND Status < 3
                                                        ", connection))
                                                        {
                                                            completeCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                            completeCmd.ExecuteNonQuery();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger?.LogInformation("ApprovePayment: order {OrderId} not completed after approval - shortfall={Shortfall}", orderId, Math.Max(0, (double)(orderTotal - approvedSum)));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.LogWarning(ex, "Error while rechecking order completion for approved payment {PaymentId}", id);
                                    }

                                    // Recalculate and persist aggregate roundoff for the order (approved payments)
                                    try
                                    {
                                        using (var roundoffSumCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT ISNULL(SUM(ISNULL(RoundoffAdjustmentAmt,0)), 0) FROM Payments WHERE OrderId = @OrderId AND Status = 1
                                        ", connection))
                                        {
                                            roundoffSumCmd.Parameters.AddWithValue("@OrderId", orderId);
                                            var sumObj = roundoffSumCmd.ExecuteScalar();
                                            decimal totalRoundoffForOrder = 0m;
                                            if (sumObj != null && sumObj != DBNull.Value)
                                            {
                                                totalRoundoffForOrder = Convert.ToDecimal(sumObj);
                                            }

                                            using (var updateOrderRoundoffCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                UPDATE Orders SET RoundoffAdjustmentAmt = @Roundoff, UpdatedAt = GETDATE() WHERE Id = @OrderId
                                            ", connection))
                                            {
                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@Roundoff", totalRoundoffForOrder);
                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                updateOrderRoundoffCmd.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                    catch { /* ignore roundoff persistence failures */ }

                                    // FINAL SAFETY: consider pending payments but exclude pending discount payments when discount approvals are required
                                    try
                                    {
                                        bool discountApprovalRequiredLocal = false;
                                        try
                                        {
                                            using (var settingCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 1 IsDiscountApprovalRequired FROM dbo.RestaurantSettings", connection))
                                            {
                                                var settingObj = settingCmd.ExecuteScalar();
                                                if (settingObj != null && settingObj != DBNull.Value)
                                                    discountApprovalRequiredLocal = Convert.ToBoolean(settingObj);
                                            }
                                        }
                                        catch { /* ignore */ }

                                        string finalSqlLocal;
                                        if (discountApprovalRequiredLocal)
                                        {
                                            finalSqlLocal = @"UPDATE Orders
                                            SET Status = 3,
                                                CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                UpdatedAt = GETDATE()
                                            WHERE Id = @OrderId
                                              AND Status < 3
                                              AND (
                                                  TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND (Status = 1 OR (Status = 0 AND ISNULL(DiscAmount,0) = 0))), 0)
                                              ) <= 0.05";
                                        }
                                        else
                                        {
                                            finalSqlLocal = @"UPDATE Orders
                                            SET Status = 3,
                                                CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                UpdatedAt = GETDATE()
                                            WHERE Id = @OrderId
                                              AND Status < 3
                                              AND (
                                                  TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status IN (0,1)), 0)
                                              ) <= 0.05";

                                        }

                                        using (var finalCompleteCmdLocal = new Microsoft.Data.SqlClient.SqlCommand(finalSqlLocal, connection))
                                        {
                                            finalCompleteCmdLocal.Parameters.AddWithValue("@OrderId", orderId);
                                            finalCompleteCmdLocal.ExecuteNonQuery();
                                        }
                                    }
                                    catch { /* ignore */ }

                                    // Redirect back to Dashboard if the approve action was invoked from there, otherwise show the order payment index
                                    if (callerWasDashboard)
                                    {
                                        return RedirectToAction("Dashboard");
                                    }
                                    return RedirectToAction("Index", new { id = orderId });
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Payment not found or already processed.";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving payment {PaymentId}", id);
                TempData["ErrorMessage"] = "An error occurred while approving the payment.";
            }
            
            return RedirectToAction("Dashboard");
        }
        
        // Reject Payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectPayment(int id, string reason = null)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Payments 
                        SET Status = 2, 
                            UpdatedAt = GETDATE(),
                            ProcessedBy = @ProcessedBy,
                            ProcessedByName = @ProcessedByName,
                            Notes = CASE WHEN @Reason IS NOT NULL THEN 
                                CASE WHEN Notes IS NOT NULL THEN Notes + ' | Rejected: ' + @Reason 
                                ELSE 'Rejected: ' + @Reason END 
                                ELSE Notes END
                        WHERE Id = @PaymentId AND Status = 0;
                        
                        SELECT @@ROWCOUNT AS RowsAffected, OrderId FROM Payments WHERE Id = @PaymentId;", connection))
                    {
                        command.Parameters.AddWithValue("@PaymentId", id);
                        command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                        command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                        command.Parameters.AddWithValue("@Reason", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : reason);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int rowsAffected = reader.GetInt32("RowsAffected");
                                int orderId = reader.GetInt32("OrderId");
                                
                                if (rowsAffected > 0)
                                {
                                    TempData["SuccessMessage"] = "Payment rejected successfully.";

                                    // Check if this was from dashboard
                                    string returnUrl = Request.Headers["Referer"].ToString();
                                    if (returnUrl.Contains("/Payment/Dashboard"))
                                    {
                                        return RedirectToAction("Dashboard");
                                    }
                                    return RedirectToAction("Index", new { id = orderId });
                                }
                                else
                                {
                                    TempData["ErrorMessage"] = "Payment not found or already processed.";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting payment {PaymentId}", id);
                TempData["ErrorMessage"] = "An error occurred while rejecting the payment.";
            }
            
            return RedirectToAction("Dashboard");
        }
        
        // Approve Payment AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApprovePaymentAjax(int id)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Payments 
                        SET Status = 1, 
                            UpdatedAt = GETDATE(),
                            ProcessedBy = @ProcessedBy,
                            ProcessedByName = @ProcessedByName
                        WHERE Id = @PaymentId AND Status = 0;
                        
                        SELECT @@ROWCOUNT AS RowsAffected, OrderId, 
                               (SELECT OrderNumber FROM Orders WHERE Id = OrderId) AS OrderNumber
                        FROM Payments WHERE Id = @PaymentId;", connection))
                    {
                        command.Parameters.AddWithValue("@PaymentId", id);
                        command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                        command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int rowsAffected = reader.GetInt32("RowsAffected");
                                int orderId = reader.GetInt32("OrderId");
                                string orderNumber = reader["OrderNumber"].ToString();
                                
                                if (rowsAffected > 0)
                                {
                                    // After approval, ensure order status is updated if fully paid
                                    try
                                    {
                                        if (!reader.IsClosed) reader.Close();

                                        using (var checkCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT o.TotalAmount,
                                                ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 1), 0) AS ApprovedSum,
                                                ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status = 0), 0) AS PendingSum
                                            FROM Orders o
                                            WHERE o.Id = @OrderId
                                        ", connection))
                                        {
                                            checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                                            using (var r2 = checkCmd.ExecuteReader())
                                            {
                                                if (r2.Read())
                                                {
                                                    decimal orderTotal = r2.IsDBNull(0) ? 0m : r2.GetDecimal(0);
                                                    decimal approvedSum = r2.IsDBNull(1) ? 0m : r2.GetDecimal(1);
                                                    decimal pendingSum = r2.IsDBNull(2) ? 0m : r2.GetDecimal(2);
                                                    _logger?.LogInformation("ApprovePaymentAjax: order {OrderId} total={OrderTotal} approvedSum={ApprovedSum} pendingSum={PendingSum}", orderId, orderTotal, approvedSum, pendingSum);

                                                    if (approvedSum >= orderTotal - 0.05m)
                                                    {
                                                        using (var completeCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                            UPDATE Orders
                                                            SET Status = 3,
                                                                CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                                UpdatedAt = GETDATE()
                                                            WHERE Id = @OrderId AND Status < 3
                                                        ", connection))
                                                        {
                                                            completeCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                            completeCmd.ExecuteNonQuery();
                                                        }
                                                    }
                                                    else
                                                    {
                                                        _logger?.LogInformation("ApprovePaymentAjax: order {OrderId} not completed after approval - shortfall={Shortfall}", orderId, Math.Max(0, (double)(orderTotal - approvedSum)));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger?.LogWarning(ex, "Error while rechecking order completion for approved payment {PaymentId}", id);
                                    }

                                    // Recalculate aggregate roundoff for the order (approved payments)
                                    try
                                    {
                                        using (var roundoffSumCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                            SELECT ISNULL(SUM(ISNULL(RoundoffAdjustmentAmt,0)), 0) FROM Payments WHERE OrderId = @OrderId AND Status = 1
                                        ", connection))
                                        {
                                            roundoffSumCmd.Parameters.AddWithValue("@OrderId", orderId);
                                            var sumObj = roundoffSumCmd.ExecuteScalar();
                                            decimal totalRoundoffForOrder = 0m;
                                            if (sumObj != null && sumObj != DBNull.Value)
                                            {
                                                totalRoundoffForOrder = Convert.ToDecimal(sumObj);
                                            }

                                            using (var updateOrderRoundoffCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                                                UPDATE Orders SET RoundoffAdjustmentAmt = @Roundoff, UpdatedAt = GETDATE() WHERE Id = @OrderId
                                            ", connection))
                                            {
                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@Roundoff", totalRoundoffForOrder);
                                                updateOrderRoundoffCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                updateOrderRoundoffCmd.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                    catch { /* ignore */ }

                                    // FINAL SAFETY: consider pending + approved payments when marking complete
                                    try
                                    {
                                            // Read discount approval setting so we don't count pending discount payments when approvals are required
                                            bool discountApprovalRequired = false;
                                            try
                                            {
                                                using (var settingCmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP 1 IsDiscountApprovalRequired FROM dbo.RestaurantSettings", connection))
                                                {
                                                    var settingObj = settingCmd.ExecuteScalar();
                                                    if (settingObj != null && settingObj != DBNull.Value)
                                                        discountApprovalRequired = Convert.ToBoolean(settingObj);
                                                }
                                            }
                                            catch { /* ignore setting read errors, default to false */ }

                                            string finalSql;
                                            if (discountApprovalRequired)
                                            {
                                                finalSql = @"UPDATE Orders
                                                SET Status = 3,
                                                    CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                    UpdatedAt = GETDATE()
                                                WHERE Id = @OrderId
                                                  AND Status < 3
                                                  AND (
                                                      TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND (Status = 1 OR (Status = 0 AND ISNULL(DiscAmount,0) = 0))), 0)
                                                  ) <= 0.05";
                                            }
                                            else
                                            {
                                                finalSql = @"UPDATE Orders
                                                SET Status = 3,
                                                    CompletedAt = CASE WHEN Status < 3 AND CompletedAt IS NULL THEN GETDATE() ELSE CompletedAt END,
                                                    UpdatedAt = GETDATE()
                                                WHERE Id = @OrderId
                                                  AND Status < 3
                                                  AND (
                                                      TotalAmount - ISNULL((SELECT SUM(Amount + TipAmount + ISNULL(RoundoffAdjustmentAmt,0)) FROM Payments WHERE OrderId = @OrderId AND Status IN (0,1)), 0)
                                                  ) <= 0.05";

                                            }

                                            using (var finalCompleteCmd = new Microsoft.Data.SqlClient.SqlCommand(finalSql, connection))
                                            {
                                                finalCompleteCmd.Parameters.AddWithValue("@OrderId", orderId);
                                                finalCompleteCmd.ExecuteNonQuery();
                                            }
                                    }
                                    catch { /* ignore */ }

                                    return Json(new { 
                                        success = true, 
                                        message = $"Payment for order #{orderNumber} approved successfully.",
                                        orderId = orderId,
                                        orderNumber = orderNumber
                                    });
                                }
                                else
                                {
                                    return Json(new { 
                                        success = false, 
                                        message = "Payment not found or already processed." 
                                    });
                                }
                            }
                            else
                            {
                                return Json(new { 
                                    success = false, 
                                    message = "Payment not found." 
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving payment {PaymentId}", id);
                return Json(new { 
                    success = false, 
                    message = "An error occurred while approving the payment." 
                });
            }
        }
        
        // Reject Payment AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectPaymentAjax(int id, string reason = null)
        {
            try
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                        UPDATE Payments 
                        SET Status = 2, 
                            UpdatedAt = GETDATE(),
                            ProcessedBy = @ProcessedBy,
                            ProcessedByName = @ProcessedByName,
                            Notes = CASE WHEN @Reason IS NOT NULL THEN 
                                CASE WHEN Notes IS NOT NULL THEN Notes + ' | Rejected: ' + @Reason 
                                ELSE 'Rejected: ' + @Reason END 
                                ELSE Notes END
                        WHERE Id = @PaymentId AND Status = 0;
                        
                        SELECT @@ROWCOUNT AS RowsAffected, OrderId, 
                               (SELECT OrderNumber FROM Orders WHERE Id = OrderId) AS OrderNumber
                        FROM Payments WHERE Id = @PaymentId;", connection))
                    {
                        command.Parameters.AddWithValue("@PaymentId", id);
                        command.Parameters.AddWithValue("@ProcessedBy", GetCurrentUserId());
                        command.Parameters.AddWithValue("@ProcessedByName", GetCurrentUserName());
                        command.Parameters.AddWithValue("@Reason", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : reason);
                        
                        using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int rowsAffected = reader.GetInt32("RowsAffected");
                                int orderId = reader.GetInt32("OrderId");
                                string orderNumber = reader["OrderNumber"].ToString();
                                
                                if (rowsAffected > 0)
                                {
                                    return Json(new { 
                                        success = true, 
                                        message = $"Payment for order #{orderNumber} rejected successfully.",
                                        orderId = orderId,
                                        orderNumber = orderNumber
                                    });
                                }
                                else
                                {
                                    return Json(new { 
                                        success = false, 
                                        message = "Payment not found or already processed." 
                                    });
                                }
                            }
                            else
                            {
                                return Json(new { 
                                    success = false, 
                                    message = "Payment not found." 
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting payment {PaymentId}", id);
                return Json(new { 
                    success = false, 
                    message = "An error occurred while rejecting the payment." 
                });
            }
        }
        
        // Split Bill
        public IActionResult SplitBill(int orderId)
        {
            var model = new CreateSplitBillViewModel
            {
                OrderId = orderId
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        o.OrderNumber, 
                        o.Subtotal,
                        o.TaxAmount,
                        o.TotalAmount
                    FROM Orders o
                    WHERE o.Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderNumber = reader.GetString(0);
                            model.Subtotal = reader.GetDecimal(1);
                            model.TaxAmount = reader.GetDecimal(2);
                            model.TotalAmount = reader.GetDecimal(3);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
                
                // Get order items that are not fully split yet
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        oi.Id,
                        mi.Name,
                        oi.Quantity,
                        oi.UnitPrice,
                        oi.Subtotal,
                        -- Calculate already split quantities
                        ISNULL((
                            SELECT SUM(sbi.Quantity)
                            FROM SplitBillItems sbi
                            INNER JOIN SplitBills sb ON sbi.SplitBillId = sb.Id
                            WHERE sbi.OrderItemId = oi.Id AND sb.Status != 2 -- Not voided
                        ), 0) AS SplitQuantity
                    FROM 
                        OrderItems oi
                    INNER JOIN 
                        MenuItems mi ON oi.MenuItemId = mi.Id
                    WHERE 
                        oi.OrderId = @OrderId
                        AND oi.Status != 5 -- Not cancelled
                    ORDER BY
                        oi.Id", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string name = reader.GetString(1);
                            int quantity = reader.GetInt32(2);
                            decimal unitPrice = reader.GetDecimal(3);
                            decimal subtotal = reader.GetDecimal(4);
                            int splitQuantity = reader.GetInt32(5);
                            
                            int availableQuantity = quantity - splitQuantity;
                            
                            if (availableQuantity > 0)
                            {
                                model.AvailableItems.Add(new SplitBillItemViewModel
                                {
                                    OrderItemId = id,
                                    Name = name,
                                    Quantity = quantity,
                                    AvailableQuantity = availableQuantity,
                                    UnitPrice = unitPrice,
                                    Subtotal = subtotal,
                                    TaxAmount = subtotal * (model.TaxAmount / model.Subtotal) // Proportional tax
                                });
                            }
                        }
                    }
                }
            }
            
            return View(model);
        }
        
        [HttpPostAttribute]
        [ValidateAntiForgeryTokenAttribute]
        public IActionResult SplitBill(CreateSplitBillViewModel model, int[] selectedItems, int[] itemQuantities)
        {
            if (ModelState.IsValid)
            {
                if (selectedItems == null || selectedItems.Length == 0)
                {
                    ModelState.AddModelError("", "Please select at least one item for the split bill.");
                    return View(model);
                }
                
                try
                {
                    // Build items string for stored procedure
                    string itemsString = "";
                    
                    for (int i = 0; i < selectedItems.Length; i++)
                    {
                        int orderItemId = selectedItems[i];
                        int quantity = itemQuantities[i];
                        
                        if (quantity <= 0)
                        {
                            continue; // Skip items with zero quantity
                        }
                        
                        // Get price from model's available items
                        var item = model.AvailableItems.FirstOrDefault(x => x.OrderItemId == orderItemId);
                        
                        if (item != null)
                        {
                            decimal amount = item.UnitPrice * quantity;
                            
                            itemsString += $"{orderItemId},{quantity},{amount};";
                        }
                    }
                    
                    // Remove trailing semicolon
                    if (itemsString.EndsWith(";"))
                    {
                        itemsString = itemsString.Substring(0, itemsString.Length - 1);
                    }
                    
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                    {
                        connection.Open();
                        
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_CreateSplitBill", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            
                            command.Parameters.AddWithValue("@OrderId", model.OrderId);
                            command.Parameters.AddWithValue("@Items", itemsString);
                            command.Parameters.AddWithValue("@Notes", string.IsNullOrEmpty(model.Notes) ? (object)DBNull.Value : model.Notes);
                            command.Parameters.AddWithValue("@CreatedBy", GetCurrentUserId());
                            command.Parameters.AddWithValue("@CreatedByName", GetCurrentUserName());
                            
                            using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int splitBillId = reader.GetInt32(0);
                                    decimal amount = reader.GetDecimal(1);
                                    decimal taxAmount = reader.GetDecimal(2);
                                    decimal totalAmount = reader.GetDecimal(3);
                                    string message = reader.GetString(4);
                                    
                                    if (splitBillId > 0)
                                    {
                                        TempData["SuccessMessage"] = $"Split bill created successfully for ${totalAmount:F2}.";
                                        return RedirectToAction("Index", new { id = model.OrderId });
                                    }
                                    else
                                    {
                                        ModelState.AddModelError("", message);
                                    }
                                }
                                else
                                {
                                    ModelState.AddModelError("", "Failed to create split bill.");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred: {ex.Message}");
                }
            }
            
            // If we get here, repopulate the model
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                // Get order details
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        o.OrderNumber, 
                        o.Subtotal,
                        o.TaxAmount,
                        o.TotalAmount
                    FROM Orders o
                    WHERE o.Id = @OrderId", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", model.OrderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.OrderNumber = reader.GetString(0);
                            model.Subtotal = reader.GetDecimal(1);
                            model.TaxAmount = reader.GetDecimal(2);
                            model.TotalAmount = reader.GetDecimal(3);
                        }
                    }
                }
                
                // Get order items that are not fully split yet
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        oi.Id,
                        mi.Name,
                        oi.Quantity,
                        oi.UnitPrice,
                        oi.Subtotal,
                        -- Calculate already split quantities
                        ISNULL((
                            SELECT SUM(sbi.Quantity)
                            FROM SplitBillItems sbi
                            INNER JOIN SplitBills sb ON sbi.SplitBillId = sb.Id
                            WHERE sbi.OrderItemId = oi.Id AND sb.Status != 2 -- Not voided
                        ), 0) AS SplitQuantity
                    FROM 
                        OrderItems oi
                    INNER JOIN 
                        MenuItems mi ON oi.MenuItemId = mi.Id
                    WHERE 
                        oi.OrderId = @OrderId
                        AND oi.Status != 5 -- Not cancelled
                    ORDER BY
                        oi.Id", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", model.OrderId);
                    model.AvailableItems.Clear();
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string name = reader.GetString(1);
                            int quantity = reader.GetInt32(2);
                            decimal unitPrice = reader.GetDecimal(3);
                            decimal subtotal = reader.GetDecimal(4);
                            int splitQuantity = reader.GetInt32(5);
                            
                            int availableQuantity = quantity - splitQuantity;
                            
                            if (availableQuantity > 0)
                            {
                                var item = new SplitBillItemViewModel
                                {
                                    OrderItemId = id,
                                    Name = name,
                                    Quantity = quantity,
                                    AvailableQuantity = availableQuantity,
                                    UnitPrice = unitPrice,
                                    Subtotal = subtotal,
                                    TaxAmount = subtotal * (model.TaxAmount / model.Subtotal) // Proportional tax
                                };
                                
                                // Set selected state if item was selected in form
                                if (selectedItems != null && selectedItems.Contains(id))
                                {
                                    int index = Array.IndexOf(selectedItems, id);
                                    item.IsSelected = true;
                                    item.SelectedQuantity = itemQuantities[index];
                                }
                                
                                model.AvailableItems.Add(item);
                            }
                        }
                    }
                }
            }
            
            return View(model);
        }

        // Payment Dashboard
        public IActionResult Dashboard(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = new PaymentDashboardViewModel
            {
                FromDate = fromDate ?? DateTime.Today,
                ToDate = toDate ?? DateTime.Today
            };

            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                // Get today's analytics
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        ISNULL(SUM(p.Amount), 0) AS TotalPayments,
                        ISNULL(SUM(p.TipAmount), 0) AS TotalTips
                    FROM Payments p
                    WHERE p.Status = 1 -- Approved payments only
                        AND CAST(p.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TodayTotalPayments = reader.GetDecimal(0);
                            model.TodayTotalTips = reader.GetDecimal(1);
                        }
                    }
                }

                // Calculate today's GST from actual processed payments (use CGST + SGST when available)
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT ISNULL(SUM(ISNULL(p.CGSTAmount,0) + ISNULL(p.SGSTAmount,0)), 0) AS TotalGST
                    FROM Payments p
                    WHERE p.Status = 1 -- Approved payments only
                        AND CAST(p.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            model.TodayTotalGST = Math.Max(0, reader.GetDecimal(0)); // Ensure GST is not negative
                        }
                    }
                }

                // Get today's payment method breakdown
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        pm.Id AS PaymentMethodId,
                        pm.Name AS PaymentMethodName,
                        pm.DisplayName AS PaymentMethodDisplayName,
                        ISNULL(SUM(p.Amount), 0) AS TotalAmount,
                        ISNULL(SUM(ISNULL(p.CGSTAmount,0) + ISNULL(p.SGSTAmount,0)), 0) AS TotalGST,
                        COUNT(p.Id) AS TransactionCount
                    FROM PaymentMethods pm
                    LEFT JOIN Payments p ON pm.Id = p.PaymentMethodId 
                        AND p.Status = 1 -- Approved payments only
                        AND CAST(p.CreatedAt AS DATE) = CAST(GETDATE() AS DATE)
                    WHERE pm.IsActive = 1
                    GROUP BY pm.Id, pm.Name, pm.DisplayName
                    ORDER BY TotalAmount DESC", connection))
                {
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.PaymentMethodBreakdowns.Add(new PaymentMethodBreakdown
                            {
                                PaymentMethodId = reader.GetInt32("PaymentMethodId"),
                                PaymentMethodName = reader.GetString("PaymentMethodName"),
                                PaymentMethodDisplayName = reader.GetString("PaymentMethodDisplayName"),
                                TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                                TotalGST = Convert.ToDecimal(reader["TotalGST"]),
                                TransactionCount = reader.GetInt32("TransactionCount")
                            });
                        }
                    }
                }

                // Get payment history - showing actual processed payments with their real amounts
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        o.Id AS OrderId,
                        o.OrderNumber,
                        ISNULL(tt.TableName, 'Takeout/Delivery') AS TableName,
                        -- Total payable is the sum of all payment amounts for this order (includes GST as processed)
                        (SELECT ISNULL(SUM(p2.Amount), 0) FROM Payments p2 WHERE p2.OrderId = o.Id AND p2.Status = 1) AS TotalPayable,
                        ISNULL(SUM(p.Amount), 0) AS TotalPaid,
                        -- Due amount is 0 since we're only showing orders with payments
                        0 AS DueAmount,
                        -- Sum of GST amounts from all approved payments
                        ISNULL(SUM(p.GSTAmount), 0) AS GSTAmount,
                        MAX(p.CreatedAt) AS PaymentDate,
                        o.Status AS OrderStatus,
                        CASE o.Status 
                            WHEN 0 THEN 'Open'
                            WHEN 1 THEN 'In Progress'
                            WHEN 2 THEN 'Ready'
                            WHEN 3 THEN 'Completed'
                            WHEN 4 THEN 'Cancelled'
                            ELSE 'Unknown'
                        END AS OrderStatusDisplay
                    FROM Orders o
                    LEFT JOIN TableTurnovers tto ON o.TableTurnoverId = tto.Id
                    LEFT JOIN Tables tt ON tto.TableId = tt.Id
                    INNER JOIN Payments p ON o.Id = p.OrderId AND p.Status = 1 -- Only orders with approved payments
                    WHERE CAST(p.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
                    GROUP BY o.Id, o.OrderNumber, tt.TableName, o.Status
                    ORDER BY MAX(p.CreatedAt) DESC", connection))
                {
                    command.Parameters.AddWithValue("@FromDate", model.FromDate.Date);
                    command.Parameters.AddWithValue("@ToDate", model.ToDate.Date);

                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.PaymentHistory.Add(new PaymentHistoryItem
                            {
                                OrderId = reader.GetInt32("OrderId"),
                                OrderNumber = reader.GetString("OrderNumber"),
                                TableName = GetMergedTableDisplayName((int)reader["OrderId"], reader.GetString("TableName")),
                                TotalPayable = Convert.ToDecimal(reader["TotalPayable"]),
                                TotalPaid = Convert.ToDecimal(reader["TotalPaid"]),
                                DueAmount = Convert.ToDecimal(reader["DueAmount"]),
                                GSTAmount = Convert.ToDecimal(reader["GSTAmount"]),
                                PaymentDate = reader.GetDateTime("PaymentDate"),
                                OrderStatus = reader.GetInt32("OrderStatus"),
                                OrderStatusDisplay = reader.GetString("OrderStatusDisplay")
                            });
                        }
                    }
                }

                    // Populate pending payments (awaiting approval) within the selected date range
                    try
                    {
                        using (var pendingCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                            SELECT 
                                p.Id AS PaymentId,
                                p.OrderId,
                                o.OrderNumber,
                                ISNULL(tt.TableName, 'Takeout/Delivery') AS TableName,
                                pm.Name AS PaymentMethodName,
                                pm.DisplayName AS PaymentMethodDisplay,
                                ISNULL(p.Amount,0) AS Amount,
                                ISNULL(p.TipAmount,0) AS TipAmount,
                                ISNULL(p.DiscAmount,0) AS DiscAmount,
                                (ISNULL(p.Amount,0) + ISNULL(p.DiscAmount,0)) AS OriginalAmount,
                                p.CreatedAt,
                                p.ProcessedByName,
                                p.ReferenceNumber,
                                p.LastFourDigits,
                                p.CardType,
                                p.Notes
                            FROM Payments p
                            INNER JOIN Orders o ON p.OrderId = o.Id
                            LEFT JOIN TableTurnovers tto ON o.TableTurnoverId = tto.Id
                            LEFT JOIN Tables tt ON tto.TableId = tt.Id
                            LEFT JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
                            WHERE p.Status = 0 -- Pending
                              AND CAST(p.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
                            ORDER BY p.CreatedAt DESC", connection))
                        {
                            pendingCmd.Parameters.AddWithValue("@FromDate", model.FromDate.Date);
                            pendingCmd.Parameters.AddWithValue("@ToDate", model.ToDate.Date);

                            using (var rdr = pendingCmd.ExecuteReader())
                            {
                                while (rdr.Read())
                                {
                                    var pending = new PendingPaymentItem
                                    {
                                        PaymentId = rdr.GetInt32(rdr.GetOrdinal("PaymentId")),
                                        OrderId = rdr.GetInt32(rdr.GetOrdinal("OrderId")),
                                        OrderNumber = rdr.IsDBNull(rdr.GetOrdinal("OrderNumber")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("OrderNumber")),
                                        TableName = rdr.IsDBNull(rdr.GetOrdinal("TableName")) ? "" : rdr.GetString(rdr.GetOrdinal("TableName")),
                                        PaymentMethodName = rdr.IsDBNull(rdr.GetOrdinal("PaymentMethodName")) ? "" : rdr.GetString(rdr.GetOrdinal("PaymentMethodName")),
                                        PaymentMethodDisplay = rdr.IsDBNull(rdr.GetOrdinal("PaymentMethodDisplay")) ? "" : rdr.GetString(rdr.GetOrdinal("PaymentMethodDisplay")),
                                        Amount = rdr.IsDBNull(rdr.GetOrdinal("Amount")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("Amount")),
                                        TipAmount = rdr.IsDBNull(rdr.GetOrdinal("TipAmount")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("TipAmount")),
                                        DiscountAmount = rdr.IsDBNull(rdr.GetOrdinal("DiscAmount")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("DiscAmount")),
                                        OriginalAmount = rdr.IsDBNull(rdr.GetOrdinal("OriginalAmount")) ? 0m : rdr.GetDecimal(rdr.GetOrdinal("OriginalAmount")),
                                        CreatedAt = rdr.IsDBNull(rdr.GetOrdinal("CreatedAt")) ? DateTime.MinValue : rdr.GetDateTime(rdr.GetOrdinal("CreatedAt")),
                                        ProcessedByName = rdr.IsDBNull(rdr.GetOrdinal("ProcessedByName")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("ProcessedByName")),
                                        ReferenceNumber = rdr.IsDBNull(rdr.GetOrdinal("ReferenceNumber")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("ReferenceNumber")),
                                        LastFourDigits = rdr.IsDBNull(rdr.GetOrdinal("LastFourDigits")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("LastFourDigits")),
                                        CardType = rdr.IsDBNull(rdr.GetOrdinal("CardType")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("CardType")),
                                        Notes = rdr.IsDBNull(rdr.GetOrdinal("Notes")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Notes"))
                                    };

                                    model.PendingPayments.Add(pending);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error loading pending payments for dashboard");
                    }

            }

            return View(model);
        }
        
        // Helper methods
        private PaymentViewModel GetPaymentViewModel(int orderId)
        {
            var model = new PaymentViewModel
            {
                OrderId = orderId
            };
            
            using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("usp_GetOrderPaymentInfo", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        // Helper to get ordinal safely
                        int OrdinalOrMinus(SqlDataReader r, string name)
                        {
                            try { return r.GetOrdinal(name); } catch (IndexOutOfRangeException) { return -1; }
                        }

                        // First result set: Order details (use ordinals to be resilient to schema changes)
                        if (reader.Read())
                        {
                            int ordOrderNumber = OrdinalOrMinus(reader, "OrderNumber");
                            int ordSubtotal = OrdinalOrMinus(reader, "Subtotal");
                            int ordTaxAmount = OrdinalOrMinus(reader, "TaxAmount");
                            int ordTipAmount = OrdinalOrMinus(reader, "TipAmount");
                            int ordDiscountAmount = OrdinalOrMinus(reader, "DiscountAmount");
                            int ordTotalAmount = OrdinalOrMinus(reader, "TotalAmount");
                            int ordPaidAmount = OrdinalOrMinus(reader, "PaidAmount");
                            int ordRemainingAmount = OrdinalOrMinus(reader, "RemainingAmount");
                            int ordTableName = OrdinalOrMinus(reader, "TableName");
                            int ordStatus = OrdinalOrMinus(reader, "Status");

                            model.OrderNumber = (ordOrderNumber >= 0 && !reader.IsDBNull(ordOrderNumber)) ? reader.GetString(ordOrderNumber) : string.Empty;
                            model.Subtotal = (ordSubtotal >= 0 && !reader.IsDBNull(ordSubtotal)) ? reader.GetDecimal(ordSubtotal) : 0m;
                            model.TaxAmount = (ordTaxAmount >= 0 && !reader.IsDBNull(ordTaxAmount)) ? reader.GetDecimal(ordTaxAmount) : 0m;
                            model.TipAmount = (ordTipAmount >= 0 && !reader.IsDBNull(ordTipAmount)) ? reader.GetDecimal(ordTipAmount) : 0m;
                            model.DiscountAmount = (ordDiscountAmount >= 0 && !reader.IsDBNull(ordDiscountAmount)) ? reader.GetDecimal(ordDiscountAmount) : 0m;
                            model.TotalAmount = (ordTotalAmount >= 0 && !reader.IsDBNull(ordTotalAmount)) ? reader.GetDecimal(ordTotalAmount) : 0m;
                            model.PaidAmount = (ordPaidAmount >= 0 && !reader.IsDBNull(ordPaidAmount)) ? reader.GetDecimal(ordPaidAmount) : 0m;
                            model.RemainingAmount = (ordRemainingAmount >= 0 && !reader.IsDBNull(ordRemainingAmount)) ? reader.GetDecimal(ordRemainingAmount) : 0m;
                            model.TableName = (ordTableName >= 0 && !reader.IsDBNull(ordTableName)) ? reader.GetString(ordTableName) : string.Empty;
                            // Override with merged table names if available
                            model.TableName = GetMergedTableDisplayName(orderId, model.TableName);
                            model.OrderStatus = (ordStatus >= 0 && !reader.IsDBNull(ordStatus)) ? reader.GetInt32(ordStatus) : 0;
                            model.OrderStatusDisplay = model.OrderStatus switch
                            {
                                0 => "Open",
                                1 => "In Progress",
                                2 => "Ready",
                                3 => "Completed",
                                4 => "Cancelled",
                                _ => "Unknown"
                            };
                        }
                        else
                        {
                            return null; // Order not found
                        }
                        
                        // Move to next result set: Order items
                        reader.NextResult();
                        
                        while (reader.Read())
                        {
                            model.OrderItems.Add(new OrderItemViewModel
                            {
                                Id = reader.GetInt32(0),
                                MenuItemId = reader.GetInt32(1),
                                MenuItemName = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                UnitPrice = reader.GetDecimal(4),
                                Subtotal = reader.GetDecimal(5)
                            });
                        }
                        
                        // Move to next result set: Payments
                        reader.NextResult();
                        
                        // Variables to store GST information from the most recent payment
                        decimal totalGSTFromPayments = 0m;
                        decimal totalCGSTFromPayments = 0m;
                        decimal totalSGSTFromPayments = 0m;
                        decimal gstPercentageFromPayments = 5.0m; // Default fallback
                        
                        // compute ordinals for commonly expected columns (if present)
                        int ordId = OrdinalOrMinus(reader, "Id");
                        int ordPaymentMethodId = OrdinalOrMinus(reader, "PaymentMethodId");
                        int ordPaymentMethodName = OrdinalOrMinus(reader, "PaymentMethod");
                        if (ordPaymentMethodName == -1) ordPaymentMethodName = OrdinalOrMinus(reader, "PaymentMethodName");
                        int ordPaymentMethodDisplay = OrdinalOrMinus(reader, "PaymentMethodDisplay");
                        if (ordPaymentMethodDisplay == -1) ordPaymentMethodDisplay = OrdinalOrMinus(reader, "PaymentMethodDisplayName");
                        int ordAmount = OrdinalOrMinus(reader, "Amount");
                        int ordTip = OrdinalOrMinus(reader, "TipAmount");
                        int ordPaymentStatus = OrdinalOrMinus(reader, "Status");
                        int ordReference = OrdinalOrMinus(reader, "ReferenceNumber");
                        int ordLastFour = OrdinalOrMinus(reader, "LastFourDigits");
                        int ordCardType = OrdinalOrMinus(reader, "CardType");
                        int ordAuthCode = OrdinalOrMinus(reader, "AuthorizationCode");
                        int ordNotes = OrdinalOrMinus(reader, "Notes");
                        int ordProcessedByName = OrdinalOrMinus(reader, "ProcessedByName");
                        int ordCreatedAt = OrdinalOrMinus(reader, "CreatedAt");

                        int ordGSTAmount = OrdinalOrMinus(reader, "GSTAmount");
                        int ordCGSTAmount = OrdinalOrMinus(reader, "CGSTAmount");
                        int ordSGSTAmount = OrdinalOrMinus(reader, "SGSTAmount");
                        int ordDiscAmount = OrdinalOrMinus(reader, "DiscAmount");
                        int ordGSTPerc = OrdinalOrMinus(reader, "GST_Perc");
                        int ordCGSTPerc = OrdinalOrMinus(reader, "CGST_Perc");
                        int ordSGSTPerc = OrdinalOrMinus(reader, "SGST_Perc");
                        int ordAmountExcl = OrdinalOrMinus(reader, "Amount_ExclGST");
                        int ordRoundoff = OrdinalOrMinus(reader, "RoundoffAdjustmentAmt");

                        while (reader.Read())
                        {
                            var payment = new Payment();

                            if (ordId >= 0 && !reader.IsDBNull(ordId)) payment.Id = reader.GetInt32(ordId);
                            if (ordPaymentMethodId >= 0 && !reader.IsDBNull(ordPaymentMethodId)) payment.PaymentMethodId = reader.GetInt32(ordPaymentMethodId);
                            if (ordPaymentMethodName >= 0 && !reader.IsDBNull(ordPaymentMethodName)) payment.PaymentMethodName = reader.GetString(ordPaymentMethodName);
                            if (ordPaymentMethodDisplay >= 0 && !reader.IsDBNull(ordPaymentMethodDisplay)) payment.PaymentMethodDisplay = reader.GetString(ordPaymentMethodDisplay);
                            if (ordAmount >= 0 && !reader.IsDBNull(ordAmount)) payment.Amount = reader.GetDecimal(ordAmount);
                            if (ordTip >= 0 && !reader.IsDBNull(ordTip)) payment.TipAmount = reader.GetDecimal(ordTip);
                            if (ordPaymentStatus >= 0 && !reader.IsDBNull(ordPaymentStatus)) payment.Status = reader.GetInt32(ordPaymentStatus);
                            if (ordReference >= 0 && !reader.IsDBNull(ordReference)) payment.ReferenceNumber = reader.GetString(ordReference);
                            if (ordLastFour >= 0 && !reader.IsDBNull(ordLastFour)) payment.LastFourDigits = reader.GetString(ordLastFour);
                            if (ordCardType >= 0 && !reader.IsDBNull(ordCardType)) payment.CardType = reader.GetString(ordCardType);
                            if (ordAuthCode >= 0 && !reader.IsDBNull(ordAuthCode)) payment.AuthorizationCode = reader.GetString(ordAuthCode);
                            if (ordNotes >= 0 && !reader.IsDBNull(ordNotes)) payment.Notes = reader.GetString(ordNotes);
                            if (ordProcessedByName >= 0 && !reader.IsDBNull(ordProcessedByName)) payment.ProcessedByName = reader.GetString(ordProcessedByName);
                            if (ordCreatedAt >= 0 && !reader.IsDBNull(ordCreatedAt)) payment.CreatedAt = reader.GetDateTime(ordCreatedAt);

                            // GST fields (optional)
                            if (ordGSTAmount >= 0 && !reader.IsDBNull(ordGSTAmount)) payment.GSTAmount = reader.GetDecimal(ordGSTAmount);
                            if (ordCGSTAmount >= 0 && !reader.IsDBNull(ordCGSTAmount)) payment.CGSTAmount = reader.GetDecimal(ordCGSTAmount);
                            if (ordSGSTAmount >= 0 && !reader.IsDBNull(ordSGSTAmount)) payment.SGSTAmount = reader.GetDecimal(ordSGSTAmount);
                            if (ordDiscAmount >= 0 && !reader.IsDBNull(ordDiscAmount)) payment.DiscAmount = reader.GetDecimal(ordDiscAmount);
                            if (ordGSTPerc >= 0 && !reader.IsDBNull(ordGSTPerc)) payment.GST_Perc = reader.GetDecimal(ordGSTPerc);
                            if (ordCGSTPerc >= 0 && !reader.IsDBNull(ordCGSTPerc)) payment.CGST_Perc = reader.GetDecimal(ordCGSTPerc);
                            if (ordSGSTPerc >= 0 && !reader.IsDBNull(ordSGSTPerc)) payment.SGST_Perc = reader.GetDecimal(ordSGSTPerc);
                            if (ordAmountExcl >= 0 && !reader.IsDBNull(ordAmountExcl)) payment.Amount_ExclGST = reader.GetDecimal(ordAmountExcl);
                            if (ordRoundoff >= 0 && !reader.IsDBNull(ordRoundoff)) payment.RoundoffAdjustmentAmt = reader.GetDecimal(ordRoundoff);

                            model.Payments.Add(payment);
                            
                            // If this is an approved payment with GST data, accumulate GST information
                            if (payment.Status == 1 && payment.GSTAmount.HasValue)
                            {
                                totalGSTFromPayments += payment.GSTAmount.Value;
                                totalCGSTFromPayments += payment.CGSTAmount ?? 0m;
                                totalSGSTFromPayments += payment.SGSTAmount ?? 0m;
                                if (payment.GST_Perc.HasValue)
                                {
                                    gstPercentageFromPayments = payment.GST_Perc.Value;
                                }
                            }
                        }

                        // Sum roundoff adjustments across approved payments for order-level display
                        model.TotalRoundoff = model.Payments.Where(p => p.Status == 1).Sum(p => p.RoundoffAdjustmentAmt ?? 0m);

                        // Fallback 1: if payments resultset did not include RoundoffAdjustmentAmt (old SP),
                        // query Payments table directly to get the persisted roundoff sum. This makes the
                        // view robust even if the stored-proc/resultset schema is older than code.
                        try
                        {
                            if (model.TotalRoundoff == 0m)
                            {
                                using (var roundSumCmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT ISNULL(SUM(RoundoffAdjustmentAmt), 0) FROM Payments WHERE OrderId = @OrderId AND Status = 1", connection))
                                {
                                    roundSumCmd.Parameters.AddWithValue("@OrderId", orderId);
                                    var roundObj = roundSumCmd.ExecuteScalar();
                                    if (roundObj != null && roundObj != DBNull.Value)
                                    {
                                        var roundVal = Convert.ToDecimal(roundObj);
                                        if (roundVal != 0m)
                                        {
                                            model.TotalRoundoff = roundVal;
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* ignore fallback failures */ }

                        // Fallback 2: If still zero, compute an implied roundoff per payment by rounding
                        // each payment's (Amount + TipAmount) to the nearest whole rupee using
                        // MidpointRounding.AwayFromZero and take the difference. This covers cases where
                        // the DB/stored-proc did not persist RoundoffAdjustmentAmt but payment.Amount holds
                        // the canonical pre-round amount (OriginalAmount) and the displayed/collected
                        // value was the rounded integer. Using this implied roundoff lets the UI show
                        // the adjustment even without DB schema changes.
                        try
                        {
                            if (model.TotalRoundoff == 0m && model.Payments != null && model.Payments.Any(p => p.Status == 1))
                            {
                                decimal impliedSum = 0m;
                                foreach (var p in model.Payments.Where(p => p.Status == 1))
                                {
                                    var amt = p.Amount + p.TipAmount;
                                    // Round each payment to nearest whole rupee using AwayFromZero
                                    var rounded = Math.Round(amt, 0, MidpointRounding.AwayFromZero);
                                    var delta = Math.Round(rounded - amt, 2, MidpointRounding.AwayFromZero);
                                    impliedSum += delta;
                                }

                                // If implied roundoff is non-zero (payments were effectively rounded), use it
                                if (impliedSum != 0m)
                                {
                                    model.TotalRoundoff = impliedSum;
                                }
                            }
                        }
                        catch { /* ignore implied computation failures */ }
                        
                        // Set GST information from payments data if available, otherwise calculate
                        if (totalGSTFromPayments > 0)
                        {
                            model.GSTPercentage = gstPercentageFromPayments;
                            model.CGSTAmount = totalCGSTFromPayments;
                            model.SGSTAmount = totalSGSTFromPayments;
                            
                            // Update TaxAmount to match total GST from payments
                            if (model.TaxAmount == 0)
                            {
                                model.TaxAmount = totalGSTFromPayments;
                                // Recalculate total amount to include GST
                                model.TotalAmount = model.Subtotal + model.TaxAmount - model.DiscountAmount + model.TipAmount;
                                model.RemainingAmount = model.TotalAmount - model.PaidAmount;
                            }
                        }
                        
                        // Move to next result set: Available payment methods
                        reader.NextResult();
                        
                        while (reader.Read())
                        {
                            model.AvailablePaymentMethods.Add(new PaymentMethodViewModel
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                DisplayName = reader.GetString(2),
                                RequiresCardInfo = reader.GetBoolean(3),
                                RequiresCardPresent = reader.GetBoolean(4),
                                RequiresApproval = reader.GetBoolean(5)
                            });
                        }
                        // Additionally, if the Orders table has a stored RoundoffAdjustmentAmt (order-level), prefer it
                        try
                        {
                            using (var ordRoundCmd = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT ISNULL(RoundoffAdjustmentAmt, 0) FROM Orders WHERE Id = @OrderId", connection))
                            {
                                ordRoundCmd.Parameters.AddWithValue("@OrderId", orderId);
                                var ordRoundObj = ordRoundCmd.ExecuteScalar();
                                if (ordRoundObj != null && ordRoundObj != DBNull.Value)
                                {
                                    var ordRound = Convert.ToDecimal(ordRoundObj);
                                    if (ordRound != 0m)
                                    {
                                        model.TotalRoundoff = ordRound;
                                    }
                                }
                            }
                        }
                        catch { /* ignore order-level roundoff read errors */ }

                        // Recompute paid amount and remaining amount from the payments list to ensure
                        // any RoundoffAdjustmentAmt present on Payments is included even if the first
                        // resultset (or the stored proc) didn't include it.
                        try
                        {
                            var paidFromPayments = model.Payments.Where(p => p.Status == 1).Sum(p => p.Amount + p.TipAmount + (p.RoundoffAdjustmentAmt ?? 0m));
                            // If we have a meaningful sum from approved payments, prefer it over the reader's PaidAmount
                            if (paidFromPayments > 0m)
                            {
                                model.PaidAmount = paidFromPayments;
                            }

                            // Ensure RemainingAmount and TotalRoundoff are consistent
                            model.RemainingAmount = Math.Round(model.TotalAmount - model.PaidAmount, 2, MidpointRounding.AwayFromZero);

                            // If Orders.RoundoffAdjustmentAmt exists but TotalRoundoff is zero, use it
                            if (model.TotalRoundoff == 0m)
                            {
                                try
                                {
                                    using (var ordRoundCmd2 = new Microsoft.Data.SqlClient.SqlCommand(@"SELECT ISNULL(RoundoffAdjustmentAmt, 0) FROM Orders WHERE Id = @OrderId", connection))
                                    {
                                        ordRoundCmd2.Parameters.AddWithValue("@OrderId", orderId);
                                        var ordRoundObj2 = ordRoundCmd2.ExecuteScalar();
                                        if (ordRoundObj2 != null && ordRoundObj2 != DBNull.Value)
                                        {
                                            var ordRound2 = Convert.ToDecimal(ordRoundObj2);
                                            if (ordRound2 != 0m)
                                            {
                                                model.TotalRoundoff = ordRound2;
                                            }
                                        }
                                    }
                                }
                                catch { /* ignore */ }
                            }
                        }
                        catch { /* ignore recompute errors */ }
                    }
                }
                
                // Fallback GST calculation if no payment GST data available
                if (model.GSTPercentage == 0 || (model.CGSTAmount == 0 && model.SGSTAmount == 0))
                {
                    try
                    {
                        using (Microsoft.Data.SqlClient.SqlCommand gstCmd = new Microsoft.Data.SqlClient.SqlCommand(
                            "SELECT DefaultGSTPercentage FROM dbo.RestaurantSettings", connection))
                        {
                            var result = gstCmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                model.GSTPercentage = Convert.ToDecimal(result);
                            }
                            else
                            {
                                model.GSTPercentage = 5.0m;
                            }
                        }
                        
                        decimal gstAmount = model.TaxAmount > 0 ? model.TaxAmount : 
                            Math.Round(model.Subtotal * model.GSTPercentage / 100m, 2, MidpointRounding.AwayFromZero);
                        
                        // Update TaxAmount if it was 0 (calculated GST)
                        if (model.TaxAmount == 0 && gstAmount > 0)
                        {
                            model.TaxAmount = gstAmount;
                        }
                        
                        model.CGSTAmount = Math.Round(gstAmount / 2m, 2, MidpointRounding.AwayFromZero);
                        model.SGSTAmount = gstAmount - model.CGSTAmount;
                        
                        // Recalculate total amount to include GST
                        model.TotalAmount = model.Subtotal + model.TaxAmount - model.DiscountAmount + model.TipAmount;
                        model.RemainingAmount = model.TotalAmount - model.PaidAmount;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error calculating fallback GST for order {OrderId}", model.OrderId);
                        model.GSTPercentage = 5.0m;
                        decimal fallbackGst = Math.Round(model.Subtotal * 0.05m, 2, MidpointRounding.AwayFromZero);
                        
                        // Update TaxAmount with calculated GST
                        if (model.TaxAmount == 0)
                        {
                            model.TaxAmount = fallbackGst;
                        }
                        
                        model.CGSTAmount = Math.Round(fallbackGst / 2m, 2, MidpointRounding.AwayFromZero);
                        model.SGSTAmount = fallbackGst - model.CGSTAmount;
                        
                        // Recalculate total amount to include GST
                        model.TotalAmount = model.Subtotal + model.TaxAmount - model.DiscountAmount + model.TipAmount;
                        model.RemainingAmount = model.TotalAmount - model.PaidAmount;
                    }
                }
                
                // Get split bills
                using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        sb.Id,
                        sb.Amount,
                        sb.TaxAmount,
                        sb.Status,
                        sb.Notes,
                        sb.CreatedByName,
                        sb.CreatedAt
                    FROM SplitBills sb
                    WHERE sb.OrderId = @OrderId
                    ORDER BY sb.CreatedAt DESC", connection))
                {
                    command.Parameters.AddWithValue("@OrderId", orderId);
                    
                    using (Microsoft.Data.SqlClient.SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            model.SplitBills.Add(new SplitBill
                            {
                                Id = reader.GetInt32(0),
                                OrderId = orderId,
                                Amount = reader.GetDecimal(1),
                                TaxAmount = reader.GetDecimal(2),
                                Status = reader.GetInt32(3),
                                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                                CreatedByName = reader.IsDBNull(5) ? null : reader.GetString(5),
                                CreatedAt = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }
            
            return model;
        }

        // Helper to get payment history between two dates
        private List<PaymentHistoryItem> GetPaymentHistory(DateTime fromDate, DateTime toDate)
        {
            var list = new List<PaymentHistoryItem>();
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new Microsoft.Data.SqlClient.SqlCommand(@"
                    SELECT 
                        o.Id AS OrderId,
                        o.OrderNumber,
                        ISNULL(tt.TableName, 'Takeout/Delivery') AS TableName,
                        (SELECT ISNULL(SUM(p2.Amount), 0) FROM Payments p2 WHERE p2.OrderId = o.Id AND p2.Status = 1) AS TotalPayable,
                        ISNULL(SUM(p.Amount), 0) AS TotalPaid,
                        0 AS DueAmount,
                        ISNULL(SUM(p.GSTAmount), 0) AS GSTAmount,
                        MAX(p.CreatedAt) AS PaymentDate,
                        o.Status AS OrderStatus,
                        CASE o.Status 
                            WHEN 0 THEN 'Open'
                            WHEN 1 THEN 'In Progress'
                            WHEN 2 THEN 'Ready'
                            WHEN 3 THEN 'Completed'
                            WHEN 4 THEN 'Cancelled'
                            ELSE 'Unknown'
                        END AS OrderStatusDisplay
                    FROM Orders o
                    LEFT JOIN TableTurnovers tto ON o.TableTurnoverId = tto.Id
                    LEFT JOIN Tables tt ON tto.TableId = tt.Id
                    INNER JOIN Payments p ON o.Id = p.OrderId AND p.Status = 1
                    WHERE CAST(p.CreatedAt AS DATE) BETWEEN @FromDate AND @ToDate
                    GROUP BY o.Id, o.OrderNumber, tt.TableName, o.Status
                    ORDER BY MAX(p.CreatedAt) DESC", connection))
                {
                    command.Parameters.AddWithValue("@FromDate", fromDate.Date);
                    command.Parameters.AddWithValue("@ToDate", toDate.Date);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new PaymentHistoryItem
                            {
                                OrderId = reader.GetInt32(reader.GetOrdinal("OrderId")),
                                OrderNumber = reader.IsDBNull(reader.GetOrdinal("OrderNumber")) ? "" : reader.GetString(reader.GetOrdinal("OrderNumber")),
                                TableName = reader.IsDBNull(reader.GetOrdinal("TableName")) ? "" : GetMergedTableDisplayName((int)reader["OrderId"], reader.GetString(reader.GetOrdinal("TableName"))),
                                TotalPayable = reader.IsDBNull(reader.GetOrdinal("TotalPayable")) ? 0m : Convert.ToDecimal(reader["TotalPayable"]),
                                TotalPaid = reader.IsDBNull(reader.GetOrdinal("TotalPaid")) ? 0m : Convert.ToDecimal(reader["TotalPaid"]),
                                DueAmount = reader.IsDBNull(reader.GetOrdinal("DueAmount")) ? 0m : Convert.ToDecimal(reader["DueAmount"]),
                                GSTAmount = reader.IsDBNull(reader.GetOrdinal("GSTAmount")) ? 0m : Convert.ToDecimal(reader["GSTAmount"]),
                                PaymentDate = reader.IsDBNull(reader.GetOrdinal("PaymentDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("PaymentDate")),
                                OrderStatus = reader.IsDBNull(reader.GetOrdinal("OrderStatus")) ? 0 : reader.GetInt32(reader.GetOrdinal("OrderStatus")),
                                OrderStatusDisplay = reader.IsDBNull(reader.GetOrdinal("OrderStatusDisplay")) ? "" : reader.GetString(reader.GetOrdinal("OrderStatusDisplay"))
                            };

                            list.Add(item);
                        }
                    }
                }
            }

            return list;
        }

        // GET: Payment/ExportCsv
        public IActionResult ExportCsv(DateTime? fromDate, DateTime? toDate)
        {
            var from = fromDate ?? DateTime.Today;
            var to = toDate ?? DateTime.Today;

            try
            {
                var items = GetPaymentHistory(from, to);

                var csv = "OrderId,OrderNumber,TableName,TotalPayable,GSTAmount,TotalPaid,DueAmount,OrderStatus,PaymentDate\n";
                foreach (var p in items)
                {
                    var safeTable = (p.TableName ?? "").Replace("\"", "\"\"");
                    var safeOrder = (p.OrderNumber ?? "").Replace("\"", "\"\"");
                    csv += $"{p.OrderId},\"{safeOrder}\",\"{safeTable}\",{p.TotalPayable},{p.GSTAmount},{p.TotalPaid},{p.DueAmount},\"{p.OrderStatusDisplay}\",{p.PaymentDate:O}\n";
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                return File(bytes, "text/csv", "payment-history.csv");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error exporting CSV: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }

        // GET: Payment/Print
        public IActionResult Print(DateTime? fromDate, DateTime? toDate)
        {
            var from = fromDate ?? DateTime.Today;
            var to = toDate ?? DateTime.Today;

            var model = new PaymentDashboardViewModel
            {
                FromDate = from,
                ToDate = to,
                PaymentHistory = GetPaymentHistory(from, to)
            };

            return View("Print", model);
        }
        
        private int GetCurrentUserId()
        {
            try
            {
                var claim = HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
                if (claim != null && int.TryParse(claim.Value, out int uid)) return uid;
            }
            catch { /* ignore and fallback */ }
            // Fallback to admin (legacy behavior) if no authenticated user is present
            return 1;
        }

        private string GetCurrentUserName()
        {
            try
            {
                var name = HttpContext?.User?.Identity?.Name;
                if (!string.IsNullOrEmpty(name)) return name;

                var fullNameClaim = HttpContext?.User?.FindFirst("FullName");
                if (fullNameClaim != null) return fullNameClaim.Value;
            }
            catch { }
            return "System Admin";
        }
        
        // GET: Payment/PrintBill
        public IActionResult PrintBill(int orderId)
        {
            try
            {
                var model = GetPaymentViewModel(orderId);
                
                if (model == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index", "Order");
                }
                
                // Get restaurant settings for bill header
                RestaurantSettings settings = null;
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT * FROM dbo.RestaurantSettings", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                settings = new RestaurantSettings
                                {
                                    RestaurantName = reader["RestaurantName"].ToString(),
                                    StreetAddress = reader["StreetAddress"].ToString(),
                                    City = reader["City"].ToString(),
                                    State = reader["State"].ToString(),
                                    Pincode = reader["Pincode"].ToString(),
                                    Country = reader["Country"].ToString(),
                                    GSTCode = reader["GSTCode"].ToString(),
                                    PhoneNumber = reader["PhoneNumber"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    Website = reader["Website"].ToString(),
                                    CurrencySymbol = reader["CurrencySymbol"].ToString(),
                                    DefaultGSTPercentage = reader["DefaultGSTPercentage"] != DBNull.Value 
                                        ? Convert.ToDecimal(reader["DefaultGSTPercentage"]) 
                                        : 0
                                };
                                    // Read FssaiNo if present
                                    try { settings.FssaiNo = reader["FssaiNo"].ToString(); } catch { /* ignore if column missing */ }
                            }
                        }
                    }
                }
                
                ViewBag.RestaurantSettings = settings ?? new RestaurantSettings
                {
                    RestaurantName = "Restaurant Management System",
                    GSTCode = "Not Configured",
                    StreetAddress = "",
                    City = "",
                    State = "",
                    Pincode = "",
                    Country = "",
                    PhoneNumber = "",
                    Email = ""
                };
                
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading bill for printing: {ex.Message}";
                return RedirectToAction("Index", new { id = orderId });
            }
        }

        // GET: Payment/PrintPOS
        public IActionResult PrintPOS(int orderId)
        {
            try
            {
                var model = GetPaymentViewModel(orderId);
                if (model == null)
                {
                    TempData["ErrorMessage"] = "Order not found.";
                    return RedirectToAction("Index", "Order");
                }

                // Get restaurant settings for bill header (reuse same logic as PrintBill)
                RestaurantSettings settings = null;
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT * FROM dbo.RestaurantSettings", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                settings = new RestaurantSettings
                                {
                                    RestaurantName = reader["RestaurantName"].ToString(),
                                    StreetAddress = reader["StreetAddress"].ToString(),
                                    City = reader["City"].ToString(),
                                    State = reader["State"].ToString(),
                                    Pincode = reader["Pincode"].ToString(),
                                    Country = reader["Country"].ToString(),
                                    GSTCode = reader["GSTCode"].ToString(),
                                    PhoneNumber = reader["PhoneNumber"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    Website = reader["Website"].ToString(),
                                    CurrencySymbol = reader["CurrencySymbol"].ToString(),
                                    DefaultGSTPercentage = reader["DefaultGSTPercentage"] != DBNull.Value 
                                        ? Convert.ToDecimal(reader["DefaultGSTPercentage"]) 
                                        : 0
                                };
                            }
                        }
                    }
                }

                ViewBag.RestaurantSettings = settings ?? new RestaurantSettings
                {
                    RestaurantName = "Restaurant Management System",
                    GSTCode = "Not Configured",
                    StreetAddress = "",
                    City = "",
                    State = "",
                    Pincode = "",
                    Country = "",
                    PhoneNumber = "",
                    Email = ""
                };
                // If FssaiNo wasn't present from the full SELECT (older DBs), try a lightweight read of the column
                try
                {
                    var existing = ViewBag.RestaurantSettings as RestaurantSettings;
                    if (existing != null && string.IsNullOrWhiteSpace(existing.FssaiNo))
                    {
                        using (var conn2 = new SqlConnection(_connectionString))
                        {
                            conn2.Open();
                            using (var cmd2 = new SqlCommand("SELECT TOP 1 FssaiNo FROM dbo.RestaurantSettings WHERE FssaiNo IS NOT NULL AND LTRIM(RTRIM(FssaiNo)) <> ''", conn2))
                            {
                                var val = cmd2.ExecuteScalar();
                                if (val != null && val != DBNull.Value)
                                {
                                    existing.FssaiNo = val.ToString();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore - column may not exist or DB user may not have permissions
                }

                return View("PrintPOS", model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading POS bill for printing: {ex.Message}";
                return RedirectToAction("Index", new { id = orderId });
            }
        }
    }
}
