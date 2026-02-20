using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Models;
using RestaurantManagementSystem.ViewModels;

namespace RestaurantManagementSystem.Controllers
{
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? SubCategoryId { get; set; }
        public string? SubCategoryName { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
    }

    public class MenuQRController : Controller
    {
        private readonly RestaurantDbContext _context;

        public MenuQRController(RestaurantDbContext context)
        {
            _context = context;
        }

        // Public action for displaying menu when QR code is scanned
        [AllowAnonymous]
        public async Task<IActionResult> Menu(int? tableId = null)
        {
            var viewModel = new PublicMenuViewModel();

            // Get restaurant information
            viewModel.RestaurantInfo = GetRestaurantInfo();

            // Use raw SQL to get menu items to avoid EF column issues
            // Show all menu items (both available and unavailable) for public menu
            var menuItemsRaw = await _context.Database.SqlQueryRaw<MenuItemDto>(@"
                SELECT 
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Price,
                    m.ImagePath,
                    m.IsAvailable,
                    m.CategoryId,
                    c.Name as CategoryName,
                    m.SubCategoryId,
                    sc.Name as SubCategoryName,
                    m.MenuItemGroupId as GroupId,
                    mig.itemgroup as GroupName
                FROM MenuItems m
                INNER JOIN Categories c ON m.CategoryId = c.Id
                LEFT JOIN dbo.SubCategories sc ON m.SubCategoryId = sc.Id
                LEFT JOIN dbo.menuitemgroup mig ON m.MenuItemGroupId = mig.ID
                ORDER BY mig.itemgroup, c.Name, sc.Name, m.Name
            ").ToListAsync();

            // Build hierarchical grouping: Group -> Category -> SubCategory -> Items
            var groups = menuItemsRaw
                .GroupBy(m => new { m.GroupId, m.GroupName })
                .Select(group => {
                    var categories = group
                        .GroupBy(m => new { m.CategoryId, m.CategoryName })
                        .Select(g => {
                    // Group items by subcategory (including null subcategory)
                    var allGroupedItems = g.GroupBy(m => new { m.SubCategoryId, m.SubCategoryName });
                    
                    // Items WITH subcategories
                    var subcategoriesWithItems = allGroupedItems
                        .Where(subg => subg.Key.SubCategoryId.HasValue)
                        .Select(subg => new SubCategoryMenuItems
                        {
                            SubCategoryId = subg.Key.SubCategoryId,
                            SubCategoryName = subg.Key.SubCategoryName,
                            MenuItems = subg.Select(m => new PublicMenuItem
                            {
                                MenuItemId = m.Id,
                                Name = m.Name,
                                Description = m.Description ?? "",
                                Price = m.Price,
                                ImageUrl = m.ImagePath,
                                IsAvailable = m.IsAvailable,
                                IsVegetarian = false,
                                IsSpicy = false,
                                CategoryName = m.CategoryName,
                                SubCategoryName = m.SubCategoryName
                            }).ToList()
                        }).ToList();

                    // Items WITHOUT subcategories (show directly under category)
                    var directCategoryItems = allGroupedItems
                        .Where(subg => !subg.Key.SubCategoryId.HasValue)
                        .SelectMany(subg => subg.Select(m => new PublicMenuItem
                        {
                            MenuItemId = m.Id,
                            Name = m.Name,
                            Description = m.Description ?? "",
                            Price = m.Price,
                            ImageUrl = m.ImagePath,
                            IsAvailable = m.IsAvailable,
                            IsVegetarian = false,
                            IsSpicy = false,
                            CategoryName = m.CategoryName,
                            SubCategoryName = m.SubCategoryName
                        })).ToList();

                            return new CategoryMenuItems
                            {
                                CategoryId = g.Key.CategoryId,
                                CategoryName = g.Key.CategoryName,
                                CategoryDescription = "",
                                MenuItems = directCategoryItems,
                                SubCategories = subcategoriesWithItems
                            };
                        })
                        .ToList();

                    return new MenuGroupViewModel
                    {
                        GroupId = group.Key.GroupId,
                        GroupName = string.IsNullOrWhiteSpace(group.Key.GroupName) ? "General" : group.Key.GroupName,
                        Categories = categories
                    };
                })
                .OrderBy(g => g.GroupName)
                .ToList();

            viewModel.MenuGroups = groups;
            // Backwards compatibility: flatten into MenuCategories if only one group or consumer expects categories
            if (!groups.Any())
            {
                viewModel.MenuCategories = new List<CategoryMenuItems>();
            }
            else if (groups.Count == 1)
            {
                viewModel.MenuCategories = groups.First().Categories;
            }

            // Handle table-specific information
            if (tableId.HasValue)
            {
                var table = await _context.Tables.FindAsync(tableId.Value);
                if (table != null)
                {
                    viewModel.TableId = tableId;
                    viewModel.TableName = table.TableName;
                    viewModel.IsTableSpecific = true;
                }
            }

            return View(viewModel);
        }

        // Admin action for QR code management
        public IActionResult Index()
        {
            var viewModel = new QRCodeManagementViewModel();
            viewModel.RestaurantInfo = GetRestaurantInfo();
            
            // Create sample tables for now to avoid DB issues
            viewModel.Tables = new List<Table>
            {
                new Table { Id = 1, TableName = "A1", Capacity = 2, Status = 0 },
                new Table { Id = 2, TableName = "A2", Capacity = 4, Status = 0 },
                new Table { Id = 3, TableName = "B1", Capacity = 6, Status = 0 },
                new Table { Id = 4, TableName = "C1", Capacity = 8, Status = 0 }
            };

            // Get existing QR codes (if you want to store them in database)
            // For now, we'll generate them on demand

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult GenerateQRCode(QRCodeType codeType, int? tableId = null)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                string qrUrl;
                string displayName;

                switch (codeType)
                {
                    case QRCodeType.TableSpecific:
                        if (!tableId.HasValue)
                        {
                            return Json(new { success = false, message = "Table ID is required for table-specific QR codes" });
                        }
                        qrUrl = $"{baseUrl}/MenuQR/Menu?tableId={tableId}";
                        var table = _context.Tables.Find(tableId.Value);
                        displayName = $"Table {table?.TableName ?? tableId.ToString()} Menu";
                        break;
                    case QRCodeType.GeneralMenu:
                    default:
                        qrUrl = $"{baseUrl}/MenuQR/Menu";
                        displayName = "Restaurant Menu";
                        break;
                }

                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                
                // Generate QR code as byte array
                var qrCodeBytes = qrCode.GetGraphic(20);
                var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

                var result = new QRCodeGenerationViewModel
                {
                    QRCodeBase64 = qrCodeBase64,
                    QRCodeUrl = qrUrl,
                    TableId = tableId,
                    TableName = displayName,
                    CodeType = codeType,
                    RestaurantName = GetRestaurantInfo().Name,
                    ContactNumber = GetRestaurantInfo().ContactNumber,
                    Address = GetRestaurantInfo().Address
                };

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error generating QR code: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult DownloadQRCode(QRCodeType codeType, int? tableId = null)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                string qrUrl;
                string fileName;

                switch (codeType)
                {
                    case QRCodeType.TableSpecific:
                        if (!tableId.HasValue)
                        {
                            return BadRequest("Table ID is required");
                        }
                        qrUrl = $"{baseUrl}/MenuQR/Menu?tableId={tableId}";
                        var table = _context.Tables.Find(tableId.Value);
                        fileName = $"Table_{table?.TableName ?? tableId.ToString()}_QR.png";
                        break;
                    case QRCodeType.GeneralMenu:
                    default:
                        qrUrl = $"{baseUrl}/MenuQR/Menu";
                        fileName = "Restaurant_Menu_QR.png";
                        break;
                }

                var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                
                // Generate high-quality QR code
                var imageBytes = qrCode.GetGraphic(20);

                return File(imageBytes, "image/png", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error downloading QR code: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateBulkTableQRCodes()
        {
            try
            {
                var tables = await _context.Tables.OrderBy(t => t.TableName).ToListAsync();
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var zipEntries = new List<(string fileName, byte[] content)>();

                foreach (var table in tables)
                {
                    var qrUrl = $"{baseUrl}/MenuQR/Menu?tableId={table.Id}";
                    
                    var qrGenerator = new QRCodeGenerator();
                    var qrCodeData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                    var qrCode = new PngByteQRCode(qrCodeData);
                    var imageBytes = qrCode.GetGraphic(20);
                    
                    zipEntries.Add((fileName: $"Table_{table.TableName}_QR.png", content: imageBytes));
                }

                // For now, return success message. In production, you might want to create a ZIP file
                TempData["SuccessMessage"] = $"Generated QR codes for {tables.Count} tables successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating bulk QR codes: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private RestaurantInfo GetRestaurantInfo()
        {
            // Get restaurant information from database settings
            var settings = _context.RestaurantSettings.FirstOrDefault();
            
            if (settings != null)
            {
                return new RestaurantInfo
                {
                    Name = settings.RestaurantName,
                    ContactNumber = settings.PhoneNumber ?? "+1 (555) 123-4567",
                    Address = $"{settings.StreetAddress}, {settings.City}, {settings.State} {settings.Pincode}",
                    Email = settings.Email ?? "",
                    Website = settings.Website ?? "",
                    Description = "Delicious food, exceptional service",
                    LogoUrl = !string.IsNullOrEmpty(settings.LogoPath) ? settings.LogoPath : "/images/logo.png"
                };
            }
            
            // Fallback if no settings found
            return new RestaurantInfo
            {
                Name = "Billing Management Application",
                ContactNumber = "+1 (555) 123-4567",
                Address = "123 Restaurant Street, Food City, FC 12345",
                Email = "info@restaurant.com",
                Website = "www.restaurant.com",
                Description = "Authentic flavors, exceptional service",
                LogoUrl = "/images/logo.png"
            };
        }
    }
}