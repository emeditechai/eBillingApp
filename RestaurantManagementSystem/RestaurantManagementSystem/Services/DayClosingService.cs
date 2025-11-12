using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RestaurantManagementSystem.Models;

namespace RestaurantManagementSystem.Services
{
    public interface IDayClosingService
    {
        Task<List<CashierOption>> GetAvailableCashiersAsync(DateTime businessDate);
        Task<(bool Success, string Message)> InitializeDayOpeningAsync(DateTime businessDate, int cashierId, decimal openingFloat, string createdBy);
        Task<decimal> GetCashierSystemAmountAsync(DateTime businessDate, int cashierId);
        Task<List<CashierDayCloseViewModel>> GetDayClosingSummaryAsync(DateTime businessDate);
        Task<DayLockStatus?> GetDayLockStatusAsync(DateTime businessDate);
        Task<(bool Success, string Message, decimal Variance)> SaveDeclaredCashAsync(DateTime businessDate, int cashierId, decimal declaredAmount, string updatedBy);
        Task<(bool Success, string Message)> ApproveVarianceAsync(int closeId, string approvedBy, string comment, bool approved);
        Task<(bool Success, string Message, int IssueCount)> LockDayAsync(DateTime businessDate, string lockedBy, string? remarks);
        Task<EODReportViewModel> GenerateEODReportAsync(DateTime businessDate, string generatedBy);
        Task<bool> UpdateCashierSystemAmountsAsync(DateTime businessDate);
        Task<CashClosingReportViewModel> GenerateCashClosingReportAsync(DateTime startDate, DateTime endDate, int? cashierId = null);
    }

    public class DayClosingService : IDayClosingService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public DayClosingService(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        /// <summary>
        /// Get list of cashiers available for day opening
        /// </summary>
        public async Task<List<CashierOption>> GetAvailableCashiersAsync(DateTime businessDate)
        {
            var cashiers = new List<CashierOption>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT DISTINCT 
                        u.Id, 
                        u.Username, 
                        u.FullName,
                        CASE WHEN cdo.Id IS NOT NULL THEN 1 ELSE 0 END AS AlreadyInitialized
                    FROM Users u
                    INNER JOIN UserRoles ur ON u.Id = ur.UserId
                    INNER JOIN Roles r ON ur.RoleId = r.Id
                    LEFT JOIN CashierDayOpening cdo ON u.Id = cdo.CashierId AND cdo.BusinessDate = @BusinessDate
                    WHERE r.Name IN ('Cashier', 'Manager', 'Administrator')
                      AND u.IsActive = 1
                    ORDER BY u.Username";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            cashiers.Add(new CashierOption
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                                Username = reader.GetString(1),
                                AlreadyInitialized = reader.GetInt32(3) == 1
                            });
                        }
                    }
                }
            }

            return cashiers;
        }

        /// <summary>
        /// Initialize day opening for a cashier
        /// </summary>
        public async Task<(bool Success, string Message)> InitializeDayOpeningAsync(
            DateTime businessDate, int cashierId, decimal openingFloat, string createdBy)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_InitializeDayOpening", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);
                    command.Parameters.AddWithValue("@CashierId", cashierId);
                    command.Parameters.AddWithValue("@OpeningFloat", openingFloat);
                    command.Parameters.AddWithValue("@CreatedBy", createdBy);

                    try
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var message = reader["Message"].ToString() ?? "Success";
                                return (true, message);
                            }
                        }
                        return (true, "Opening float initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Get system cash amount for a cashier
        /// </summary>
        public async Task<decimal> GetCashierSystemAmountAsync(DateTime businessDate, int cashierId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_GetCashierSystemAmount", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);
                    command.Parameters.AddWithValue("@CashierId", cashierId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetDecimal(0);
                        }
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Update system amounts for all cashiers on a given date
        /// </summary>
        public async Task<bool> UpdateCashierSystemAmountsAsync(DateTime businessDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Query using Payments table (actual schema)
                var query = @"
                    -- Refresh SystemAmount per cashier for the business date
                    -- Prefer Orders.CashierId; fallback to Payments.ProcessedBy when Orders.CashierId is NULL
                    UPDATE cdc
                    SET cdc.SystemAmount = ISNULL(ROUND(cashSummary.CashAmount, 0), 0) -- adjust to whole rupees including roundoff
                    FROM CashierDayClose cdc
                    LEFT JOIN (
                        SELECT 
                            COALESCE(o.CashierId, p.ProcessedBy) AS CashierId,
                            SUM(p.Amount + ISNULL(p.RoundoffAdjustmentAmt, 0)) AS CashAmount -- include per-payment roundoff adjustments
                        FROM Orders o
                        INNER JOIN Payments p ON p.OrderId = o.Id
                        INNER JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
                        WHERE CAST(o.CreatedAt AS DATE) = @BusinessDate
                          AND pm.Name = 'CASH'
                          AND p.Status = 1
                          AND o.Status IN (2, 3)
                        GROUP BY COALESCE(o.CashierId, p.ProcessedBy)
                    ) cashSummary ON cdc.CashierId = cashSummary.CashierId
                    WHERE cdc.BusinessDate = @BusinessDate";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);
                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }

        /// <summary>
        /// Get day closing summary for all cashiers
        /// </summary>
        public async Task<List<CashierDayCloseViewModel>> GetDayClosingSummaryAsync(DateTime businessDate)
        {
            // First update system amounts
            await UpdateCashierSystemAmountsAsync(businessDate);

            var closings = new List<CashierDayCloseViewModel>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_GetDayClosingSummary", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var status = reader.GetString(reader.GetOrdinal("Status"));
                            var variance = reader.IsDBNull(reader.GetOrdinal("Variance")) 
                                ? (decimal?)null 
                                : reader.GetDecimal(reader.GetOrdinal("Variance"));

                            closings.Add(new CashierDayCloseViewModel
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                CashierId = reader.GetInt32(reader.GetOrdinal("CashierId")),
                                CashierName = reader.GetString(reader.GetOrdinal("CashierName")),
                                OpeningFloat = reader.GetDecimal(reader.GetOrdinal("OpeningFloat")),
                                SystemAmount = reader.GetDecimal(reader.GetOrdinal("SystemAmount")),
                                DeclaredAmount = reader.IsDBNull(reader.GetOrdinal("DeclaredAmount")) 
                                    ? (decimal?)null 
                                    : reader.GetDecimal(reader.GetOrdinal("DeclaredAmount")),
                                ExpectedCash = reader.GetDecimal(reader.GetOrdinal("ExpectedCash")),
                                Variance = variance,
                                Status = status,
                                ApprovedBy = reader.IsDBNull(reader.GetOrdinal("ApprovedBy")) 
                                    ? null 
                                    : reader.GetString(reader.GetOrdinal("ApprovedBy")),
                                ApprovalComment = reader.IsDBNull(reader.GetOrdinal("ApprovalComment")) 
                                    ? null 
                                    : reader.GetString(reader.GetOrdinal("ApprovalComment")),
                                LockedFlag = reader.GetBoolean(reader.GetOrdinal("LockedFlag")),
                                LockedAt = reader.IsDBNull(reader.GetOrdinal("LockedAt")) 
                                    ? (DateTime?)null 
                                    : reader.GetDateTime(reader.GetOrdinal("LockedAt")),
                                LockedBy = reader.IsDBNull(reader.GetOrdinal("LockedBy")) 
                                    ? null 
                                    : reader.GetString(reader.GetOrdinal("LockedBy")),
                                StatusBadgeClass = GetStatusBadgeClass(status),
                                StatusIcon = GetStatusIcon(status),
                                RequiresApproval = status == "CHECK"
                            });
                        }
                    }
                }
            }

            return closings;
        }

        /// <summary>
        /// Get day lock status
        /// </summary>
        public async Task<DayLockStatus?> GetDayLockStatusAsync(DateTime businessDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP 1
                        LockId, BusinessDate, LockedBy, LockTime, Remarks, Status
                    FROM DayLockAudit
                    WHERE BusinessDate = @BusinessDate
                    ORDER BY LockTime DESC";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new DayLockStatus
                            {
                                LockId = reader.GetInt32(0),
                                BusinessDate = reader.GetDateTime(1),
                                LockedBy = reader.GetString(2),
                                LockTime = reader.GetDateTime(3),
                                Remarks = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Status = reader.GetString(5)
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Save declared cash amount for a cashier
        /// </summary>
        public async Task<(bool Success, string Message, decimal Variance)> SaveDeclaredCashAsync(
            DateTime businessDate, int cashierId, decimal declaredAmount, string updatedBy)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_SaveDeclaredCash", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);
                    command.Parameters.AddWithValue("@CashierId", cashierId);
                    command.Parameters.AddWithValue("@DeclaredAmount", declaredAmount);
                    command.Parameters.AddWithValue("@UpdatedBy", updatedBy);

                    try
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var result = reader["Result"].ToString();
                                var variance = reader.GetDecimal(reader.GetOrdinal("Variance"));
                                var message = reader["Message"].ToString() ?? "Success";

                                return (true, message, variance);
                            }
                        }
                        return (false, "No response from database", 0);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Error: {ex.Message}", 0);
                    }
                }
            }
        }

        /// <summary>
        /// Approve or reject variance
        /// </summary>
        public async Task<(bool Success, string Message)> ApproveVarianceAsync(
            int closeId, string approvedBy, string comment, bool approved)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = @"
                    UPDATE CashierDayClose
                    SET Status = @Status,
                        ApprovedBy = @ApprovedBy,
                        ApprovalComment = @Comment,
                        UpdatedBy = @ApprovedBy,
                        UpdatedAt = GETDATE()
                    WHERE Id = @CloseId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CloseId", closeId);
                    command.Parameters.AddWithValue("@Status", approved ? "OK" : "CHECK");
                    command.Parameters.AddWithValue("@ApprovedBy", approvedBy);
                    command.Parameters.AddWithValue("@Comment", comment);

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        return (true, approved ? "Variance approved successfully" : "Variance requires re-verification");
                    }
                    return (false, "Failed to update approval status");
                }
            }
        }

        /// <summary>
        /// Lock the business day
        /// </summary>
        public async Task<(bool Success, string Message, int IssueCount)> LockDayAsync(
            DateTime businessDate, string lockedBy, string? remarks)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_LockDay", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);
                    command.Parameters.AddWithValue("@LockedBy", lockedBy);
                    command.Parameters.AddWithValue("@Remarks", (object?)remarks ?? DBNull.Value);

                    try
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var result = reader["Result"].ToString();
                                var message = reader["Message"].ToString() ?? "Unknown result";

                                if (result == "Success")
                                {
                                    return (true, message, 0);
                                }
                                else
                                {
                                    var issueCount = reader.IsDBNull(reader.GetOrdinal("IssueCount")) 
                                        ? 0 
                                        : reader.GetInt32(reader.GetOrdinal("IssueCount"));
                                    return (false, message, issueCount);
                                }
                            }
                        }
                        return (false, "No response from database", 0);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Error: {ex.Message}", 0);
                    }
                }
            }
        }

        /// <summary>
        /// Generate EOD Report
        /// </summary>
        public async Task<EODReportViewModel> GenerateEODReportAsync(DateTime businessDate, string generatedBy)
        {
            var report = new EODReportViewModel
            {
                BusinessDate = businessDate,
                GeneratedBy = generatedBy,
                GeneratedAt = DateTime.Now
            };

            // Get cashier details
            report.CashierDetails = await GetDayClosingSummaryAsync(businessDate);

            // Get lock status
            report.LockStatus = await GetDayLockStatusAsync(businessDate);

            // Calculate summary
            report.Summary = new DaySummary
            {
                TotalCashiers = report.CashierDetails.Count,
                PendingCount = report.CashierDetails.Count(c => c.Status == "PENDING"),
                OkCount = report.CashierDetails.Count(c => c.Status == "OK"),
                CheckCount = report.CashierDetails.Count(c => c.Status == "CHECK"),
                LockedCount = report.CashierDetails.Count(c => c.Status == "LOCKED"),
                TotalOpeningFloat = report.CashierDetails.Sum(c => c.OpeningFloat),
                TotalSystemAmount = report.CashierDetails.Sum(c => c.SystemAmount),
                TotalDeclaredAmount = report.CashierDetails.Sum(c => c.DeclaredAmount ?? 0),
                TotalVariance = report.CashierDetails.Sum(c => c.Variance ?? 0),
                TotalExpectedCash = report.CashierDetails.Sum(c => c.ExpectedCash)
            };

            // Get restaurant settings
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "SELECT TOP 1 RestaurantName FROM RestaurantSettings";
                using (var command = new SqlCommand(query, connection))
                {
                    var name = await command.ExecuteScalarAsync();
                    report.RestaurantName = name?.ToString() ?? "Restaurant";
                }

                // Get sales summary
                var salesQuery = @"
                    SELECT 
                        COUNT(DISTINCT o.Id) AS TotalOrders,
                        ISNULL(SUM(o.TotalAmount), 0) AS TotalSales,
                        ISNULL(SUM(CASE WHEN pm.Name = 'CASH' THEN p.Amount ELSE 0 END), 0) AS CashSales,
                        ISNULL(SUM(CASE WHEN pm.Name IN ('CREDIT_CARD', 'DEBIT_CARD') THEN p.Amount ELSE 0 END), 0) AS CardSales,
                        ISNULL(SUM(CASE WHEN pm.Name NOT IN ('CASH', 'CREDIT_CARD', 'DEBIT_CARD') THEN p.Amount ELSE 0 END), 0) AS OtherSales,
                        COUNT(DISTINCT CASE WHEN o.CustomerName IS NOT NULL THEN o.CustomerName ELSE NULL END) AS TotalCustomers
                    FROM Orders o
                    LEFT JOIN Payments p ON p.OrderId = o.Id AND p.Status = 1
                    LEFT JOIN PaymentMethods pm ON p.PaymentMethodId = pm.Id
                    WHERE CAST(o.CreatedAt AS DATE) = @BusinessDate
                      AND o.Status IN (2, 3)";

                using (var command = new SqlCommand(salesQuery, connection))
                {
                    command.Parameters.AddWithValue("@BusinessDate", businessDate);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            report.TotalOrders = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            report.TotalSales = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            report.CashSales = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                            report.CardSales = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                            report.UPISales = 0; // No separate UPI tracking, included in OtherSales
                            report.OtherSales = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
                            report.TotalCustomers = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                        }
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Generate comprehensive cash closing report for date range
        /// </summary>
        public async Task<CashClosingReportViewModel> GenerateCashClosingReportAsync(DateTime startDate, DateTime endDate, int? cashierId = null)
        {
            var report = new CashClosingReportViewModel();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new SqlCommand("usp_GetCashClosingReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@CashierId", cashierId.HasValue ? (object)cashierId.Value : DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Result Set 1: Summary Statistics
                        if (await reader.ReadAsync())
                        {
                            int ordTotalDays = reader.GetOrdinal("TotalDays");
                            int ordTotalCashiers = reader.GetOrdinal("TotalCashiers");
                            int ordOpening = reader.GetOrdinal("TotalOpeningFloat");
                            int ordSystem = reader.GetOrdinal("TotalSystemAmount");
                            int ordDeclared = reader.GetOrdinal("TotalDeclaredAmount");
                            int ordVariance = reader.GetOrdinal("TotalVariance");
                            int ordCashOver = reader.GetOrdinal("TotalCashOver");
                            int ordCashShort = reader.GetOrdinal("TotalCashShort");
                            int ordApproved = reader.GetOrdinal("ApprovedCount");
                            int ordPending = reader.GetOrdinal("PendingApprovalCount");
                            int ordLocked = reader.GetOrdinal("LockedCount");

                            report.Summary = new CashClosingReportSummary
                            {
                                TotalDays = reader.IsDBNull(ordTotalDays) ? 0 : reader.GetInt32(ordTotalDays),
                                TotalCashiers = reader.IsDBNull(ordTotalCashiers) ? 0 : reader.GetInt32(ordTotalCashiers),
                                TotalOpeningFloat = reader.IsDBNull(ordOpening) ? 0 : reader.GetDecimal(ordOpening),
                                TotalSystemAmount = reader.IsDBNull(ordSystem) ? 0 : reader.GetDecimal(ordSystem),
                                TotalDeclaredAmount = reader.IsDBNull(ordDeclared) ? 0 : reader.GetDecimal(ordDeclared),
                                TotalVariance = reader.IsDBNull(ordVariance) ? 0 : reader.GetDecimal(ordVariance),
                                TotalCashOver = reader.IsDBNull(ordCashOver) ? 0 : reader.GetDecimal(ordCashOver),
                                TotalCashShort = reader.IsDBNull(ordCashShort) ? 0 : reader.GetDecimal(ordCashShort),
                                ApprovedCount = reader.IsDBNull(ordApproved) ? 0 : reader.GetInt32(ordApproved),
                                PendingApprovalCount = reader.IsDBNull(ordPending) ? 0 : reader.GetInt32(ordPending),
                                LockedCount = reader.IsDBNull(ordLocked) ? 0 : reader.GetInt32(ordLocked)
                            };
                        }

                        // Result Set 2: Daily Summary
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int ordBizDate = reader.GetOrdinal("BusinessDate");
                                int ordCashierCount = reader.GetOrdinal("CashierCount");
                                int ordDayOpening = reader.GetOrdinal("DayOpeningFloat");
                                int ordDaySystem = reader.GetOrdinal("DaySystemAmount");
                                int ordDayDeclared = reader.GetOrdinal("DayDeclaredAmount");
                                int ordDayVariance = reader.GetOrdinal("DayVariance");
                                int ordDayCashOver = reader.GetOrdinal("DayCashOver");
                                int ordDayCashShort = reader.GetOrdinal("DayCashShort");
                                int ordIsLocked = reader.GetOrdinal("IsDayLocked");

                                report.DailySummaries.Add(new CashClosingDailySummary
                                {
                                    BusinessDate = reader.GetDateTime(ordBizDate),
                                    CashierCount = reader.IsDBNull(ordCashierCount) ? 0 : reader.GetInt32(ordCashierCount),
                                    DayOpeningFloat = reader.IsDBNull(ordDayOpening) ? 0 : reader.GetDecimal(ordDayOpening),
                                    DaySystemAmount = reader.IsDBNull(ordDaySystem) ? 0 : reader.GetDecimal(ordDaySystem),
                                    DayDeclaredAmount = reader.IsDBNull(ordDayDeclared) ? 0 : reader.GetDecimal(ordDayDeclared),
                                    DayVariance = reader.IsDBNull(ordDayVariance) ? 0 : reader.GetDecimal(ordDayVariance),
                                    DayCashOver = reader.IsDBNull(ordDayCashOver) ? 0 : reader.GetDecimal(ordDayCashOver),
                                    DayCashShort = reader.IsDBNull(ordDayCashShort) ? 0 : reader.GetDecimal(ordDayCashShort),
                                    IsDayLocked = reader.IsDBNull(ordIsLocked) ? "No" : reader.GetString(ordIsLocked)
                                });
                            }
                        }

                        // Result Set 3: Detailed Records
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                report.DetailRecords.Add(new CashClosingDetailRecord
                                {
                                    BusinessDate = reader.GetDateTime(reader.GetOrdinal("BusinessDate")),
                                    CashierId = reader.GetInt32(reader.GetOrdinal("CashierId")),
                                    CashierName = reader.GetString(reader.GetOrdinal("CashierName")),
                                    OpeningFloat = reader.GetDecimal(reader.GetOrdinal("OpeningFloat")),
                                    SystemAmount = reader.GetDecimal(reader.GetOrdinal("SystemAmount")),
                                    ExpectedCash = reader.GetDecimal(reader.GetOrdinal("ExpectedCash")),
                                    DeclaredAmount = reader.IsDBNull(reader.GetOrdinal("DeclaredAmount")) ? 0 : reader.GetDecimal(reader.GetOrdinal("DeclaredAmount")),
                                    Variance = reader.IsDBNull(reader.GetOrdinal("Variance")) ? 0 : reader.GetDecimal(reader.GetOrdinal("Variance")),
                                    VarianceType = reader.GetString(reader.GetOrdinal("VarianceType")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    ApprovedBy = reader.IsDBNull(reader.GetOrdinal("ApprovedBy")) ? null : reader.GetString(reader.GetOrdinal("ApprovedBy")),
                                    ApprovalComment = reader.IsDBNull(reader.GetOrdinal("ApprovalComment")) ? null : reader.GetString(reader.GetOrdinal("ApprovalComment")),
                                    LockedFlag = reader.GetBoolean(reader.GetOrdinal("LockedFlag")),
                                    LockedAt = reader.IsDBNull(reader.GetOrdinal("LockedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("LockedAt")),
                                    LockedBy = reader.IsDBNull(reader.GetOrdinal("LockedBy")) ? null : reader.GetString(reader.GetOrdinal("LockedBy")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("UpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
                                });
                            }
                        }

                        // Result Set 4: Cashier Performance
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int ordCashierId = reader.GetOrdinal("CashierId");
                                int ordCashierName = reader.GetOrdinal("CashierName");
                                int ordTotalDaysWorked = reader.GetOrdinal("TotalDaysWorked");
                                int ordTotalCashCollected = reader.GetOrdinal("TotalCashCollected");
                                int ordAverageVariance = reader.GetOrdinal("AverageVariance");
                                int ordBestVariance = reader.GetOrdinal("BestVariance");
                                int ordWorstVariance = reader.GetOrdinal("WorstVariance");
                                int ordDaysWithinTol = reader.GetOrdinal("DaysWithinTolerance");
                                int ordDaysAboveTol = reader.GetOrdinal("DaysAboveTolerance");
                                int ordApprovedDays = reader.GetOrdinal("ApprovedDays");
                                int ordPendingDays = reader.GetOrdinal("PendingDays");

                                report.CashierPerformance.Add(new CashClosingCashierPerformance
                                {
                                    CashierId = reader.IsDBNull(ordCashierId) ? 0 : reader.GetInt32(ordCashierId),
                                    CashierName = reader.IsDBNull(ordCashierName) ? string.Empty : reader.GetString(ordCashierName),
                                    TotalDaysWorked = reader.IsDBNull(ordTotalDaysWorked) ? 0 : reader.GetInt32(ordTotalDaysWorked),
                                    TotalCashCollected = reader.IsDBNull(ordTotalCashCollected) ? 0 : reader.GetDecimal(ordTotalCashCollected),
                                    AverageVariance = reader.IsDBNull(ordAverageVariance) ? 0 : reader.GetDecimal(ordAverageVariance),
                                    BestVariance = reader.IsDBNull(ordBestVariance) ? 0 : reader.GetDecimal(ordBestVariance),
                                    WorstVariance = reader.IsDBNull(ordWorstVariance) ? 0 : reader.GetDecimal(ordWorstVariance),
                                    DaysWithinTolerance = reader.IsDBNull(ordDaysWithinTol) ? 0 : reader.GetInt32(ordDaysWithinTol),
                                    DaysAboveTolerance = reader.IsDBNull(ordDaysAboveTol) ? 0 : reader.GetInt32(ordDaysAboveTol),
                                    ApprovedDays = reader.IsDBNull(ordApprovedDays) ? 0 : reader.GetInt32(ordApprovedDays),
                                    PendingDays = reader.IsDBNull(ordPendingDays) ? 0 : reader.GetInt32(ordPendingDays)
                                });
                            }
                        }

                        // Result Set 5: Day Lock Audit
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                report.DayLockAudits.Add(new CashClosingDayLockAudit
                                {
                                    BusinessDate = reader.GetDateTime(reader.GetOrdinal("BusinessDate")),
                                    LockedBy = reader.GetString(reader.GetOrdinal("LockedBy")),
                                    LockTime = reader.GetDateTime(reader.GetOrdinal("LockTime")),
                                    Remarks = reader.IsDBNull(reader.GetOrdinal("Remarks")) ? null : reader.GetString(reader.GetOrdinal("Remarks")),
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    ReopenedBy = reader.IsDBNull(reader.GetOrdinal("ReopenedBy")) ? null : reader.GetString(reader.GetOrdinal("ReopenedBy")),
                                    ReopenedAt = reader.IsDBNull(reader.GetOrdinal("ReopenedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReopenedAt")),
                                    ReopenReason = reader.IsDBNull(reader.GetOrdinal("ReopenReason")) ? null : reader.GetString(reader.GetOrdinal("ReopenReason"))
                                });
                            }
                        }
                    }
                }
            }

            return report;
        }

        // Helper methods
        private string GetStatusBadgeClass(string status) => status switch
        {
            "PENDING" => "bg-warning text-dark",
            "OK" => "bg-success",
            "CHECK" => "bg-danger",
            "LOCKED" => "bg-secondary",
            _ => "bg-secondary"
        };

        private string GetStatusIcon(string status) => status switch
        {
            "PENDING" => "fa-clock",
            "OK" => "fa-check-circle",
            "CHECK" => "fa-exclamation-triangle",
            "LOCKED" => "fa-lock",
            _ => "fa-question-circle"
        };
    }
}
