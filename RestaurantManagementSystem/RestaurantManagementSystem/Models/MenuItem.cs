using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "PLU Code is required")]
        [Display(Name = "PLU Code")]
        public string PLUCode { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 9999.99, ErrorMessage = "Price must be between ₹0.01 and ₹9,999.99")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Takeout Price")]
        [Range(0.01, 9999.99, ErrorMessage = "Takeout Price must be between ₹0.01 and ₹9,999.99")]
        [DataType(DataType.Currency)]
        public decimal? TakeoutPrice { get; set; }

        [Display(Name = "Delivery Price")]
        [Range(0.01, 9999.99, ErrorMessage = "Delivery Price must be between ₹0.01 and ₹9,999.99")]
        [DataType(DataType.Currency)]
        public decimal? DeliveryPrice { get; set; }

        [Display(Name = "Unit of Measurement")]
        public int? UOMId { get; set; }
        public string UOMName { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        public Category Category { get; set; }

        [Display(Name = "Sub Category")]
        public int? SubCategoryId { get; set; }
        public SubCategory SubCategory { get; set; }

    [Display(Name = "Item Group")]
    public int? MenuItemGroupId { get; set; }
    public MenuItemGroup MenuItemGroup { get; set; }

        [Display(Name = "Image Path")]
        public string ImagePath { get; set; }

        [Required]
        [Display(Name = "Is Available")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Preparation Time (minutes)")]
        [Range(1, 120, ErrorMessage = "Preparation time must be between 1 and 120 minutes")]
        public int PreparationTimeMinutes { get; set; }

        [Display(Name = "Calorie Count")]
        public int? CalorieCount { get; set; }

        [Display(Name = "Is Featured")]
        public bool IsFeatured { get; set; }

        [Display(Name = "Is Special")]
        public bool IsSpecial { get; set; }

        [Display(Name = "Discount Percentage")]
        [Range(0, 100, ErrorMessage = "Discount must be between 0% and 100%")]
        public decimal? DiscountPercentage { get; set; }

        [Display(Name = "Kitchen Station")]
        public int? KitchenStationId { get; set; }
        
        [Display(Name = "Target GP %")]
        [Range(0, 90, ErrorMessage = "Target GP % must be between 0% and 90%")]
        public decimal? TargetGP { get; set; }
        
        [Display(Name = "Item Type")]
        public string ItemType { get; set; }
        
        [Display(Name = "GST Percentage")]
        [Range(0, 100, ErrorMessage = "GST Percentage must be between 0% and 100%")]
        public decimal? GSTPercentage { get; set; }

    [Display(Name = "Is GST Applicable")]
    public bool IsGstApplicable { get; set; } = true; // New flag controlling GSTPercentage applicability

    [Display(Name = "Not Available")]
    public bool NotAvailable { get; set; } = false; // Separate flag while retaining existing IsAvailable for backward compatibility
        
        // Navigation properties
        public virtual ICollection<MenuItemAllergen> Allergens { get; set; } = new List<MenuItemAllergen>();
        public virtual ICollection<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
        public virtual ICollection<MenuItemModifier> Modifiers { get; set; } = new List<MenuItemModifier>();
    }
}
