using System;
using System.Collections.Generic;
using RestaurantManagementSystem.Models;

namespace RestaurantManagementSystem.ViewModels
{
    /// <summary>
    /// BOT (Bar Order Ticket) Dashboard ViewModel
    /// Similar to Kitchen Dashboard but for BAR station only
    /// </summary>
    public class BOTDashboardViewModel
    {
        public List<KitchenTicket> NewTickets { get; set; } = new List<KitchenTicket>();
        public List<KitchenTicket> InProgressTickets { get; set; } = new List<KitchenTicket>();
        public List<KitchenTicket> ReadyTickets { get; set; } = new List<KitchenTicket>();
        // Delivered today tickets
        public List<KitchenTicket> DeliveredTickets { get; set; } = new List<KitchenTicket>();
        public RestaurantManagementSystem.Models.BOTDashboardStats Stats { get; set; } = new RestaurantManagementSystem.Models.BOTDashboardStats();
    }
    
    /// <summary>
    /// BOT Status Update Model
    /// </summary>
    public class BOTStatusUpdateModel
    {
        public int TicketId { get; set; }
        public int Status { get; set; }
    }

    /// <summary>
    /// Filter options for BAR Tickets page
    /// </summary>
    public class BOTTicketsFilterViewModel
    {
        public int? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public List<StatusOption> StatusOptions { get; set; } = new List<StatusOption>
        {
            new StatusOption { Value = 0, Text = "New" },
            new StatusOption { Value = 1, Text = "In Progress" },
            new StatusOption { Value = 2, Text = "Ready" },
            new StatusOption { Value = 3, Text = "Delivered" },
            new StatusOption { Value = 4, Text = "Cancelled" }
        };
    }

    /// <summary>
    /// ViewModel for BAR Tickets list (clone of Kitchen Tickets but filtered to BAR)
    /// </summary>
    public class BOTTicketsViewModel
    {
        public List<KitchenTicket> Tickets { get; set; } = new List<KitchenTicket>();
        public BOTTicketsFilterViewModel Filter { get; set; } = new BOTTicketsFilterViewModel();
        public RestaurantManagementSystem.Models.BOTDashboardStats Stats { get; set; } = new RestaurantManagementSystem.Models.BOTDashboardStats();

        public int TotalTicketsCount => (Stats?.NewBOTsCount ?? 0) + (Stats?.InProgressBOTsCount ?? 0) + (Stats?.ReadyBOTsCount ?? 0);
    }
}
