using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using RestaurantManagementSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantManagementSystem.Controllers
{
    public class InventoryController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public InventoryController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult Dashboard()
        {
            try
            {
                var model = BuildDashboardModel();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load inventory dashboard: {ex.Message}";
                return View(new InventoryDashboardViewModel());
            }
        }

        public IActionResult GodownMaster()
        {
            try
            {
                var godowns = ReadGodowns();
                return View("GodownList", godowns);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load godowns: {ex.Message}";
                return View("GodownList", new List<InventoryGodown>());
            }
        }

        public IActionResult GodownList()
        {
            return RedirectToAction(nameof(GodownMaster));
        }

        public IActionResult PartyMaster()
        {
            try
            {
                var parties = ReadParties();
                return View("PartyList", parties);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load parties: {ex.Message}";
                return View("PartyList", new List<InventoryParty>());
            }
        }

        public IActionResult PartyList()
        {
            return RedirectToAction(nameof(PartyMaster));
        }

        public IActionResult StockIn()
        {
            try
            {
                var movements = ReadStockInMovements();
                return View("StockInList", movements);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load stock-in data: {ex.Message}";
                return View("StockInList", new List<InventoryStockMovementListItem>());
            }
        }

        public IActionResult StockInForm()
        {
            try
            {
                var model = new InventoryStockInEntryViewModel
                {
                    Quantity = 1,
                    UnitCost = 0,
                    LowLevelQty = 0
                };

                PopulateStockInLookups(model);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load stock-in form: {ex.Message}";
                return RedirectToAction(nameof(StockIn));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult StockInForm(InventoryStockInEntryViewModel model)
        {
            try
            {
                model.InvoiceNo = string.IsNullOrWhiteSpace(model.InvoiceNo) ? null : model.InvoiceNo.Trim();
                model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

                if (model.MenuItemId <= 0)
                {
                    ModelState.AddModelError(nameof(InventoryStockInEntryViewModel.MenuItemId), "Menu Item is required.");
                }

                if (model.GodownId <= 0)
                {
                    ModelState.AddModelError(nameof(InventoryStockInEntryViewModel.GodownId), "Godown is required.");
                }

                if (model.Quantity <= 0)
                {
                    ModelState.AddModelError(nameof(InventoryStockInEntryViewModel.Quantity), "Quantity should be greater than zero.");
                }

                if (model.UnitCost < 0)
                {
                    ModelState.AddModelError(nameof(InventoryStockInEntryViewModel.UnitCost), "Unit Cost should be at least zero.");
                }

                if (model.LowLevelQty < 0)
                {
                    ModelState.AddModelError(nameof(InventoryStockInEntryViewModel.LowLevelQty), "Low Level Qty should be at least zero.");
                }

                if (!ModelState.IsValid)
                {
                    PopulateStockInLookups(model);
                    return View(model);
                }

                model.Notes = BuildStockInNotes(model.InvoiceNo, model.Notes);

                var saved = SaveStockIn(model);
                TempData["ResultMessage"] = saved ? "Stock in entry saved successfully." : "Stock in entry failed.";
                return RedirectToAction(nameof(StockIn));
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to save stock-in entry: {ex.Message}";
                PopulateStockInLookups(model);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public IActionResult DeleteStockIn(int id)
        {
            try
            {
                if (id <= 0)
                {
                    TempData["ResultMessage"] = "Invalid stock-in entry.";
                    return RedirectToAction(nameof(StockIn));
                }

                var cancelled = DeleteStockInMovement(id, out var resultMessage);
                TempData["ResultMessage"] = cancelled
                    ? "Stock-in entry cancelled successfully. Inventory synced."
                    : (string.IsNullOrWhiteSpace(resultMessage)
                        ? "Stock-in entry not found or already cancelled."
                        : resultMessage);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to cancel stock-in entry: {ex.Message}";
            }

            return RedirectToAction(nameof(StockIn));
        }

        public IActionResult PartyForm(int? id, bool isView = false)
        {
            try
            {
                var model = new InventoryParty();

                if (id.HasValue && id.Value > 0)
                {
                    model = ReadPartyById(id.Value) ?? model;
                }

                SetPartyTypeOptions(model.PartyType);
                ViewBag.IsView = isView;
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load party: {ex.Message}";
                return RedirectToAction(nameof(PartyMaster));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PartyForm(InventoryParty model, bool isView = false)
        {
            try
            {
                model.PartyCode = (model.PartyCode ?? string.Empty).Trim();
                model.PartyName = (model.PartyName ?? string.Empty).Trim();
                model.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
                model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
                model.PartyType = (model.PartyType ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(model.PartyCode))
                {
                    ModelState.AddModelError(nameof(InventoryParty.PartyCode), "Party Code is required.");
                }

                if (string.IsNullOrWhiteSpace(model.PartyName))
                {
                    ModelState.AddModelError(nameof(InventoryParty.PartyName), "Party Name is required.");
                }

                if (!IsValidPartyType(model.PartyType))
                {
                    ModelState.AddModelError(nameof(InventoryParty.PartyType), "Invalid Party Type selected.");
                }

                if (!string.IsNullOrWhiteSpace(model.PartyCode) && PartyCodeExists(model.PartyCode, model.Id))
                {
                    ModelState.AddModelError(nameof(InventoryParty.PartyCode), "Party Code already exists.");
                }

                if (!ModelState.IsValid)
                {
                    SetPartyTypeOptions(model.PartyType);
                    ViewBag.IsView = isView;
                    return View(model);
                }

                if (model.Id > 0)
                {
                    var updated = UpdateParty(model);
                    TempData["ResultMessage"] = updated ? "Party updated successfully." : "Party update failed.";
                }
                else
                {
                    var inserted = InsertParty(model);
                    TempData["ResultMessage"] = inserted ? "Party added successfully." : "Party add failed.";
                }

                return RedirectToAction(nameof(PartyMaster));
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to save party: {ex.Message}";
                SetPartyTypeOptions(model.PartyType);
                ViewBag.IsView = isView;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetPartyStatus(int id, bool isActive)
        {
            try
            {
                var updated = SetPartyActive(id, isActive);
                TempData["ResultMessage"] = updated
                    ? (isActive ? "Party activated successfully." : "Party deactivated successfully.")
                    : "Party status update failed.";
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Party status update failed: {ex.Message}";
            }

            return RedirectToAction(nameof(PartyMaster));
        }

        public IActionResult GodownForm(int? id, bool isView = false)
        {
            try
            {
                var model = new InventoryGodown();

                if (id.HasValue && id.Value > 0)
                {
                    model = ReadGodownById(id.Value) ?? model;
                }

                ViewBag.IsView = isView;
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load godown: {ex.Message}";
                return RedirectToAction(nameof(GodownMaster));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GodownForm(InventoryGodown model, bool isView = false)
        {
            try
            {
                model.GodownCode = (model.GodownCode ?? string.Empty).Trim();
                model.GodownName = (model.GodownName ?? string.Empty).Trim();
                model.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();

                if (string.IsNullOrWhiteSpace(model.GodownCode))
                {
                    ModelState.AddModelError(nameof(InventoryGodown.GodownCode), "Godown Code is required.");
                }

                if (string.IsNullOrWhiteSpace(model.GodownName))
                {
                    ModelState.AddModelError(nameof(InventoryGodown.GodownName), "Godown Name is required.");
                }

                if (!string.IsNullOrWhiteSpace(model.GodownCode) && GodownCodeExists(model.GodownCode, model.Id))
                {
                    ModelState.AddModelError(nameof(InventoryGodown.GodownCode), "Godown Code already exists.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.IsView = isView;
                    return View(model);
                }

                if (model.Id > 0)
                {
                    var updated = UpdateGodown(model);
                    TempData["ResultMessage"] = updated ? "Godown updated successfully." : "Godown update failed.";
                }
                else
                {
                    var inserted = InsertGodown(model);
                    TempData["ResultMessage"] = inserted ? "Godown added successfully." : "Godown add failed.";
                }

                return RedirectToAction(nameof(GodownMaster));
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to save godown: {ex.Message}";
                ViewBag.IsView = isView;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetGodownStatus(int id, bool isActive)
        {
            try
            {
                var updated = SetGodownActive(id, isActive);
                TempData["ResultMessage"] = updated
                    ? (isActive ? "Godown activated successfully." : "Godown deactivated successfully.")
                    : "Godown status update failed.";
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Godown status update failed: {ex.Message}";
            }

            return RedirectToAction(nameof(GodownMaster));
        }

        private List<InventoryGodown> ReadGodowns()
        {
            var list = new List<InventoryGodown>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT Id, GodownCode, GodownName, Address, IsActive, CreatedAt, UpdatedAt
FROM dbo.InventoryGodowns
ORDER BY GodownCode", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new InventoryGodown
                        {
                            Id = reader.GetInt32(0),
                            GodownCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            GodownName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Address = reader.IsDBNull(3) ? null : reader.GetString(3),
                            IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4),
                            CreatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
                            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                        });
                    }
                }
            }

            return list;
        }

        private List<InventoryStockMovementListItem> ReadStockInMovements()
        {
            var list = new List<InventoryStockMovementListItem>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var godownNameMap = ReadGodownNameMap(connection);
                using (var command = new SqlCommand(@"
SELECT TOP 300
    sm.Id,
    sm.MovementType,
    sm.ReferenceType,
    sm.ReferenceId,
    sm.OrderId,
    sm.MenuItemId,
    ISNULL(mi.Name, CONCAT('MenuItem#', sm.MenuItemId)) AS MenuItemName,
    sm.GodownId,
    ISNULL(g.GodownName, '') AS GodownName,
    sm.Quantity,
    sm.UnitCost,
    sm.PartyId,
    ISNULL(p.PartyName, '') AS PartyName,
    sm.Notes,
    sm.CreatedBy,
    sm.CreatedAt
FROM dbo.InventoryStockMovements sm
LEFT JOIN dbo.MenuItems mi ON mi.Id = sm.MenuItemId
LEFT JOIN dbo.InventoryGodowns g ON g.Id = sm.GodownId
LEFT JOIN dbo.InventoryParties p ON p.Id = sm.PartyId
WHERE UPPER(sm.ReferenceType) = 'STOCK_IN'
ORDER BY sm.CreatedAt DESC, sm.Id DESC", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var godownId = ToInt32(reader.GetValue(7));
                        var godownName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
                        var rawNotes = reader.IsDBNull(13) ? null : reader.GetString(13);
                        var (invoiceNo, notes) = ExtractInvoiceNoAndNotes(rawNotes);

                        list.Add(new InventoryStockMovementListItem
                        {
                            Id = ToInt32(reader.GetValue(0)),
                            MovementType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            ReferenceType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            ReferenceId = ToNullableInt32(reader.GetValue(3)),
                            OrderId = ToNullableInt32(reader.GetValue(4)),
                            MenuItemId = ToInt32(reader.GetValue(5)),
                            MenuItemName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            GodownId = godownId,
                            GodownName = ResolveGodownName(godownId, godownName, godownNameMap),
                            Quantity = reader.IsDBNull(9) ? 0 : ToDecimal(reader.GetValue(9)),
                            UnitCost = ToNullableDecimal(reader.GetValue(10)),
                            PartyId = ToNullableInt32(reader.GetValue(11)),
                            PartyName = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                            InvoiceNo = invoiceNo,
                            Notes = notes,
                            CreatedBy = ToNullableInt32(reader.GetValue(14)),
                            CreatedAt = reader.IsDBNull(15) ? DateTime.UtcNow : reader.GetDateTime(15)
                        });
                    }
                }
            }

            return list;
        }

        private bool SaveStockIn(InventoryStockInEntryViewModel model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var createdBy = GetCurrentUserIdOrDefault();

                        int movementId;
                        using (var movementCommand = new SqlCommand(@"
INSERT INTO dbo.InventoryStockMovements
(
    MovementType, ReferenceType, ReferenceId, OrderId, MenuItemId, GodownId, Quantity,
    UnitCost, PartyId, Notes, CreatedBy, CreatedAt
)
VALUES
(
    'IN', 'STOCK_IN', NULL, NULL, @MenuItemId, @GodownId, @Quantity,
    @UnitCost, @PartyId, @Notes, @CreatedBy, SYSUTCDATETIME()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
                        {
                            movementCommand.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                            movementCommand.Parameters.AddWithValue("@GodownId", model.GodownId);
                            movementCommand.Parameters.AddWithValue("@Quantity", model.Quantity);
                            movementCommand.Parameters.AddWithValue("@UnitCost", model.UnitCost);
                            movementCommand.Parameters.AddWithValue("@PartyId", (object?)model.PartyId ?? DBNull.Value);
                            movementCommand.Parameters.AddWithValue("@Notes", (object?)model.Notes ?? DBNull.Value);
                            movementCommand.Parameters.AddWithValue("@CreatedBy", createdBy);
                            movementId = Convert.ToInt32(movementCommand.ExecuteScalar());
                        }

                        using (var stockCommand = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.InventoryStock WITH (UPDLOCK, HOLDLOCK) WHERE MenuItemId = @MenuItemId AND GodownId = @GodownId)
BEGIN
    UPDATE dbo.InventoryStock
    SET QuantityOnHand = ISNULL(QuantityOnHand, 0) + @Quantity,
        UpdatedAt = SYSUTCDATETIME(),
        LowLevelQty = @LowLevelQty
    WHERE MenuItemId = @MenuItemId AND GodownId = @GodownId;
END
ELSE
BEGIN
    INSERT INTO dbo.InventoryStock (MenuItemId, GodownId, QuantityOnHand, UpdatedAt, LowLevelQty)
    VALUES (@MenuItemId, @GodownId, @Quantity, SYSUTCDATETIME(), @LowLevelQty);
END

UPDATE dbo.InventoryStockMovements
SET ReferenceId = @MovementId
WHERE Id = @MovementId;", connection, transaction))
                        {
                            stockCommand.Parameters.AddWithValue("@MenuItemId", model.MenuItemId);
                            stockCommand.Parameters.AddWithValue("@GodownId", model.GodownId);
                            stockCommand.Parameters.AddWithValue("@Quantity", model.Quantity);
                            stockCommand.Parameters.AddWithValue("@LowLevelQty", model.LowLevelQty);
                            stockCommand.Parameters.AddWithValue("@MovementId", movementId);
                            stockCommand.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private bool DeleteStockInMovement(int movementId, out string resultMessage)
        {
            resultMessage = string.Empty;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int menuItemId;
                        int godownId;
                        decimal quantity;
                                                decimal unitCost;
                                                string? existingNotes;
                                                int createdBy;
                                                DateTime createdAtUtc;

                        using (var readCommand = new SqlCommand(@"
SELECT TOP 1 MenuItemId, GodownId, Quantity, UnitCost, Notes, CreatedAt
FROM dbo.InventoryStockMovements WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @Id
  AND UPPER(MovementType) = 'IN'
  AND UPPER(ReferenceType) = 'STOCK_IN'", connection, transaction))
                        {
                            readCommand.Parameters.AddWithValue("@Id", movementId);
                            using (var reader = readCommand.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    transaction.Rollback();
                                    return false;
                                }

                                menuItemId = ToInt32(reader.GetValue(0));
                                godownId = ToInt32(reader.GetValue(1));
                                quantity = reader.IsDBNull(2) ? 0 : ToDecimal(reader.GetValue(2));
                                unitCost = reader.IsDBNull(3) ? 0 : ToDecimal(reader.GetValue(3));
                                existingNotes = reader.IsDBNull(4) ? null : reader.GetString(4);
                                createdAtUtc = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5);
                            }
                        }

                        if (createdAtUtc < DateTime.UtcNow.AddMinutes(-3))
                        {
                            transaction.Rollback();
                            resultMessage = "Stock-in entry can only be cancelled within 3 minutes of creation.";
                            return false;
                        }

                        createdBy = GetCurrentUserIdOrDefault();

                        using (var cancelCommand = new SqlCommand(@"
UPDATE dbo.InventoryStockMovements
SET MovementType = 'CANCELLED',
    ReferenceType = 'STOCK_IN_CANCELLED',
    Notes = @CancelledNotes
WHERE Id = @Id", connection, transaction))
                        {
                            var cancelledNotes = string.IsNullOrWhiteSpace(existingNotes)
                                ? $"Cancelled stock-in entry #{movementId}"
                                : $"{existingNotes} | Cancelled stock-in entry #{movementId}";

                            cancelCommand.Parameters.AddWithValue("@Id", movementId);
                            cancelCommand.Parameters.AddWithValue("@CancelledNotes", cancelledNotes);
                            var affected = cancelCommand.ExecuteNonQuery();
                            if (affected <= 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        using (var reverseMovementCommand = new SqlCommand(@"
INSERT INTO dbo.InventoryStockMovements
(
    MovementType, ReferenceType, ReferenceId, OrderId, MenuItemId, GodownId, Quantity,
    UnitCost, PartyId, Notes, CreatedBy, CreatedAt
)
VALUES
(
    'OUT', 'STOCK_IN_CANCELLED', @ReferenceId, NULL, @MenuItemId, @GodownId, @Quantity,
    @UnitCost, NULL, @Notes, @CreatedBy, SYSUTCDATETIME()
)", connection, transaction))
                        {
                            reverseMovementCommand.Parameters.AddWithValue("@ReferenceId", movementId);
                            reverseMovementCommand.Parameters.AddWithValue("@MenuItemId", menuItemId);
                            reverseMovementCommand.Parameters.AddWithValue("@GodownId", godownId);
                            reverseMovementCommand.Parameters.AddWithValue("@Quantity", quantity);
                            reverseMovementCommand.Parameters.AddWithValue("@UnitCost", unitCost);
                            reverseMovementCommand.Parameters.AddWithValue("@Notes", $"Reversal for cancelled stock-in entry #{movementId}");
                            reverseMovementCommand.Parameters.AddWithValue("@CreatedBy", createdBy);
                            reverseMovementCommand.ExecuteNonQuery();
                        }

                        using (var stockCommand = new SqlCommand(@"
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
END", connection, transaction))
                        {
                            stockCommand.Parameters.AddWithValue("@MenuItemId", menuItemId);
                            stockCommand.Parameters.AddWithValue("@GodownId", godownId);
                            stockCommand.Parameters.AddWithValue("@Quantity", quantity);
                            stockCommand.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void PopulateStockInLookups(InventoryStockInEntryViewModel model)
        {
            model.MenuItems = new List<SelectListItem>();
            model.Godowns = new List<SelectListItem>();
            model.Parties = new List<SelectListItem>();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var menuItemsCommand = new SqlCommand(@"
SELECT Id, Name
FROM dbo.MenuItems
WHERE ISNULL(IsAvailable, 1) = 1
ORDER BY Name", connection))
                using (var reader = menuItemsCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = reader.GetInt32(0);
                        model.MenuItems.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = reader.IsDBNull(1) ? $"MenuItem#{id}" : reader.GetString(1),
                            Selected = id == model.MenuItemId
                        });
                    }
                }

                using (var godownsCommand = new SqlCommand(@"
SELECT Id, GodownName
FROM dbo.InventoryGodowns
WHERE IsActive = 1
ORDER BY GodownName", connection))
                using (var reader = godownsCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = reader.GetInt32(0);
                        model.Godowns.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = reader.IsDBNull(1) ? $"Godown#{id}" : reader.GetString(1),
                            Selected = id == model.GodownId
                        });
                    }
                }

                using (var partiesCommand = new SqlCommand(@"
SELECT Id, PartyName
FROM dbo.InventoryParties
WHERE IsActive = 1
ORDER BY PartyName", connection))
                using (var reader = partiesCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = reader.GetInt32(0);
                        model.Parties.Add(new SelectListItem
                        {
                            Value = id.ToString(),
                            Text = reader.IsDBNull(1) ? $"Party#{id}" : reader.GetString(1),
                            Selected = model.PartyId.HasValue && id == model.PartyId.Value
                        });
                    }
                }
            }
        }

        private static string? BuildStockInNotes(string? invoiceNo, string? notes)
        {
            var normalizedInvoice = string.IsNullOrWhiteSpace(invoiceNo) ? null : invoiceNo.Trim();
            var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            if (string.IsNullOrWhiteSpace(normalizedInvoice))
            {
                return normalizedNotes;
            }

            if (string.IsNullOrWhiteSpace(normalizedNotes))
            {
                return $"Invoice No: {normalizedInvoice}";
            }

            return $"Invoice No: {normalizedInvoice} | Note: {normalizedNotes}";
        }

        private static (string? InvoiceNo, string? Notes) ExtractInvoiceNoAndNotes(string? rawNotes)
        {
            if (string.IsNullOrWhiteSpace(rawNotes))
            {
                return (null, null);
            }

            var text = rawNotes.Trim();
            const string invoicePrefix = "Invoice No:";
            const string noteDelimiter = "| Note:";

            if (!text.StartsWith(invoicePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return (null, text);
            }

            var payload = text.Substring(invoicePrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return (null, null);
            }

            var delimiterIndex = payload.IndexOf(noteDelimiter, StringComparison.OrdinalIgnoreCase);
            if (delimiterIndex < 0)
            {
                return (payload, null);
            }

            var invoiceNo = payload.Substring(0, delimiterIndex).Trim();
            var noteText = payload.Substring(delimiterIndex + noteDelimiter.Length).Trim();

            return
            (
                string.IsNullOrWhiteSpace(invoiceNo) ? null : invoiceNo,
                string.IsNullOrWhiteSpace(noteText) ? null : noteText
            );
        }

        private int GetCurrentUserIdOrDefault()
        {
            try
            {
                var claim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(claim, out var userId))
                {
                    return userId;
                }
            }
            catch
            {
            }

            return 1;
        }

        private InventoryDashboardViewModel BuildDashboardModel()
        {
            var model = new InventoryDashboardViewModel();

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var godownNameMap = ReadGodownNameMap(connection);

                using (var metricsCommand = new SqlCommand(@"
SELECT
    ISNULL((SELECT COUNT(1) FROM dbo.InventoryStock), 0) AS TotalTrackedItems,
    ISNULL((SELECT SUM(ISNULL(QuantityOnHand, 0)) FROM dbo.InventoryStock), 0) AS TotalQuantityOnHand,
    ISNULL((SELECT COUNT(1) FROM dbo.InventoryStock WHERE LowLevelQty IS NOT NULL AND ISNULL(QuantityOnHand, 0) <= LowLevelQty), 0) AS LowStockItemsCount,
    ISNULL((SELECT SUM(ISNULL(Quantity, 0)) FROM dbo.InventoryStockMovements WHERE UPPER(MovementType) = 'IN' AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)), 0) AS TodayStockInQuantity,
    ISNULL((SELECT SUM(ISNULL(Quantity, 0)) FROM dbo.InventoryStockMovements WHERE UPPER(MovementType) = 'OUT' AND CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)), 0) AS TodayStockOutQuantity,
    ISNULL((SELECT COUNT(1) FROM dbo.InventoryGodowns WHERE IsActive = 1), 0) AS ActiveGodownsCount,
    ISNULL((SELECT COUNT(1) FROM dbo.InventoryParties WHERE IsActive = 1), 0) AS ActivePartiesCount", connection))
                using (var reader = metricsCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.TotalTrackedItems = ToInt32(reader.GetValue(0));
                        model.TotalQuantityOnHand = ToDecimal(reader.GetValue(1));
                        model.LowStockItemsCount = ToInt32(reader.GetValue(2));
                        model.TodayStockInQuantity = ToDecimal(reader.GetValue(3));
                        model.TodayStockOutQuantity = ToDecimal(reader.GetValue(4));
                        model.ActiveGodownsCount = ToInt32(reader.GetValue(5));
                        model.ActivePartiesCount = ToInt32(reader.GetValue(6));
                    }
                }

                using (var lowStockCommand = new SqlCommand(@"
SELECT TOP 10
    s.MenuItemId,
    ISNULL(mi.Name, CONCAT('MenuItem#', s.MenuItemId)) AS MenuItemName,
    s.GodownId,
    ISNULL(g.GodownName, '') AS GodownName,
    ISNULL(s.QuantityOnHand, 0) AS QuantityOnHand,
    s.LowLevelQty
FROM dbo.InventoryStock s
LEFT JOIN dbo.MenuItems mi ON mi.Id = s.MenuItemId
LEFT JOIN dbo.InventoryGodowns g ON g.Id = s.GodownId
WHERE s.LowLevelQty IS NOT NULL
  AND ISNULL(s.QuantityOnHand, 0) <= s.LowLevelQty
ORDER BY (s.LowLevelQty - ISNULL(s.QuantityOnHand, 0)) DESC, mi.Name", connection))
                using (var reader = lowStockCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var godownId = ToInt32(reader.GetValue(2));
                        var godownName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                        model.LowStockItems.Add(new InventoryLowStockItem
                        {
                            MenuItemId = ToInt32(reader.GetValue(0)),
                            MenuItemName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            GodownId = godownId,
                            GodownName = ResolveGodownName(godownId, godownName, godownNameMap),
                            QuantityOnHand = reader.IsDBNull(4) ? 0 : ToDecimal(reader.GetValue(4)),
                            LowLevelQty = ToNullableDecimal(reader.GetValue(5))
                        });
                    }
                }

                using (var itemWiseStockCommand = new SqlCommand(@"
SELECT TOP 20
    s.MenuItemId,
    ISNULL(mi.Name, CONCAT('MenuItem#', s.MenuItemId)) AS MenuItemName,
    SUM(ISNULL(s.QuantityOnHand, 0)) AS TotalQuantityOnHand,
    COUNT(1) AS GodownCount,
    ISNULL(
        STUFF((
            SELECT ', ' + ISNULL(NULLIF(LTRIM(RTRIM(g2.GodownName)), ''), CONCAT('Godown ', s2.GodownId))
            FROM dbo.InventoryStock s2
            LEFT JOIN dbo.InventoryGodowns g2 ON g2.Id = s2.GodownId
            WHERE s2.MenuItemId = s.MenuItemId
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
        '-'
    ) AS GodownNames,
    SUM(CASE WHEN s.LowLevelQty IS NOT NULL AND ISNULL(s.QuantityOnHand, 0) <= s.LowLevelQty THEN 1 ELSE 0 END) AS LowStockGodownCount,
    MAX(s.UpdatedAt) AS LastUpdatedAt
FROM dbo.InventoryStock s
LEFT JOIN dbo.MenuItems mi ON mi.Id = s.MenuItemId
GROUP BY s.MenuItemId, mi.Name
ORDER BY SUM(ISNULL(s.QuantityOnHand, 0)) ASC, mi.Name", connection))
                using (var reader = itemWiseStockCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        model.ItemWiseStocks.Add(new InventoryItemWiseStockInfo
                        {
                            MenuItemId = ToInt32(reader.GetValue(0)),
                            MenuItemName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            TotalQuantityOnHand = reader.IsDBNull(2) ? 0 : ToDecimal(reader.GetValue(2)),
                            GodownCount = ToInt32(reader.GetValue(3)),
                            GodownNames = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            LowStockGodownCount = ToInt32(reader.GetValue(5)),
                            LastUpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                        });
                    }
                }
            }

            return model;
        }

        private static int ToInt32(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? ToNullableInt32(object value)
        {
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static decimal ToDecimal(object value)
        {
            return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }

        private static decimal? ToNullableDecimal(object value)
        {
            return value == null || value == DBNull.Value ? null : Convert.ToDecimal(value);
        }

        private static string ResolveGodownName(int godownId, string? currentName, Dictionary<int, string> godownNameMap)
        {
            var normalizedName = (currentName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedName)
                && !string.Equals(normalizedName, godownId.ToString(), StringComparison.OrdinalIgnoreCase)
                && !normalizedName.StartsWith("Godown#", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedName;
            }

            if (godownNameMap.TryGetValue(godownId, out var mappedName) && !string.IsNullOrWhiteSpace(mappedName))
            {
                return mappedName;
            }

            return godownId > 0 ? $"Godown {godownId}" : "-";
        }

        private static Dictionary<int, string> ReadGodownNameMap(SqlConnection connection)
        {
            var map = new Dictionary<int, string>();

            using (var command = new SqlCommand(@"
SELECT Id, GodownName
FROM dbo.InventoryGodowns", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = ToInt32(reader.GetValue(0));
                    if (id <= 0)
                    {
                        continue;
                    }

                    var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    map[id] = name.Trim();
                }
            }

            return map;
        }

        private InventoryGodown? ReadGodownById(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT TOP 1 Id, GodownCode, GodownName, Address, IsActive, CreatedAt, UpdatedAt
FROM dbo.InventoryGodowns
WHERE Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new InventoryGodown
                        {
                            Id = reader.GetInt32(0),
                            GodownCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            GodownName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Address = reader.IsDBNull(3) ? null : reader.GetString(3),
                            IsActive = !reader.IsDBNull(4) && reader.GetBoolean(4),
                            CreatedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5),
                            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                        };
                    }
                }
            }
        }

        private bool GodownCodeExists(string godownCode, int excludeId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.InventoryGodowns
WHERE UPPER(GodownCode) = UPPER(@GodownCode)
  AND Id <> @ExcludeId", connection))
                {
                    command.Parameters.AddWithValue("@GodownCode", godownCode);
                    command.Parameters.AddWithValue("@ExcludeId", excludeId);
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        private bool InsertGodown(InventoryGodown model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
INSERT INTO dbo.InventoryGodowns (GodownCode, GodownName, Address, IsActive, CreatedAt, UpdatedAt)
VALUES (@GodownCode, @GodownName, @Address, @IsActive, SYSUTCDATETIME(), NULL)
", connection))
                {
                    command.Parameters.AddWithValue("@GodownCode", model.GodownCode);
                    command.Parameters.AddWithValue("@GodownName", model.GodownName);
                    command.Parameters.AddWithValue("@Address", (object?)model.Address ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", model.IsActive);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool UpdateGodown(InventoryGodown model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
UPDATE dbo.InventoryGodowns
SET GodownCode = @GodownCode,
    GodownName = @GodownName,
    Address = @Address,
    IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    command.Parameters.AddWithValue("@Id", model.Id);
                    command.Parameters.AddWithValue("@GodownCode", model.GodownCode);
                    command.Parameters.AddWithValue("@GodownName", model.GodownName);
                    command.Parameters.AddWithValue("@Address", (object?)model.Address ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", model.IsActive);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool SetGodownActive(int id, bool isActive)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
UPDATE dbo.InventoryGodowns
SET IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@IsActive", isActive);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private List<InventoryParty> ReadParties()
        {
            var list = new List<InventoryParty>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT Id, PartyCode, PartyName, PhoneNumber, Email, Address, IsActive, CreatedAt, UpdatedAt, PartyType
FROM dbo.InventoryParties
ORDER BY PartyCode", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new InventoryParty
                        {
                            Id = reader.GetInt32(0),
                            PartyCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            PartyName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsActive = !reader.IsDBNull(6) && reader.GetBoolean(6),
                            CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                            UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                            PartyType = reader.IsDBNull(9) ? "Vendor" : reader.GetString(9)
                        });
                    }
                }
            }

            return list;
        }

        private InventoryParty? ReadPartyById(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT TOP 1 Id, PartyCode, PartyName, PhoneNumber, Email, Address, IsActive, CreatedAt, UpdatedAt, PartyType
FROM dbo.InventoryParties
WHERE Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new InventoryParty
                        {
                            Id = reader.GetInt32(0),
                            PartyCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            PartyName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Address = reader.IsDBNull(5) ? null : reader.GetString(5),
                            IsActive = !reader.IsDBNull(6) && reader.GetBoolean(6),
                            CreatedAt = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                            UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                            PartyType = reader.IsDBNull(9) ? "Vendor" : reader.GetString(9)
                        };
                    }
                }
            }
        }

        private bool PartyCodeExists(string partyCode, int excludeId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.InventoryParties
WHERE UPPER(PartyCode) = UPPER(@PartyCode)
  AND Id <> @ExcludeId", connection))
                {
                    command.Parameters.AddWithValue("@PartyCode", partyCode);
                    command.Parameters.AddWithValue("@ExcludeId", excludeId);
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        private bool InsertParty(InventoryParty model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
INSERT INTO dbo.InventoryParties (PartyCode, PartyName, PhoneNumber, Email, Address, IsActive, CreatedAt, UpdatedAt, PartyType)
VALUES (@PartyCode, @PartyName, @PhoneNumber, @Email, @Address, @IsActive, SYSUTCDATETIME(), NULL, @PartyType)
", connection))
                {
                    command.Parameters.AddWithValue("@PartyCode", model.PartyCode);
                    command.Parameters.AddWithValue("@PartyName", model.PartyName);
                    command.Parameters.AddWithValue("@PhoneNumber", (object?)model.PhoneNumber ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Email", (object?)model.Email ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Address", (object?)model.Address ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", model.IsActive);
                    command.Parameters.AddWithValue("@PartyType", model.PartyType);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool UpdateParty(InventoryParty model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
UPDATE dbo.InventoryParties
SET PartyCode = @PartyCode,
    PartyName = @PartyName,
    PhoneNumber = @PhoneNumber,
    Email = @Email,
    Address = @Address,
    IsActive = @IsActive,
    PartyType = @PartyType,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    command.Parameters.AddWithValue("@Id", model.Id);
                    command.Parameters.AddWithValue("@PartyCode", model.PartyCode);
                    command.Parameters.AddWithValue("@PartyName", model.PartyName);
                    command.Parameters.AddWithValue("@PhoneNumber", (object?)model.PhoneNumber ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Email", (object?)model.Email ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Address", (object?)model.Address ?? DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", model.IsActive);
                    command.Parameters.AddWithValue("@PartyType", model.PartyType);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool SetPartyActive(int id, bool isActive)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(@"
UPDATE dbo.InventoryParties
SET IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@IsActive", isActive);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        private static bool IsValidPartyType(string partyType)
        {
            return string.Equals(partyType, "Vendor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(partyType, "Suppliers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(partyType, "Traders", StringComparison.OrdinalIgnoreCase);
        }

        private void SetPartyTypeOptions(string? selectedPartyType)
        {
            var partyTypes = new[] { "Vendor", "Suppliers", "Traders" };
            ViewBag.PartyTypes = partyTypes
                .Select(type => new SelectListItem
                {
                    Value = type,
                    Text = type,
                    Selected = string.Equals(type, selectedPartyType, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }
    }
}
