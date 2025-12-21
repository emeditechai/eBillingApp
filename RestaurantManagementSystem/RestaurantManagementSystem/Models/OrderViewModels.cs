using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    // View Models for UC-003: Capture Dine-In Order
    
    public class OrderDashboardViewModel
    {
        public List<OrderSummary> ActiveOrders { get; set; } = new List<OrderSummary>();
        public List<OrderSummary> CompletedOrders { get; set; } = new List<OrderSummary>();
        public List<OrderSummary> CancelledOrders { get; set; } = new List<OrderSummary>();
        public int OpenOrdersCount { get; set; }
        public int InProgressOrdersCount { get; set; }
        public int ReadyOrdersCount { get; set; }
        public int CompletedOrdersCount { get; set; }
        public int CancelledOrdersCount { get; set; }
        public decimal TotalSales { get; set; }
    }
    
    public class OrderSummary
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int OrderType { get; set; } // 0=Dine-In, 1=Takeout, 2=Delivery, 3=Online, 4=Room Service
        public string OrderTypeDisplay { get; set; }
        public int Status { get; set; } // 0=Open, 1=In Progress, 2=Ready, 3=Completed, 4=Cancelled
        public string StatusDisplay { get; set; }
        public string TableName { get; set; }
        public string GuestName { get; set; }
        public string ServerName { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }
    
    public class CreateOrderViewModel
    {
        public List<TableViewModel> AvailableTables { get; set; } = new List<TableViewModel>();
        public List<ActiveTableViewModel> OccupiedTables { get; set; } = new List<ActiveTableViewModel>();
        // For merged table selection (multiple tables assigned to a single order)
        [Display(Name = "Merged Tables")]
        public List<int> SelectedTableIds { get; set; } = new List<int>();
        
        [Display(Name = "Table")]
        public int? TableTurnoverId { get; set; }
        
        [Display(Name = "Selected Table")]
        public int? SelectedTableId { get; set; }
        
        [Required]
        [Display(Name = "Order Type")]
        public int OrderType { get; set; } = 0; // Default to Dine-In (4 = Room Service)
        
        [Display(Name = "Customer Name")]
        [StringLength(100)]
        public string CustomerName { get; set; }
        
        [Display(Name = "Customer Phone")]
        [StringLength(20)]
        public string CustomerPhone { get; set; }
        
        [Display(Name = "Customer Email")]
        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string CustomerEmailId { get; set; }

        // Delivery only
        [Display(Name = "Address")]
        [StringLength(500)]
        public string CustomerAddress { get; set; }
        
        [Display(Name = "Special Instructions")]
        [StringLength(500)]
        public string SpecialInstructions { get; set; }

        // Room Service (Hotel) only
        [Display(Name = "Hotel Branch")]
        public int? HBranchId { get; set; }

        [Display(Name = "Room")]
        public int? RoomId { get; set; }

        [Display(Name = "Hotel Booking ID")]
        public int? HBookingId { get; set; }

        [Display(Name = "Booking No")]
        [StringLength(50)]
        public string HBookingNo { get; set; }
    }
    
    public class OrderViewModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int? TableTurnoverId { get; set; }
        public string TableName { get; set; }
        public string GuestName { get; set; }
        public int OrderType { get; set; }
        public string OrderTypeDisplay { get; set; }
        public int Status { get; set; }
        public string StatusDisplay { get; set; }
        public string ServerName { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmailId { get; set; }
        public string CustomerAddress { get; set; }

        // Room Service (Hotel) fields (optional)
        public int? HBranchId { get; set; }
        public int? RoomId { get; set; }
        public string RoomNo { get; set; }
        public int? HBookingId { get; set; }
        public string HBookingNo { get; set; }
        private decimal? _subtotal;
        public decimal Subtotal {
            get {
                if (_subtotal.HasValue) return _subtotal.Value;
                if (Items != null && Items.Count > 0)
                    return Items.Where(i => i.Status != 5).Sum(i => i.Subtotal);
                return 0;
            }
            set { _subtotal = value; }
        }
        public decimal TaxAmount { get; set; }
    // GST related breakdown (computed using Default GST % from settings)
    public decimal GSTPercentage { get; set; }
    public decimal CGSTAmount { get; set; }
    public decimal SGSTAmount { get; set; }
        public decimal TipAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        private decimal? _totalAmount;
        public decimal TotalAmount {
            get {
                if (_totalAmount.HasValue) return _totalAmount.Value;
                return Subtotal + TaxAmount + TipAmount - DiscountAmount;
            }
            set { _totalAmount = value; }
        }
        public string SpecialInstructions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
        public List<KitchenTicketViewModel> KitchenTickets { get; set; } = new List<KitchenTicketViewModel>();

        // Properties for adding new items
        public List<MenuCategoryViewModel> MenuCategories { get; set; } = new List<MenuCategoryViewModel>();
        public List<CourseType> AvailableCourses { get; set; } = new List<CourseType>();
        public List<MenuItem> AvailableMenuItems { get; set; } = new List<MenuItem>();
        // Filtering support: list of menu item groups and current selection (default 1)
        public List<MenuItemGroup> MenuItemGroups { get; set; } = new List<MenuItemGroup>();
        public int SelectedMenuItemGroupId { get; set; } = 1;
        // Payment aggregates
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    // Treat any order with Status == 3 (Completed) as locked/paid for UI purposes.
    // This ensures completed orders cannot be edited regardless of small rounding diffs.
    public bool IsFullyPaid => Status == 3;
    }
    }
    
    public class OrderItemViewModel
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }
        public string MenuItemDescription { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TotalPrice { get; set; }
        public string Name { get; set; } // Added to resolve the error
        public string SpecialInstructions { get; set; }
        public int? CourseId { get; set; }
        public string CourseName { get; set; }
        public int Status { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime? FireTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public DateTime? DeliveryTime { get; set; }
        
        public List<OrderItemModifierViewModel> Modifiers { get; set; } = new List<OrderItemModifierViewModel>();
    }
    
    public class OrderItemModifierViewModel
    {
        public int Id { get; set; }
        public int OrderItemId { get; set; }
        public int ModifierId { get; set; }
        public string ModifierName { get; set; }
        public decimal Price { get; set; }
    }
    
    public class KitchenTicketViewModel
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; }
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public int? StationId { get; set; } // Maps to KitchenStationId in the database
        public string StationName { get; set; }
        public int Status { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        public List<KitchenTicketItemViewModel> Items { get; set; } = new List<KitchenTicketItemViewModel>();
    }
    
    public class KitchenTicketItemViewModel
    {
        public int Id { get; set; }
        public int KitchenTicketId { get; set; }
        public int OrderItemId { get; set; }
        public string MenuItemName { get; set; }
        public int Quantity { get; set; }
        public string SpecialInstructions { get; set; }
        public int Status { get; set; }
        public string StatusDisplay { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string Notes { get; set; }
        public List<string> Modifiers { get; set; } = new List<string>();
    }
    
    public class AddOrderItemViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; }
        public string TableNumber { get; set; }
        
        [Required]
        [Display(Name = "Menu Item")]
        public int MenuItemId { get; set; }
        
        // Menu item details
        public string MenuItemName { get; set; }
        public string MenuItemDescription { get; set; }
        public decimal MenuItemPrice { get; set; }
        public string MenuItemImagePath { get; set; }
        
        [Required]
        [Range(1, 100)]
        [Display(Name = "Quantity")]
        public int Quantity { get; set; } = 1;
        
        [Display(Name = "Special Instructions")]
        [StringLength(500)]
        public string SpecialInstructions { get; set; }
        
        [Display(Name = "Course")]
        public int? CourseId { get; set; }
        
        [Display(Name = "Modifiers")]
        public List<int> SelectedModifiers { get; set; } = new List<int>();
        
        // Order summary
        public List<OrderItemViewModel> CurrentOrderItems { get; set; } = new List<OrderItemViewModel>();
        public decimal CurrentOrderTotal { get; set; }
        
        // Navigation properties for dropdown population
        public MenuItem MenuItem { get; set; }
        public List<SelectListItem> AvailableCourses { get; set; } = new List<SelectListItem>();
        public List<ModifierViewModel> AvailableModifiers { get; set; } = new List<ModifierViewModel>();
        public List<string> CommonAllergens { get; set; } = new List<string>();
    }
    
    public class MenuCategoryViewModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
    
    public class ModifierViewModel
    {
        public int Id { get; set; }
        public int ModifierId { get; set; } // Added to fix the errors
        public string Name { get; set; }
        public decimal Price { get; set; }
        public bool IsDefault { get; set; }
        public bool IsSelected { get; set; }
    }
    
    public class FireOrderItemsViewModel
    {
        public int OrderId { get; set; }
        public List<int> SelectedItems { get; set; } = new List<int>();
        public bool FireAll { get; set; }
        public bool IsBarOrder { get; set; } // Flag to indicate if this is a bar order
    }
    
    public class TableViewModel
    {
        public int Id { get; set; }
        public string TableName { get; set; }
        public int Capacity { get; set; }
        public int Status { get; set; }
        public string StatusDisplay { get; set; }
        public string Section { get; set; }

        // Optional: next/active reservation details for display on dashboard.
        public DateTime? NextReservationAt { get; set; }
        public int? NextReservationId { get; set; }
    }
