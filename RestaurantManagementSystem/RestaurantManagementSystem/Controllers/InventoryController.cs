using Microsoft.AspNetCore.Mvc;
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
            return View();
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
