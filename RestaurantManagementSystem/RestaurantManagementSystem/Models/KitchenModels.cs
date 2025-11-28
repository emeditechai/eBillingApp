using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    // Models for UC-005: Kitchen Management
    
    public class KitchenStation
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        
        [StringLength(200)]
        public string Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class MenuItemKitchenStation
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public int KitchenStationId { get; set; }
        public bool IsPrimary { get; set; } = true;
        
        // Navigation properties
        public MenuItem MenuItem { get; set; }
        public KitchenStation KitchenStation { get; set; }
    }
    
    public class KitchenTicket
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(20)]
        public string TicketNumber { get; set; }
        
        public int OrderId { get; set; }
        
        [StringLength(20)]
        public string OrderNumber { get; set; }
        
        public int? KitchenStationId { get; set; }
        
        [StringLength(50)]
        public string StationName { get; set; }
        
        [StringLength(50)]
        public string TableName { get; set; }
        
        public int Status { get; set; } // 0=New, 1=In Progress, 2=Ready, 3=Delivered, 4=Cancelled
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "New",
                    1 => "In Progress",
                    2 => "Ready",
                    3 => "Delivered",
                    4 => "Cancelled",
                    _ => "Unknown"
                };
            }
        }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int MinutesSinceCreated { get; set; }
        
        // Navigation properties
        public List<KitchenTicketItem> Items { get; set; } = new List<KitchenTicketItem>();
    }
    
    public class KitchenTicketItem
    {
        public int Id { get; set; }
        public int KitchenTicketId { get; set; }
        public int OrderItemId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string MenuItemName { get; set; }
        
        public int Quantity { get; set; }
        
        [StringLength(500)]
        public string SpecialInstructions { get; set; }
        
        public int Status { get; set; } // 0=New, 1=In Progress, 2=Ready, 3=Delivered, 4=Cancelled
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "New",
                    1 => "In Progress",
                    2 => "Ready",
                    3 => "Delivered",
                    4 => "Cancelled",
                    _ => "Unknown"
                };
            }
        }
        
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        
        [StringLength(500)]
        public string Notes { get; set; }
        
        public int MinutesCooking { get; set; }
        public int? KitchenStationId { get; set; }
        public string StationName { get; set; }
        public int PrepTime { get; set; } // In minutes
        
        // Navigation properties
        public List<string> Modifiers { get; set; } = new List<string>();
    }
    
    public class KitchenDashboardStats
    {
        public int NewTicketsCount { get; set; }
        public int InProgressTicketsCount { get; set; }
        public int ReadyTicketsCount { get; set; }
        public int PendingItemsCount { get; set; }
        public int ReadyItemsCount { get; set; }
        public double AvgPrepTimeMinutes { get; set; }
        
        // Item counts per status
        public int NewItemsCount { get; set; }
        public int InProgressItemsCount { get; set; }
        public int ReadyItemsTotalCount { get; set; }
        
        public int TotalTicketsCount => NewTicketsCount + InProgressTicketsCount + ReadyTicketsCount;
        public int TotalItemsCount => PendingItemsCount + ReadyItemsCount;
    }
}
