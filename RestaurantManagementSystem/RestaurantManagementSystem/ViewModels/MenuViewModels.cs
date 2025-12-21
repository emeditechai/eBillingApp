using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RestaurantManagementSystem.Models;

namespace RestaurantManagementSystem.ViewModels
{
    public class MenuItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "PLU Code is required")]
        [Display(Name = "PLU Code")]
        [StringLength(20, ErrorMessage = "PLU Code cannot exceed 20 characters")]
        public string PLUCode { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 10000, ErrorMessage = "Price must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Takeout Price")]
        [Range(0.01, 10000, ErrorMessage = "Takeout Price must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal? TakeoutPrice { get; set; }

        [Display(Name = "Delivery Price")]
        [Range(0.01, 10000, ErrorMessage = "Delivery Price must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal? DeliveryPrice { get; set; }

        [Display(Name = "Room Service Price")]
        [Range(0.01, 10000, ErrorMessage = "Room Service Price must be greater than 0")]
        [DataType(DataType.Currency)]
        public decimal? RoomServicePrice { get; set; }

        [Display(Name = "Unit of Measurement")]
        public int? UOMId { get; set; }

        public string UOMName { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

    [Required(ErrorMessage = "Sub Category is required")]
    [Display(Name = "Sub Category")]
    public int? SubCategoryId { get; set; }

    [Required(ErrorMessage = "Item Group is required")]
    [Display(Name = "Item Group")]
    public int? MenuItemGroupId { get; set; }

        [Display(Name = "Image")]
        public string ImagePath { get; set; }

        [Display(Name = "Upload Image")]
        public IFormFile ImageFile { get; set; }

        [Display(Name = "Available")]
        public bool IsAvailable { get; set; } = true;

    [Required(ErrorMessage = "Preparation time is required")]
    [Display(Name = "Preparation Time (minutes)")]
    [Range(1, 300, ErrorMessage = "Preparation time must be between 1 and 300 minutes")]
    public int PreparationTimeMinutes { get; set; } = 1;

        [Display(Name = "Calorie Count")]
        [Range(0, 10000, ErrorMessage = "Calorie count must be between 0 and 10000")]
        public int? CalorieCount { get; set; }

        [Display(Name = "Featured Item")]
        public bool IsFeatured { get; set; }

        [Display(Name = "Special")]
        public bool IsSpecial { get; set; }

        [Display(Name = "Discount (%)")]
        [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100 percent")]
        public decimal? DiscountPercentage { get; set; }

        [Display(Name = "Kitchen Station")]
        public int? KitchenStationId { get; set; }
        
        [Display(Name = "GST Percentage")]
        [Range(0, 100, ErrorMessage = "GST Percentage must be between 0% and 100%")]
        public decimal? GSTPercentage { get; set; }

    [Display(Name = "Is GST Applicable")]
    public bool IsGstApplicable { get; set; } = true; // Controls whether GSTPercentage value is used

    [Display(Name = "Not Available")]
    public bool NotAvailable { get; set; } = false; // Inverse-style flag separate from IsAvailable for clarity

        // Related data
        [Display(Name = "Allergens")]
        public List<int> SelectedAllergens { get; set; } = new List<int>();

        [Display(Name = "Ingredients")]
        public List<MenuItemIngredientViewModel> Ingredients { get; set; } = new List<MenuItemIngredientViewModel>();

        [Display(Name = "Modifiers")]
        public List<int> SelectedModifiers { get; set; } = new List<int>();

        // Dictionary to hold modifier prices (ModifierId -> Price)
        public Dictionary<int, decimal> ModifierPrices { get; set; } = new Dictionary<int, decimal>();
    }

    public class MenuItemIngredientViewModel
    {
        public int IngredientId { get; set; }
        
        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, 10000, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }
        
        [Required(ErrorMessage = "Unit is required")]
        [StringLength(20, ErrorMessage = "Unit cannot exceed 20 characters")]
        public string Unit { get; set; }
        
        public bool IsOptional { get; set; }
        
        [StringLength(200, ErrorMessage = "Instructions cannot exceed 200 characters")]
        public string Instructions { get; set; }
    }
    
    // Renamed to avoid conflicts with RecipeViewModel in RecipeViewModels.cs
    public class MenuRecipeViewModel
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Menu Item is required")]
        public int MenuItemId { get; set; }
        
        public string MenuItemName { get; set; }
        
        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; }
        
        [Display(Name = "Preparation Instructions")]
        [Required(ErrorMessage = "Preparation instructions are required")]
        public string PreparationInstructions { get; set; }
        
        [Display(Name = "Cooking Instructions")]
        [Required(ErrorMessage = "Cooking instructions are required")]
        public string CookingInstructions { get; set; }
        
        [Display(Name = "Plating Instructions")]
        public string PlatingInstructions { get; set; }
        
        [Range(1, 100, ErrorMessage = "Yield must be between 1 and 100")]
        [Required(ErrorMessage = "Yield is required")]
        public int Yield { get; set; }
        
        [Display(Name = "Preparation Time (minutes)")]
        [Range(1, 300, ErrorMessage = "Preparation time must be between 1 and 300 minutes")]
        [Required(ErrorMessage = "Preparation time is required")]
        public int PreparationTimeMinutes { get; set; }
        
        [Display(Name = "Cooking Time (minutes)")]
        [Range(0, 300, ErrorMessage = "Cooking time must be between 0 and 300 minutes")]
        [Required(ErrorMessage = "Cooking time is required")]
        public int CookingTimeMinutes { get; set; }
        
        public int? CreatedById { get; set; }
        
        public string Notes { get; set; }
        
        [Display(Name = "Archived")]
        public bool IsArchived { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Version must be at least 1")]
        public int Version { get; set; }
        
        public List<MenuRecipeStepViewModel> Steps { get; set; } = new List<MenuRecipeStepViewModel>();
    }
    
    public class MenuRecipeStepViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Step")]
        [Required(ErrorMessage = "Step number is required")]
        [Range(1, 100, ErrorMessage = "Step number must be between 1 and 100")]
        public int StepNumber { get; set; }
        
        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; }
        
        [Display(Name = "Time Required (minutes)")]
        [Range(0, 300, ErrorMessage = "Time required must be between 0 and 300 minutes")]
        public int? TimeRequiredMinutes { get; set; }
        
        [StringLength(50, ErrorMessage = "Temperature cannot exceed 50 characters")]
        public string Temperature { get; set; }
        
        [Display(Name = "Special Equipment")]
        [StringLength(200, ErrorMessage = "Special equipment cannot exceed 200 characters")]
        public string SpecialEquipment { get; set; }
        
        [StringLength(500, ErrorMessage = "Tips cannot exceed 500 characters")]
        public string Tips { get; set; }
        
        [Display(Name = "Image")]
        public string ImagePath { get; set; }
        
        [Display(Name = "Upload Image")]
        public IFormFile ImageFile { get; set; }
    }
}
