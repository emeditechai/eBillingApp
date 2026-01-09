using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RestaurantManagementSystem.Models;

namespace RestaurantManagementSystem.ViewModels
{
    // View Models for UC-005: Kitchen Management
    
    public class KitchenDashboardViewModel
    {
        public List<KitchenStation> Stations { get; set; } = new List<KitchenStation>();
        public List<KitchenTicket> NewTickets { get; set; } = new List<KitchenTicket>();
        public List<KitchenTicket> InProgressTickets { get; set; } = new List<KitchenTicket>();
        public List<KitchenTicket> ReadyTickets { get; set; } = new List<KitchenTicket>();
        // Newly delivered (today) tickets
        public List<KitchenTicket> DeliveredTickets { get; set; } = new List<KitchenTicket>();
        public KitchenDashboardStats Stats { get; set; } = new KitchenDashboardStats();
        
        public int SelectedStationId { get; set; }
        public string SelectedStationName { get; set; }
    }
    
    public class KitchenTicketsViewModel
    {
        public List<KitchenTicket> Tickets { get; set; } = new List<KitchenTicket>();
        public KitchenStationFilterViewModel Filter { get; set; } = new KitchenStationFilterViewModel();
        public KitchenDashboardStats Stats { get; set; } = new KitchenDashboardStats();
    }
    
    public class KitchenTicketDetailsViewModel
    {
        public KitchenTicket Ticket { get; set; }
        public List<KitchenTicketItem> Items { get; set; } = new List<KitchenTicketItem>();
        public string OrderNotes { get; set; }
        
        public bool CanUpdateStatus { get; set; } = true;
    }
    
    public class KitchenStationFilterViewModel
    {
        public int? StationId { get; set; }
        public List<KitchenStation> Stations { get; set; } = new List<KitchenStation>();
        
        public int? Status { get; set; }
        public List<StatusOption> StatusOptions { get; set; } = new List<StatusOption> 
        {
            new StatusOption { Value = 0, Text = "New" },
            new StatusOption { Value = 1, Text = "In Progress" },
            new StatusOption { Value = 2, Text = "Ready" },
            new StatusOption { Value = 3, Text = "Delivered" },
            new StatusOption { Value = 4, Text = "Cancelled" }
        };
        
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }
    
    public class StatusOption
    {
        public int Value { get; set; }
        public string Text { get; set; }
    }
    
    public class KitchenStationViewModel
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Station name is required")]
        [StringLength(50, ErrorMessage = "Station name cannot exceed 50 characters")]
        [Display(Name = "Station Name")]
        public string Name { get; set; }
        
        [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
        public string Description { get; set; }
        
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
        
        public List<int> AssignedMenuItemIds { get; set; } = new List<int>();
        public List<MenuItemOption> AvailableMenuItems { get; set; } = new List<MenuItemOption>();
    }
    
    public class MenuItemOption
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public bool IsAssigned { get; set; }
        public bool IsPrimary { get; set; }
    }
    
    public class KitchenStatusUpdateModel
    {
        public int TicketId { get; set; }
        public int Status { get; set; }
    }
    
    public class KitchenItemStatusUpdateModel
    {
        public int ItemId { get; set; }
        public int Status { get; set; }
    }

    public class KitchenItemCommentInputModel
    {
        [Required]
        public int KitchenTicketId { get; set; }

        [Required]
        public int KitchenTicketItemId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int OrderItemId { get; set; }

        [Required]
        [StringLength(1000)]
        public string CommentText { get; set; }
    }
}
