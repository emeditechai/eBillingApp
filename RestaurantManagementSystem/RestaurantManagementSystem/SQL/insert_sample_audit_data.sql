-- Insert sample audit trail data for testing
-- This creates some example audit records to demonstrate the audit trail functionality

DECLARE @OrderId INT = (SELECT TOP 1 Id FROM Orders ORDER BY Id DESC);
DECLARE @UserId INT = (SELECT TOP 1 Id FROM Users WHERE username = 'admin');
DECLARE @OrderNumber NVARCHAR(50) = (SELECT TOP 1 OrderNumber FROM Orders WHERE Id = @OrderId);

IF @OrderId IS NOT NULL AND @UserId IS NOT NULL
BEGIN
    -- Sample: Order Created
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Create', 'Order', @OrderId, NULL, NULL, 'Order Type: Dine In, Table: 5', @UserId, 'admin', DATEADD(MINUTE, -120, GETDATE()), '127.0.0.1', 'Order created from POS');

    -- Sample: Item Added
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Add', 'OrderItem', NULL, 'MenuItem', NULL, 'Chicken Biryani x2', @UserId, 'admin', DATEADD(MINUTE, -115, GETDATE()), '127.0.0.1', 'Item added to order');

    -- Sample: Item Updated
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Update', 'OrderItem', NULL, 'Quantity', '2', '3', @UserId, 'admin', DATEADD(MINUTE, -110, GETDATE()), '127.0.0.1', 'Quantity updated');

    -- Sample: Order Fired to Kitchen
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Fire', 'Order', @OrderId, 'Status', 'Pending', 'Fired', @UserId, 'admin', DATEADD(MINUTE, -105, GETDATE()), '127.0.0.1', 'Order sent to kitchen');

    -- Sample: Payment Added
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Add', 'Payment', NULL, 'Amount', NULL, '₹450.00 (Cash)', @UserId, 'admin', DATEADD(MINUTE, -30, GETDATE()), '127.0.0.1', 'Cash payment received');

    -- Sample: Order Completed
    INSERT INTO OrderAuditTrail (OrderId, OrderNumber, Action, EntityType, EntityId, FieldName, OldValue, NewValue, ChangedBy, ChangedByName, ChangedDate, IPAddress, AdditionalInfo)
    VALUES (@OrderId, @OrderNumber, 'Complete', 'Order', @OrderId, 'Status', 'Fired', 'Completed', @UserId, 'admin', DATEADD(MINUTE, -25, GETDATE()), '127.0.0.1', 'Order marked as complete');

    PRINT 'Sample audit trail data inserted successfully';
    PRINT 'Order ID: ' + CAST(@OrderId AS NVARCHAR);
    PRINT 'Order Number: ' + @OrderNumber;
END
ELSE
BEGIN
    PRINT 'ERROR: No orders or users found in the database';
    PRINT 'Please create some orders first to generate audit trail data';
END
