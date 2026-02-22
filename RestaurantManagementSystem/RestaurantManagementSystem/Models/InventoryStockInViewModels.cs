using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantManagementSystem.Models
{
    public class InventoryStockInEntryViewModel
    {
        [Required]
        [Display(Name = "Menu Item")]
        public int MenuItemId { get; set; }

        [Required]
        [Display(Name = "Godown")]
        public int GodownId { get; set; }

        [Required]
        [Range(typeof(decimal), "0.001", "9999999")]
        public decimal Quantity { get; set; }

        [Range(typeof(decimal), "0", "9999999")]
        [Display(Name = "Unit Cost")]
        public decimal? UnitCost { get; set; }

        [Display(Name = "Party")]
        public int? PartyId { get; set; }

        [Display(Name = "Low Level Qty")]
        [Range(typeof(decimal), "0", "9999999")]
        public decimal? LowLevelQty { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<SelectListItem> MenuItems { get; set; } = new();
        public List<SelectListItem> Godowns { get; set; } = new();
        public List<SelectListItem> Parties { get; set; } = new();
    }

    public class InventoryStockMovementListItem
    {
        public int Id { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public int? ReferenceId { get; set; }
        public int? OrderId { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public int GodownId { get; set; }
        public string GodownName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public int? PartyId { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
