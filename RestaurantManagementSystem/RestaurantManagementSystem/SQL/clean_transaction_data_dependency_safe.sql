-- ============================================================================
-- Script: Clean Transaction Data (Dependency-Safe)
-- Target: Microsoft SQL Server
--
-- Purpose:
--   Deletes ONLY transactional data while preserving master data.
--
-- Included transactional areas:
--   - Orders + order items + modifiers + merged order tables
--   - Payments + split bills
--   - Kitchen tickets (food & bar stations) + kitchen item comments
--   - BOT (Bar/Beverage Order Ticket) module (BOT_*)
--   - Reservations + waitlist + table turnovers + server assignments
--   - Guest feedback
--   - Online ordering transactions (OnlineOrders* + logs)
--   - Day closing tables (CashierDayOpening/CashierDayClose/DayLockAudit)
--
-- Explicitly preserved (examples):
--   - Tables (master) (rows are NOT deleted; status can be reset)
--   - MenuItems / Modifiers / PaymentMethods / KitchenStations
--   - Users / Roles / Settings / Master config
--
-- Safety:
--   - Requires explicit confirmation flag before it will run.
--   - Uses DELETE (not TRUNCATE) to avoid FK/TRUNCATE limitations.
--   - Supports FULL cleanup or DATE-RANGE cleanup.
--
-- IMPORTANT:
--   Take a full DB backup before running.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-------------------------------------------------------------------------------
-- CONFIGURATION
-------------------------------------------------------------------------------
DECLARE @Confirm NVARCHAR(20) = 'NO';
-- Set to 'DELETE' to actually delete data
-- DECLARE @Confirm NVARCHAR(20) = 'DELETE';

DECLARE @FromDate DATETIME = NULL; -- e.g. '2026-01-01'
DECLARE @ToDate   DATETIME = NULL; -- e.g. '2026-02-01' (exclusive)

-- If provided, @ToDate is treated as an exclusive end boundary.
-- Example: @FromDate='2026-01-01', @ToDate='2026-02-01' deletes January data.

DECLARE @ResetTableStatus BIT = 1;  -- Reset Tables to Available after cleanup
DECLARE @ReseedIdentities BIT = 1;  -- Only applies on FULL cleanup

-------------------------------------------------------------------------------
-- GUARDRAIL
-------------------------------------------------------------------------------
IF @Confirm <> 'DELETE'
BEGIN
	RAISERROR('Refusing to run. Set @Confirm = ''DELETE'' to proceed.', 16, 1);
	RETURN;
END

DECLARE @IsFullCleanup BIT = CASE WHEN @FromDate IS NULL AND @ToDate IS NULL THEN 1 ELSE 0 END;

BEGIN TRANSACTION;
BEGIN TRY
	PRINT '=====================================================';
	PRINT 'Transaction data cleanup STARTED: ' + CONVERT(VARCHAR(19), GETDATE(), 120);
	PRINT 'Mode: ' + CASE WHEN @IsFullCleanup = 1 THEN 'FULL' ELSE 'DATE_RANGE' END;
	PRINT '=====================================================';

	---------------------------------------------------------------------------
	-- Build working sets (Orders are the core dependency root)
	---------------------------------------------------------------------------
	IF OBJECT_ID('tempdb..#OrderIds') IS NOT NULL DROP TABLE #OrderIds;
	CREATE TABLE #OrderIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.Orders','U') IS NOT NULL
	BEGIN
		INSERT INTO #OrderIds (Id)
		SELECT o.Id
		FROM dbo.Orders o
		WHERE
			(@IsFullCleanup = 1)
			OR (
				(@FromDate IS NULL OR o.CreatedAt >= @FromDate)
				AND (@ToDate   IS NULL OR o.CreatedAt <  @ToDate)
			);
	END

	IF OBJECT_ID('tempdb..#OrderItemIds') IS NOT NULL DROP TABLE #OrderItemIds;
	CREATE TABLE #OrderItemIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.OrderItems','U') IS NOT NULL
	BEGIN
		INSERT INTO #OrderItemIds (Id)
		SELECT oi.Id
		FROM dbo.OrderItems oi
		INNER JOIN #OrderIds o ON o.Id = oi.OrderId;
	END

	IF OBJECT_ID('tempdb..#KitchenTicketIds') IS NOT NULL DROP TABLE #KitchenTicketIds;
	CREATE TABLE #KitchenTicketIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.KitchenTickets','U') IS NOT NULL
	BEGIN
		INSERT INTO #KitchenTicketIds (Id)
		SELECT kt.Id
		FROM dbo.KitchenTickets kt
		INNER JOIN #OrderIds o ON o.Id = kt.OrderId;
	END

	IF OBJECT_ID('tempdb..#KitchenTicketItemIds') IS NOT NULL DROP TABLE #KitchenTicketItemIds;
	CREATE TABLE #KitchenTicketItemIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.KitchenTicketItems','U') IS NOT NULL
	BEGIN
		INSERT INTO #KitchenTicketItemIds (Id)
		SELECT kti.Id
		FROM dbo.KitchenTicketItems kti
		INNER JOIN #KitchenTicketIds kt ON kt.Id = kti.KitchenTicketId;
	END

	IF OBJECT_ID('tempdb..#SplitBillIds') IS NOT NULL DROP TABLE #SplitBillIds;
	CREATE TABLE #SplitBillIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.SplitBills','U') IS NOT NULL
	BEGIN
		INSERT INTO #SplitBillIds (Id)
		SELECT sb.Id
		FROM dbo.SplitBills sb
		INNER JOIN #OrderIds o ON o.Id = sb.OrderId;
	END

	IF OBJECT_ID('tempdb..#TurnoverIds') IS NOT NULL DROP TABLE #TurnoverIds;
	CREATE TABLE #TurnoverIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.Orders','U') IS NOT NULL AND OBJECT_ID('dbo.TableTurnovers','U') IS NOT NULL
	BEGIN
		INSERT INTO #TurnoverIds (Id)
		SELECT DISTINCT o.TableTurnoverId
		FROM dbo.Orders o
		INNER JOIN #OrderIds od ON od.Id = o.Id
		WHERE o.TableTurnoverId IS NOT NULL;

		-- On FULL cleanup, also delete all turnovers
		IF @IsFullCleanup = 1
		BEGIN
			INSERT INTO #TurnoverIds (Id)
			SELECT tt.Id
			FROM dbo.TableTurnovers tt
			WHERE NOT EXISTS (SELECT 1 FROM #TurnoverIds x WHERE x.Id = tt.Id);
		END
	END

	IF OBJECT_ID('tempdb..#ReservationIds') IS NOT NULL DROP TABLE #ReservationIds;
	CREATE TABLE #ReservationIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.Reservations','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
		BEGIN
			INSERT INTO #ReservationIds (Id)
			SELECT r.Id FROM dbo.Reservations r;
		END
		ELSE
		BEGIN
			-- Delete reservations referenced by turnovers being removed, plus those in date window
			IF OBJECT_ID('dbo.TableTurnovers','U') IS NOT NULL
			BEGIN
				INSERT INTO #ReservationIds (Id)
				SELECT DISTINCT tt.ReservationId
				FROM dbo.TableTurnovers tt
				INNER JOIN #TurnoverIds t ON t.Id = tt.Id
				WHERE tt.ReservationId IS NOT NULL;
			END

			INSERT INTO #ReservationIds (Id)
			SELECT r.Id
			FROM dbo.Reservations r
			WHERE
				(
					(@FromDate IS NULL OR r.ReservationTime >= @FromDate)
					AND (@ToDate   IS NULL OR r.ReservationTime <  @ToDate)
				)
				AND NOT EXISTS (SELECT 1 FROM #ReservationIds x WHERE x.Id = r.Id);
		END
	END

	IF OBJECT_ID('tempdb..#WaitlistIds') IS NOT NULL DROP TABLE #WaitlistIds;
	CREATE TABLE #WaitlistIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.Waitlist','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
		BEGIN
			INSERT INTO #WaitlistIds (Id)
			SELECT w.Id FROM dbo.Waitlist w;
		END
		ELSE
		BEGIN
			IF OBJECT_ID('dbo.TableTurnovers','U') IS NOT NULL
			BEGIN
				INSERT INTO #WaitlistIds (Id)
				SELECT DISTINCT tt.WaitlistId
				FROM dbo.TableTurnovers tt
				INNER JOIN #TurnoverIds t ON t.Id = tt.Id
				WHERE tt.WaitlistId IS NOT NULL;
			END

			INSERT INTO #WaitlistIds (Id)
			SELECT w.Id
			FROM dbo.Waitlist w
			WHERE
				(
					(@FromDate IS NULL OR w.AddedAt >= @FromDate)
					AND (@ToDate   IS NULL OR w.AddedAt <  @ToDate)
				)
				AND NOT EXISTS (SELECT 1 FROM #WaitlistIds x WHERE x.Id = w.Id);
		END
	END

	---------------------------------------------------------------------------
	-- 1) BOT / Bar transactions (BOT_*)
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '1) Cleaning BOT / Bar transactions...';

	IF OBJECT_ID('dbo.BOT_Payments','U') IS NOT NULL
	BEGIN
		DELETE bp
		FROM dbo.BOT_Payments bp
		INNER JOIN dbo.BOT_Bills bb ON bb.BillID = bp.BillID
		INNER JOIN #OrderIds o ON o.Id = bb.OrderId;
		PRINT '   - BOT_Payments deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.BOT_Bills','U') IS NOT NULL
	BEGIN
		DELETE bb
		FROM dbo.BOT_Bills bb
		INNER JOIN #OrderIds o ON o.Id = bb.OrderId;
		PRINT '   - BOT_Bills deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.BOT_Audit','U') IS NOT NULL
	BEGIN
		-- No FK in setup; delete by BOT_IDs related to deleted orders when possible
		IF OBJECT_ID('dbo.BOT_Header','U') IS NOT NULL
		BEGIN
			DELETE ba
			FROM dbo.BOT_Audit ba
			INNER JOIN dbo.BOT_Header bh ON bh.BOT_ID = ba.BOT_ID
			INNER JOIN #OrderIds o ON o.Id = bh.OrderId;
		END
		ELSE
		BEGIN
			-- Fallback
			DELETE FROM dbo.BOT_Audit;
		END
		PRINT '   - BOT_Audit deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.BOT_Detail','U') IS NOT NULL
	BEGIN
		DELETE bd
		FROM dbo.BOT_Detail bd
		INNER JOIN dbo.BOT_Header bh ON bh.BOT_ID = bd.BOT_ID
		INNER JOIN #OrderIds o ON o.Id = bh.OrderId;
		PRINT '   - BOT_Detail deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.BOT_Header','U') IS NOT NULL
	BEGIN
		DELETE bh
		FROM dbo.BOT_Header bh
		INNER JOIN #OrderIds o ON o.Id = bh.OrderId;
		PRINT '   - BOT_Header deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 2) Kitchen / Food + Bar stations (KitchenTickets*)
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '2) Cleaning Kitchen tickets/comments...';

	IF OBJECT_ID('dbo.KitchenItemComments','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
		BEGIN
			DELETE FROM dbo.KitchenItemComments;
		END
		ELSE
		BEGIN
			-- KitchenItemComments has DATETIME2 CreatedAt in the setup script
			DELETE kic
			FROM dbo.KitchenItemComments kic
			LEFT JOIN #OrderIds o ON o.Id = kic.OrderId
			WHERE o.Id IS NOT NULL
			   OR ((@FromDate IS NULL OR kic.CreatedAt >= @FromDate) AND (@ToDate IS NULL OR kic.CreatedAt < @ToDate));
		END
		PRINT '   - KitchenItemComments deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.KitchenTicketItemModifiers','U') IS NOT NULL
	BEGIN
		DELETE ktim
		FROM dbo.KitchenTicketItemModifiers ktim
		INNER JOIN #KitchenTicketItemIds kti ON kti.Id = ktim.KitchenTicketItemId;
		PRINT '   - KitchenTicketItemModifiers deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.KitchenTicketItems','U') IS NOT NULL
	BEGIN
		DELETE kti
		FROM dbo.KitchenTicketItems kti
		INNER JOIN #KitchenTicketIds kt ON kt.Id = kti.KitchenTicketId;
		PRINT '   - KitchenTicketItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.KitchenTickets','U') IS NOT NULL
	BEGIN
		DELETE kt
		FROM dbo.KitchenTickets kt
		INNER JOIN #KitchenTicketIds x ON x.Id = kt.Id;
		PRINT '   - KitchenTickets deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 3) Payments + split bills
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '3) Cleaning Payments / Split bills...';

	IF OBJECT_ID('dbo.SplitBillItems','U') IS NOT NULL
	BEGIN
		DELETE sbi
		FROM dbo.SplitBillItems sbi
		INNER JOIN #SplitBillIds sb ON sb.Id = sbi.SplitBillId;
		PRINT '   - SplitBillItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.SplitBills','U') IS NOT NULL
	BEGIN
		DELETE sb
		FROM dbo.SplitBills sb
		INNER JOIN #SplitBillIds x ON x.Id = sb.Id;
		PRINT '   - SplitBills deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.Payments','U') IS NOT NULL
	BEGIN
		DELETE p
		FROM dbo.Payments p
		INNER JOIN #OrderIds o ON o.Id = p.OrderId;
		PRINT '   - Payments deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 4) Online ordering transactions (NOT master/config)
	--     IMPORTANT: OnlineOrders may FK to Orders (SyncedToLocalOrderId),
	--     so we must delete these BEFORE deleting Orders.
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '4) Cleaning Online ordering transactions...';

	IF OBJECT_ID('dbo.WebhookEvents','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
			DELETE FROM dbo.WebhookEvents;
		ELSE
			DELETE we FROM dbo.WebhookEvents we
			WHERE (@FromDate IS NULL OR we.CreatedAt >= @FromDate)
			  AND (@ToDate   IS NULL OR we.CreatedAt <  @ToDate);
		PRINT '   - WebhookEvents deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.ApiCallLogs','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
			DELETE FROM dbo.ApiCallLogs;
		ELSE
			DELETE acl FROM dbo.ApiCallLogs acl
			WHERE (@FromDate IS NULL OR acl.CreatedAt >= @FromDate)
			  AND (@ToDate   IS NULL OR acl.CreatedAt <  @ToDate);
		PRINT '   - ApiCallLogs deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('tempdb..#OnlineOrderIds') IS NOT NULL DROP TABLE #OnlineOrderIds;
	CREATE TABLE #OnlineOrderIds (Id INT NOT NULL PRIMARY KEY);

	IF OBJECT_ID('dbo.OnlineOrders','U') IS NOT NULL
	BEGIN
		INSERT INTO #OnlineOrderIds (Id)
		SELECT oo.Id
		FROM dbo.OnlineOrders oo
		WHERE
			(@IsFullCleanup = 1)
			OR (
				((@FromDate IS NULL OR oo.CreatedAt >= @FromDate) AND (@ToDate IS NULL OR oo.CreatedAt < @ToDate))
				OR (oo.SyncedToLocalOrderId IS NOT NULL AND EXISTS (SELECT 1 FROM #OrderIds o WHERE o.Id = oo.SyncedToLocalOrderId))
			);

		IF OBJECT_ID('dbo.OnlineOrderItemModifiers','U') IS NOT NULL
		BEGIN
			DELETE oom
			FROM dbo.OnlineOrderItemModifiers oom
			INNER JOIN dbo.OnlineOrderItems ooi ON ooi.Id = oom.OnlineOrderItemId
			INNER JOIN #OnlineOrderIds x ON x.Id = ooi.OnlineOrderId;
			PRINT '   - OnlineOrderItemModifiers deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
		END

		IF OBJECT_ID('dbo.OnlineOrderItems','U') IS NOT NULL
		BEGIN
			DELETE ooi
			FROM dbo.OnlineOrderItems ooi
			INNER JOIN #OnlineOrderIds x ON x.Id = ooi.OnlineOrderId;
			PRINT '   - OnlineOrderItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
		END

		DELETE oo
		FROM dbo.OnlineOrders oo
		INNER JOIN #OnlineOrderIds x ON x.Id = oo.Id;
		PRINT '   - OnlineOrders deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 5) Orders
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '5) Cleaning Orders...';

	-- Some deployments may have extra transactional tables that reference Orders
	-- (example: dbo.OrderPaymentSessions). To keep this script dependency-safe
	-- without hard-coding every possible table, delete from ANY table that has
	-- a single-column FK to dbo.Orders before deleting from dbo.Orders.
	IF OBJECT_ID('dbo.Orders','U') IS NOT NULL
	BEGIN
		PRINT '   - Deleting additional child rows referencing Orders (FK scan)...';
		DECLARE @ChildSchema SYSNAME;
		DECLARE @ChildTable SYSNAME;
		DECLARE @ChildColumn SYSNAME;
		DECLARE @ChildDeleteSql NVARCHAR(MAX);
		DECLARE @Deleted INT;

		DECLARE ChildFkCursor CURSOR FAST_FORWARD FOR
		WITH OrderFks AS (
			SELECT
				fk.object_id AS FkObjectId,
				s.name AS SchemaName,
				t.name AS TableName
			FROM sys.foreign_keys fk
			INNER JOIN sys.tables t ON t.object_id = fk.parent_object_id
			INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
			WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.Orders')
		),
		FkColCounts AS (
			SELECT fkc.constraint_object_id AS FkObjectId, COUNT(*) AS ColCount
			FROM sys.foreign_key_columns fkc
			GROUP BY fkc.constraint_object_id
		)
		SELECT
			ofks.SchemaName,
			ofks.TableName,
			c.name AS ColumnName
		FROM OrderFks ofks
		INNER JOIN FkColCounts cc ON cc.FkObjectId = ofks.FkObjectId AND cc.ColCount = 1
		INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = ofks.FkObjectId
		INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
		WHERE NOT (ofks.SchemaName = N'dbo' AND ofks.TableName = N'Orders');

		OPEN ChildFkCursor;
		FETCH NEXT FROM ChildFkCursor INTO @ChildSchema, @ChildTable, @ChildColumn;

		WHILE @@FETCH_STATUS = 0
		BEGIN
			SET @Deleted = 0;
			SET @ChildDeleteSql =
				N'DELETE t ' +
				N'FROM ' + QUOTENAME(@ChildSchema) + N'.' + QUOTENAME(@ChildTable) + N' t ' +
				N'INNER JOIN #OrderIds o ON t.' + QUOTENAME(@ChildColumn) + N' = o.Id; ' +
				N'SELECT @Deleted = @@ROWCOUNT;';

			BEGIN TRY
				EXEC sys.sp_executesql @ChildDeleteSql, N'@Deleted INT OUTPUT', @Deleted OUTPUT;
				IF @Deleted > 0
					PRINT '     * ' + @ChildSchema + N'.' + @ChildTable + N': ' + CAST(@Deleted AS VARCHAR(20));
			END TRY
			BEGIN CATCH
				-- If a child table itself has deeper dependencies, it may fail here.
				-- In that case, we rely on earlier explicit deletes or you can add
				-- that table explicitly above.
				PRINT '     ! Skipped ' + @ChildSchema + N'.' + @ChildTable + N' (delete failed): ' + ERROR_MESSAGE();
			END CATCH

			FETCH NEXT FROM ChildFkCursor INTO @ChildSchema, @ChildTable, @ChildColumn;
		END

		CLOSE ChildFkCursor;
		DEALLOCATE ChildFkCursor;
	END

	IF OBJECT_ID('dbo.OrderAuditTrail','U') IS NOT NULL
	BEGIN
		DELETE oat
		FROM dbo.OrderAuditTrail oat
		INNER JOIN #OrderIds o ON o.Id = oat.OrderId;
		PRINT '   - OrderAuditTrail deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.OrderTables','U') IS NOT NULL
	BEGIN
		DELETE ot
		FROM dbo.OrderTables ot
		INNER JOIN #OrderIds o ON o.Id = ot.OrderId;
		PRINT '   - OrderTables deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.OrderItemModifiers','U') IS NOT NULL
	BEGIN
		DELETE oim
		FROM dbo.OrderItemModifiers oim
		INNER JOIN #OrderItemIds oi ON oi.Id = oim.OrderItemId;
		PRINT '   - OrderItemModifiers deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.OrderItems','U') IS NOT NULL
	BEGIN
		DELETE oi
		FROM dbo.OrderItems oi
		INNER JOIN #OrderIds o ON o.Id = oi.OrderId;
		PRINT '   - OrderItems deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.Orders','U') IS NOT NULL
	BEGIN
		DELETE o
		FROM dbo.Orders o
		INNER JOIN #OrderIds x ON x.Id = o.Id;
		PRINT '   - Orders deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 6) Table service / bookings
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '6) Cleaning Table service / Reservation bookings...';

	IF OBJECT_ID('dbo.TableTurnovers','U') IS NOT NULL
	BEGIN
		DELETE tt
		FROM dbo.TableTurnovers tt
		INNER JOIN #TurnoverIds t ON t.Id = tt.Id;
		PRINT '   - TableTurnovers deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.ServerAssignments','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
		BEGIN
			DELETE FROM dbo.ServerAssignments;
		END
		ELSE
		BEGIN
			DELETE sa
			FROM dbo.ServerAssignments sa
			WHERE
				(@FromDate IS NULL OR sa.AssignedAt >= @FromDate)
				AND (@ToDate   IS NULL OR sa.AssignedAt <  @ToDate);
		END
		PRINT '   - ServerAssignments deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.Reservations','U') IS NOT NULL
	BEGIN
		DELETE r
		FROM dbo.Reservations r
		INNER JOIN #ReservationIds x ON x.Id = r.Id;
		PRINT '   - Reservations deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.Waitlist','U') IS NOT NULL
	BEGIN
		DELETE w
		FROM dbo.Waitlist w
		INNER JOIN #WaitlistIds x ON x.Id = w.Id;
		PRINT '   - Waitlist deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 7) Guest feedback
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '7) Cleaning Guest feedback...';

	IF OBJECT_ID('dbo.GuestFeedback','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
		BEGIN
			DELETE FROM dbo.GuestFeedback;
		END
		ELSE
		BEGIN
			DELETE gf
			FROM dbo.GuestFeedback gf
			WHERE
				(@FromDate IS NULL OR gf.CreatedAt >= @FromDate)
				AND (@ToDate   IS NULL OR gf.CreatedAt <  @ToDate);
		END
		PRINT '   - GuestFeedback deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 8) Day closing / cashier close (transactional)
	---------------------------------------------------------------------------
	PRINT '';
	PRINT '8) Cleaning Day Closing (Cashier opening/close/audit)...';

	IF OBJECT_ID('dbo.DayLockAudit','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
			DELETE FROM dbo.DayLockAudit;
		ELSE
			DELETE dla FROM dbo.DayLockAudit dla
			WHERE (@FromDate IS NULL OR CAST(dla.BusinessDate AS DATETIME) >= @FromDate)
			  AND (@ToDate   IS NULL OR CAST(dla.BusinessDate AS DATETIME) <  @ToDate);
		PRINT '   - DayLockAudit deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.CashierDayClose','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
			DELETE FROM dbo.CashierDayClose;
		ELSE
			DELETE cdc FROM dbo.CashierDayClose cdc
			WHERE (@FromDate IS NULL OR CAST(cdc.BusinessDate AS DATETIME) >= @FromDate)
			  AND (@ToDate   IS NULL OR CAST(cdc.BusinessDate AS DATETIME) <  @ToDate);
		PRINT '   - CashierDayClose deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	IF OBJECT_ID('dbo.CashierDayOpening','U') IS NOT NULL
	BEGIN
		IF @IsFullCleanup = 1
			DELETE FROM dbo.CashierDayOpening;
		ELSE
			DELETE cdo FROM dbo.CashierDayOpening cdo
			WHERE (@FromDate IS NULL OR CAST(cdo.BusinessDate AS DATETIME) >= @FromDate)
			  AND (@ToDate   IS NULL OR CAST(cdo.BusinessDate AS DATETIME) <  @ToDate);
		PRINT '   - CashierDayOpening deleted: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
	END

	---------------------------------------------------------------------------
	-- 9) Reset table status (DO NOT delete master rows)
	---------------------------------------------------------------------------
	IF @ResetTableStatus = 1 AND OBJECT_ID('dbo.Tables','U') IS NOT NULL
	BEGIN
		PRINT '';
		PRINT '9) Resetting table status (master rows preserved)...';

		-- IMPORTANT:
		-- SQL Server validates column names at parse/compile time, even inside IF blocks.
		-- To avoid "Invalid column name" errors on deployments with different schemas,
		-- we use dynamic SQL and only include columns that exist.
		DECLARE @TableResetSql NVARCHAR(MAX) = N'';
		DECLARE @SetList NVARCHAR(MAX) = N'';
		DECLARE @Rows INT = 0;

		IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tables') AND name = N'Status')
			SET @SetList = @SetList + CASE WHEN @SetList = N'' THEN N'' ELSE N', ' END + N'Status = 0';

		IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tables') AND name = N'IsAvailable')
			SET @SetList = @SetList + CASE WHEN @SetList = N'' THEN N'' ELSE N', ' END + N'IsAvailable = 1';

		IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tables') AND name = N'LastOccupiedAt')
			SET @SetList = @SetList + CASE WHEN @SetList = N'' THEN N'' ELSE N', ' END + N'LastOccupiedAt = NULL';

		IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tables') AND name = N'LastOccupied')
			SET @SetList = @SetList + CASE WHEN @SetList = N'' THEN N'' ELSE N', ' END + N'LastOccupied = NULL';

		IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Tables') AND name = N'CurrentOrder')
			SET @SetList = @SetList + CASE WHEN @SetList = N'' THEN N'' ELSE N', ' END + N'CurrentOrder = NULL';

		IF @SetList <> N''
		BEGIN
			SET @TableResetSql = N'UPDATE dbo.Tables SET ' + @SetList + N';';
			EXEC sys.sp_executesql @TableResetSql;
			SET @Rows = @@ROWCOUNT;
			PRINT '   - Tables reset rows affected: ' + CAST(@Rows AS VARCHAR(20));
		END
		ELSE
		BEGIN
			PRINT '   - Tables reset skipped (no matching columns found).';
		END
	END

	---------------------------------------------------------------------------
	-- 10) Identity reseed (only if FULL cleanup)
	---------------------------------------------------------------------------
	IF @ReseedIdentities = 1 AND @IsFullCleanup = 1
	BEGIN
		PRINT '';
		PRINT '10) Reseeding identities (FULL cleanup only)...';

		IF OBJECT_ID('dbo.Orders','U') IS NOT NULL DBCC CHECKIDENT ('dbo.Orders', RESEED, 0);
		IF OBJECT_ID('dbo.OrderItems','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OrderItems', RESEED, 0);
		IF OBJECT_ID('dbo.OrderItemModifiers','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OrderItemModifiers', RESEED, 0);
		IF OBJECT_ID('dbo.Payments','U') IS NOT NULL DBCC CHECKIDENT ('dbo.Payments', RESEED, 0);
		IF OBJECT_ID('dbo.SplitBills','U') IS NOT NULL DBCC CHECKIDENT ('dbo.SplitBills', RESEED, 0);
		IF OBJECT_ID('dbo.SplitBillItems','U') IS NOT NULL DBCC CHECKIDENT ('dbo.SplitBillItems', RESEED, 0);
		IF OBJECT_ID('dbo.KitchenTickets','U') IS NOT NULL DBCC CHECKIDENT ('dbo.KitchenTickets', RESEED, 0);
		IF OBJECT_ID('dbo.KitchenTicketItems','U') IS NOT NULL DBCC CHECKIDENT ('dbo.KitchenTicketItems', RESEED, 0);
		IF OBJECT_ID('dbo.KitchenTicketItemModifiers','U') IS NOT NULL DBCC CHECKIDENT ('dbo.KitchenTicketItemModifiers', RESEED, 0);

		IF OBJECT_ID('dbo.TableTurnovers','U') IS NOT NULL DBCC CHECKIDENT ('dbo.TableTurnovers', RESEED, 0);
		IF OBJECT_ID('dbo.Reservations','U') IS NOT NULL DBCC CHECKIDENT ('dbo.Reservations', RESEED, 0);
		IF OBJECT_ID('dbo.Waitlist','U') IS NOT NULL DBCC CHECKIDENT ('dbo.Waitlist', RESEED, 0);
		IF OBJECT_ID('dbo.ServerAssignments','U') IS NOT NULL DBCC CHECKIDENT ('dbo.ServerAssignments', RESEED, 0);

		IF OBJECT_ID('dbo.GuestFeedback','U') IS NOT NULL DBCC CHECKIDENT ('dbo.GuestFeedback', RESEED, 0);

		IF OBJECT_ID('dbo.BOT_Header','U') IS NOT NULL DBCC CHECKIDENT ('dbo.BOT_Header', RESEED, 0);
		IF OBJECT_ID('dbo.BOT_Detail','U') IS NOT NULL DBCC CHECKIDENT ('dbo.BOT_Detail', RESEED, 0);
		IF OBJECT_ID('dbo.BOT_Audit','U') IS NOT NULL DBCC CHECKIDENT ('dbo.BOT_Audit', RESEED, 0);
		IF OBJECT_ID('dbo.BOT_Bills','U') IS NOT NULL DBCC CHECKIDENT ('dbo.BOT_Bills', RESEED, 0);
		IF OBJECT_ID('dbo.BOT_Payments','U') IS NOT NULL DBCC CHECKIDENT ('dbo.BOT_Payments', RESEED, 0);

		IF OBJECT_ID('dbo.OnlineOrders','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OnlineOrders', RESEED, 0);
		IF OBJECT_ID('dbo.OnlineOrderItems','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OnlineOrderItems', RESEED, 0);
		IF OBJECT_ID('dbo.OnlineOrderItemModifiers','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OnlineOrderItemModifiers', RESEED, 0);
		IF OBJECT_ID('dbo.WebhookEvents','U') IS NOT NULL DBCC CHECKIDENT ('dbo.WebhookEvents', RESEED, 0);
		IF OBJECT_ID('dbo.ApiCallLogs','U') IS NOT NULL DBCC CHECKIDENT ('dbo.ApiCallLogs', RESEED, 0);

		IF OBJECT_ID('dbo.OrderTables','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OrderTables', RESEED, 0);
		IF OBJECT_ID('dbo.OrderAuditTrail','U') IS NOT NULL DBCC CHECKIDENT ('dbo.OrderAuditTrail', RESEED, 0);
		IF OBJECT_ID('dbo.KitchenItemComments','U') IS NOT NULL DBCC CHECKIDENT ('dbo.KitchenItemComments', RESEED, 0);

		IF OBJECT_ID('dbo.CashierDayOpening','U') IS NOT NULL DBCC CHECKIDENT ('dbo.CashierDayOpening', RESEED, 0);
		IF OBJECT_ID('dbo.CashierDayClose','U') IS NOT NULL DBCC CHECKIDENT ('dbo.CashierDayClose', RESEED, 0);
		IF OBJECT_ID('dbo.DayLockAudit','U') IS NOT NULL DBCC CHECKIDENT ('dbo.DayLockAudit', RESEED, 0);

		PRINT '   - Identity reseed complete.';
	END

	COMMIT TRANSACTION;

	PRINT '';
	PRINT '=====================================================';
	PRINT 'Transaction data cleanup COMPLETED: ' + CONVERT(VARCHAR(19), GETDATE(), 120);
	PRINT '=====================================================';
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0
		ROLLBACK TRANSACTION;

	DECLARE @Err NVARCHAR(4000) = ERROR_MESSAGE();
	DECLARE @ErrLine INT = ERROR_LINE();
	DECLARE @ErrNum INT = ERROR_NUMBER();

	PRINT '=====================================================';
	PRINT 'Transaction data cleanup FAILED';
	PRINT 'Error ' + CAST(@ErrNum AS VARCHAR(20)) + ' at line ' + CAST(@ErrLine AS VARCHAR(20));
	PRINT @Err;
	PRINT '=====================================================';

	THROW;
END CATCH;
