using Microsoft.Data.SqlClient;

namespace RestaurantManagementSystem.Controllers
{
    public partial class PaymentController
    {
        private void TryApplyStockOutForCompletedOrder(int orderId, SqlConnection? existingConnection = null)
        {
            if (orderId <= 0)
            {
                return;
            }

            try
            {
                if (existingConnection != null)
                {
                    if (!GetIsSaleFromInventoryEnabled(existingConnection))
                    {
                        return;
                    }

                    ApplyStockOutForCompletedOrder(orderId, existingConnection);
                    return;
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    if (!GetIsSaleFromInventoryEnabled(connection))
                    {
                        return;
                    }

                    ApplyStockOutForCompletedOrder(orderId, connection);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Stock OUT sync failed for order {OrderId}", orderId);
            }
        }

        private void ApplyStockOutForCompletedOrder(int orderId, SqlConnection connection)
        {
            bool orderCompleted;
            using (var statusCmd = new SqlCommand("SELECT ISNULL(Status, 0) FROM dbo.Orders WHERE Id = @OrderId", connection))
            {
                statusCmd.Parameters.AddWithValue("@OrderId", orderId);
                var statusObj = statusCmd.ExecuteScalar();
                var status = statusObj == null || statusObj == DBNull.Value ? 0 : Convert.ToInt32(statusObj);
                orderCompleted = status == 3;
            }

            if (!orderCompleted)
            {
                return;
            }

            var defaultGodownId = GetDefaultStockGodownId(connection);
            if (defaultGodownId <= 0)
            {
                return;
            }

            var createdBy = GetCurrentUserId();
            var lineItems = new List<(int MenuItemId, decimal Quantity, decimal UnitCost)>();
            using (var itemsCmd = new SqlCommand(@"
SELECT
    oi.MenuItemId,
    SUM(CAST(oi.Quantity AS decimal(18,3))) AS Qty,
    MAX(CAST(ISNULL(oi.UnitPrice, 0) AS decimal(18,2))) AS UnitCost
FROM dbo.OrderItems oi
WHERE oi.OrderId = @OrderId
  AND ISNULL(oi.Status, 0) <> 5
GROUP BY oi.MenuItemId
HAVING SUM(CAST(oi.Quantity AS decimal(18,3))) > 0", connection))
            {
                itemsCmd.Parameters.AddWithValue("@OrderId", orderId);
                using (var reader = itemsCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lineItems.Add(
                            (
                                reader.GetInt32(0),
                                reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                                reader.IsDBNull(2) ? 0 : reader.GetDecimal(2)
                            )
                        );
                    }
                }
            }

            if (lineItems.Count == 0)
            {
                return;
            }

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var lineItem in lineItems)
                    {
                        if (lineItem.Quantity <= 0)
                        {
                            continue;
                        }

                        bool exists;
                        using (var checkCmd = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.InventoryStockMovements
WHERE MovementType = 'OUT'
    AND UPPER(ReferenceType) IN ('SALE', 'ORDER')
  AND OrderId = @OrderId
  AND MenuItemId = @MenuItemId
  AND GodownId = @GodownId", connection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                            checkCmd.Parameters.AddWithValue("@MenuItemId", lineItem.MenuItemId);
                            checkCmd.Parameters.AddWithValue("@GodownId", defaultGodownId);
                            exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                        }

                        if (exists)
                        {
                            continue;
                        }

                        int movementId;
                        using (var movementCmd = new SqlCommand(@"
INSERT INTO dbo.InventoryStockMovements
(
    MovementType, ReferenceType, ReferenceId, OrderId, MenuItemId, GodownId,
    Quantity, UnitCost, PartyId, Notes, CreatedBy, CreatedAt
)
VALUES
(
    'OUT', 'ORDER', @OrderId, @OrderId, @MenuItemId, @GodownId,
    @Quantity, @UnitCost, NULL, @Notes, @CreatedBy, SYSUTCDATETIME()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
                        {
                            movementCmd.Parameters.AddWithValue("@OrderId", orderId);
                            movementCmd.Parameters.AddWithValue("@MenuItemId", lineItem.MenuItemId);
                            movementCmd.Parameters.AddWithValue("@GodownId", defaultGodownId);
                            movementCmd.Parameters.AddWithValue("@Quantity", lineItem.Quantity);
                            movementCmd.Parameters.AddWithValue("@UnitCost", lineItem.UnitCost);
                            movementCmd.Parameters.AddWithValue("@Notes", "Auto stock-out on sale");
                            movementCmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                            movementId = Convert.ToInt32(movementCmd.ExecuteScalar());
                        }

                        using (var stockCmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.InventoryStock WITH (UPDLOCK, HOLDLOCK) WHERE MenuItemId = @MenuItemId AND GodownId = @GodownId)
BEGIN
    UPDATE dbo.InventoryStock
    SET QuantityOnHand = ISNULL(QuantityOnHand, 0) - @Quantity,
        UpdatedAt = SYSUTCDATETIME()
    WHERE MenuItemId = @MenuItemId AND GodownId = @GodownId;
END
ELSE
BEGIN
    INSERT INTO dbo.InventoryStock (MenuItemId, GodownId, QuantityOnHand, UpdatedAt, LowLevelQty)
    VALUES (@MenuItemId, @GodownId, (0 - @Quantity), SYSUTCDATETIME(), NULL);
END

UPDATE dbo.InventoryStockMovements
SET ReferenceId = @MovementId
WHERE Id = @MovementId;", connection, transaction))
                        {
                            stockCmd.Parameters.AddWithValue("@MenuItemId", lineItem.MenuItemId);
                            stockCmd.Parameters.AddWithValue("@GodownId", defaultGodownId);
                            stockCmd.Parameters.AddWithValue("@Quantity", lineItem.Quantity);
                            stockCmd.Parameters.AddWithValue("@MovementId", movementId);
                            stockCmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private static int GetDefaultStockGodownId(SqlConnection connection)
        {
            using (var cmd = new SqlCommand(@"
SELECT TOP 1 Id
FROM dbo.InventoryGodowns
WHERE IsActive = 1
ORDER BY CASE WHEN UPPER(GodownCode) = 'MAIN' THEN 0 ELSE 1 END, Id", connection))
            {
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToInt32(result);
            }
        }

        private static bool GetIsSaleFromInventoryEnabled(SqlConnection connection)
        {
            using (var cmd = new SqlCommand(@"
IF OBJECT_ID('dbo.RestaurantSettings','U') IS NULL
BEGIN
    SELECT CAST(0 AS bit);
END
ELSE
BEGIN
    SELECT TOP 1
        CASE
            WHEN COL_LENGTH('dbo.RestaurantSettings','IsSaleFromInventory') IS NULL THEN CAST(0 AS bit)
            ELSE CAST(ISNULL(IsSaleFromInventory, 0) AS bit)
        END
    FROM dbo.RestaurantSettings
    ORDER BY Id DESC;
END", connection))
            {
                var val = cmd.ExecuteScalar();
                return val != null && val != DBNull.Value && Convert.ToBoolean(val);
            }
        }
    }
}
