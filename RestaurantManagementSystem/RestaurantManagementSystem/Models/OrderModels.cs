using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    // Model for UC-003: Capture Dine-In Order
    
    public class OrderMenuItem
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [StringLength(500)]
        public string Description { get; set; }
        
        [Required]
        [Range(0.01, 10000)]
        public decimal Price { get; set; }
        
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        
        public bool IsAvailable { get; set; } = true;
        
        public int? PrepTime { get; set; }
        
        public string ImagePath { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public List<OrderModifier> AvailableModifiers { get; set; } = new List<OrderModifier>();
        public List<OrderAllergen> ContainedAllergens { get; set; } = new List<OrderAllergen>();
    }
    
    public class OrderModifier
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        [Range(0, 1000)]
        public decimal Price { get; set; }
        
        public bool IsDefault { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class OrderAllergen
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [StringLength(500)]
        public string Description { get; set; }
        
        public string IconPath { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class CourseType
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        public int DisplayOrder { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class Order
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(20)]
        public string OrderNumber { get; set; }
        
        public int? TableTurnoverId { get; set; }
        
        public int OrderType { get; set; } // 0=Dine-In, 1=Takeout, 2=Delivery, 3=Online
        
        public string OrderTypeDisplay
        {
            get
            {
                return OrderType switch
                {
                    0 => "Dine-In",
                    1 => "Takeout",
                    2 => "Delivery",
                    3 => "Online",
                    _ => "Unknown"
                };
            }
        }
        
        public int Status { get; set; } // 0=Open, 1=In Progress, 2=Ready, 3=Completed, 4=Cancelled
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "Open",
                    1 => "In Progress",
                    2 => "Ready",
                    3 => "Completed",
                    4 => "Cancelled",
                    _ => "Unknown"
                };
            }
        }
        
        public int? UserId { get; set; }
        public string UserName { get; set; }
        
        [StringLength(100)]
        public string CustomerName { get; set; }
        
        [StringLength(20)]
        public string CustomerPhone { get; set; }
        
        [StringLength(100)]
        [EmailAddress]
        public string CustomerEmailId { get; set; }
        
        [Required]
        [Range(0, 100000)]
        public decimal Subtotal { get; set; }
        
        [Required]
        [Range(0, 10000)]
        public decimal TaxAmount { get; set; }
        
        [Required]
        [Range(0, 10000)]
        public decimal TipAmount { get; set; }
        
        [Required]
        [Range(0, 10000)]
        public decimal DiscountAmount { get; set; }
        
        [Required]
        [Range(0.01, 100000)]
        public decimal TotalAmount { get; set; }
        
        [StringLength(500)]
        public string SpecialInstructions { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        // Table information
        public string TableName { get; set; }
        public string GuestName { get; set; }
        
        // Navigation properties
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public List<KitchenTicket> KitchenTickets { get; set; } = new List<KitchenTicket>();
    }
    
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MenuItemId { get; set; }
        
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
        
        [Required]
        [Range(0.01, 10000)]
        public decimal UnitPrice { get; set; }
        
        [Required]
        [Range(0.01, 10000)]
        public decimal Subtotal { get; set; }
        
        [StringLength(500)]
        public string SpecialInstructions { get; set; }
        
        public int? CourseId { get; set; }
        public string CourseName { get; set; }
        
        public int Status { get; set; } // 0=New, 1=Fired, 2=Cooking, 3=Ready, 4=Delivered, 5=Cancelled
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "New",
                    1 => "Fired",
                    2 => "Cooking",
                    3 => "Ready",
                    4 => "Delivered",
                    5 => "Cancelled",
                    _ => "Unknown"
                };
            }
        }
        
        public DateTime? FireTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public DateTime? DeliveryTime { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // MenuItem details
        public string MenuItemName { get; set; }
        public string MenuItemDescription { get; set; }
        
        // Navigation properties
        public List<OrderItemModifier> Modifiers { get; set; } = new List<OrderItemModifier>();
    }
    
    public class OrderItemModifier
    {
        public int Id { get; set; }
        public int OrderItemId { get; set; }
        public int ModifierId { get; set; }
        public string ModifierName { get; set; }
        public decimal Price { get; set; }
    }
}
