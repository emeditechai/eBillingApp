using Microsoft.AspNetCore.Mvc;
using RestaurantManagementSystem.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using Microsoft.Data.SqlClient;
using System;
using System.Linq;

namespace RestaurantManagementSystem.Controllers
{
    public class MasterController : Controller
    {
        private readonly RestaurantDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        
        public MasterController(RestaurantDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // Category List
        public IActionResult CategoryList()
        {
            var categories = _dbContext.Categories.ToList();
            return View(categories);
    }

    // Category Add/Edit/View Form
    public IActionResult CategoryForm(int? id, bool isView = false)
    {
        Category model = new Category { Name = "" };
        if (id.HasValue)
        {
            model = _dbContext.Categories.FirstOrDefault(c => c.Id == id.Value) ?? model;
        }
        
        ViewBag.IsView = isView;
        return View(model);
    }

    [HttpPostAttribute]
    public IActionResult CategoryForm(Category model)
    {
        string resultMessage;
        
        if (model.Id > 0)
        {
            // Update existing category
            var existingCategory = _dbContext.Categories.FirstOrDefault(c => c.Id == model.Id);
            if (existingCategory == null)
            {
                TempData["ResultMessage"] = "Category update failed. Id not found.";
                return RedirectToAction("CategoryList");
            }
            
            existingCategory.Name = model.Name;
            existingCategory.IsActive = model.IsActive;
            _dbContext.SaveChanges();
            resultMessage = "Category updated successfully.";
        }
        else
        {
            // Add new category
            _dbContext.Categories.Add(model);
            _dbContext.SaveChanges();
            resultMessage = "Category added successfully.";
        }
        
        TempData["ResultMessage"] = resultMessage;
        return RedirectToAction("CategoryList");
    }

    // Ingredients List
    public IActionResult IngredientsList()
    {
        var ingredients = _dbContext.Ingredients.ToList();
        
        // If there are no ingredients, seed some sample data
        if (!ingredients.Any())
        {
            _dbContext.Ingredients.AddRange(
                new Ingredients { IngredientsName = "Tomato", DisplayName = "Tomato", Code = "TMT" },
                new Ingredients { IngredientsName = "Cheese", DisplayName = "Cheese", Code = "CHS" }
            );
            _dbContext.SaveChanges();
            ingredients = _dbContext.Ingredients.ToList();
        }
        
        return View(ingredients);
    }

    // Add/Edit/View Form
    public IActionResult IngredientsForm(int? id, bool isView = false)
    {
        Ingredients model = new Ingredients { IngredientsName = "" };
        
        if (id.HasValue)
        {
            model = _dbContext.Ingredients.FirstOrDefault(i => i.Id == id.Value) ?? model;
        }
        
        ViewBag.IsView = isView;
        return View("Ingredients", model);
    }

    [HttpPostAttribute]
    public IActionResult IngredientsForm(Ingredients model)
    {
        if (ModelState.IsValid)
        {
            if (model.Id == 0)
            {
                // Add new ingredient
                _dbContext.Ingredients.Add(model);
                _dbContext.SaveChanges();
                TempData["ResultMessage"] = "Ingredient added successfully.";
            }
            else
            {
                // Update existing ingredient
                var existingIngredient = _dbContext.Ingredients.FirstOrDefault(i => i.Id == model.Id);
                if (existingIngredient != null)
                {
                    existingIngredient.IngredientsName = model.IngredientsName;
                    existingIngredient.DisplayName = model.DisplayName;
                    existingIngredient.Code = model.Code;
                    _dbContext.SaveChanges();
                    TempData["ResultMessage"] = "Ingredient updated successfully.";
                }
                else
                {
                    TempData["ResultMessage"] = "Ingredient update failed. Id not found.";
                }
            }
            return RedirectToAction("IngredientsList");
        }
        return View("Ingredients", model);
    }

        // Counter Master List
        public IActionResult CounterList()
        {
            try
            {
                EnsureCountersTableExists();
                var counters = ReadCounters();
                return View(counters);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load counters: {ex.Message}";
                return View(new List<CounterMaster>());
            }
        }

        // Counter Master Add/Edit/View Form
        public IActionResult CounterForm(int? id, bool isView = false)
        {
            try
            {
                EnsureCountersTableExists();
                var model = new CounterMaster();

                if (id.HasValue && id.Value > 0)
                {
                    model = ReadCounterById(id.Value) ?? model;
                }

                ViewBag.IsView = isView;
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to load counter: {ex.Message}";
                return RedirectToAction(nameof(CounterList));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CounterForm(CounterMaster model, bool isView = false)
        {
            try
            {
                EnsureCountersTableExists();

                model.CounterCode = (model.CounterCode ?? string.Empty).Trim();
                model.CounterName = (model.CounterName ?? string.Empty).Trim();

                // Unique validation for CounterCode
                if (string.IsNullOrWhiteSpace(model.CounterCode))
                {
                    ModelState.AddModelError(nameof(CounterMaster.CounterCode), "Counter Code is required.");
                }

                if (string.IsNullOrWhiteSpace(model.CounterName))
                {
                    ModelState.AddModelError(nameof(CounterMaster.CounterName), "Counter Name is required.");
                }

                if (!string.IsNullOrWhiteSpace(model.CounterCode))
                {
                    var duplicate = CounterCodeExists(model.CounterCode, model.Id);
                    if (duplicate)
                    {
                        ModelState.AddModelError(nameof(CounterMaster.CounterCode), "Counter Code already exists.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.IsView = isView;
                    return View(model);
                }

                if (model.Id > 0)
                {
                    var updated = UpdateCounter(model);
                    TempData["ResultMessage"] = updated ? "Counter updated successfully." : "Counter update failed.";
                }
                else
                {
                    var inserted = InsertCounter(model);
                    TempData["ResultMessage"] = inserted ? "Counter added successfully." : "Counter add failed.";
                }

                return RedirectToAction(nameof(CounterList));
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Failed to save counter: {ex.Message}";
                ViewBag.IsView = isView;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCounterStatus(int id, bool isActive)
        {
            try
            {
                EnsureCountersTableExists();
                var ok = SetCounterActive(id, isActive);
                TempData["ResultMessage"] = ok
                    ? (isActive ? "Counter activated successfully." : "Counter deactivated successfully.")
                    : "Counter status update failed.";
            }
            catch (Exception ex)
            {
                TempData["ResultMessage"] = $"Counter status update failed: {ex.Message}";
            }

            return RedirectToAction(nameof(CounterList));
        }

        private void EnsureCountersTableExists()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var cmd = new SqlCommand(@"
IF OBJECT_ID(N'dbo.Counters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Counters
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Counters PRIMARY KEY,
        CounterCode NVARCHAR(50) NOT NULL,
        CounterName NVARCHAR(120) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Counters_IsActive DEFAULT (1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Counters_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NULL
    );

    CREATE UNIQUE INDEX UX_Counters_CounterCode ON dbo.Counters (CounterCode);
END
ELSE
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UX_Counters_CounterCode'
          AND object_id = OBJECT_ID(N'dbo.Counters')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_Counters_CounterCode ON dbo.Counters (CounterCode);
    END
END
", connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<CounterMaster> ReadCounters()
        {
            var list = new List<CounterMaster>();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
SELECT Id, CounterCode, CounterName, IsActive, CreatedAt, UpdatedAt
FROM dbo.Counters
ORDER BY CounterCode", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new CounterMaster
                        {
                            Id = reader.GetInt32(0),
                            CounterCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            CounterName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IsActive = !reader.IsDBNull(3) && reader.GetBoolean(3),
                            CreatedAt = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4),
                            UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                        });
                    }
                }
            }
            return list;
        }

        private CounterMaster? ReadCounterById(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 Id, CounterCode, CounterName, IsActive, CreatedAt, UpdatedAt
FROM dbo.Counters
WHERE Id = @Id", connection))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new CounterMaster
                        {
                            Id = reader.GetInt32(0),
                            CounterCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            CounterName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IsActive = !reader.IsDBNull(3) && reader.GetBoolean(3),
                            CreatedAt = reader.IsDBNull(4) ? DateTime.UtcNow : reader.GetDateTime(4),
                            UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                        };
                    }
                }
            }
        }

        private bool CounterCodeExists(string code, int excludeId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
SELECT COUNT(1)
FROM dbo.Counters
WHERE UPPER(CounterCode) = UPPER(@Code)
  AND Id <> @ExcludeId", connection))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.Parameters.AddWithValue("@ExcludeId", excludeId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        private bool InsertCounter(CounterMaster model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
INSERT INTO dbo.Counters (CounterCode, CounterName, IsActive, CreatedAt, UpdatedAt)
VALUES (@Code, @Name, @IsActive, SYSUTCDATETIME(), NULL)
", connection))
                {
                    cmd.Parameters.AddWithValue("@Code", model.CounterCode);
                    cmd.Parameters.AddWithValue("@Name", model.CounterName);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool UpdateCounter(CounterMaster model)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
UPDATE dbo.Counters
SET CounterCode = @Code,
    CounterName = @Name,
    IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    cmd.Parameters.AddWithValue("@Id", model.Id);
                    cmd.Parameters.AddWithValue("@Code", model.CounterCode);
                    cmd.Parameters.AddWithValue("@Name", model.CounterName);
                    cmd.Parameters.AddWithValue("@IsActive", model.IsActive);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private bool SetCounterActive(int id, bool isActive)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
UPDATE dbo.Counters
SET IsActive = @IsActive,
    UpdatedAt = SYSUTCDATETIME()
WHERE Id = @Id
", connection))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@IsActive", isActive);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
}
}