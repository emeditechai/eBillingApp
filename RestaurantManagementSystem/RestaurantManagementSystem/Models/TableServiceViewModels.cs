using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RestaurantManagementSystem.Models
{
    // Model for UC-002: Table Service Models
    
    public class SeatGuestViewModel
    {
        public int? ReservationId { get; set; }
        public int? WaitlistId { get; set; }
        
        [Required(ErrorMessage = "Table is required")]
        [Display(Name = "Table")]
        public int TableId { get; set; }
        
        [Required(ErrorMessage = "Server is required")]
        [Display(Name = "Assign Server")]
        public int ServerId { get; set; }
        
        [Required(ErrorMessage = "Guest name is required")]
        [Display(Name = "Guest Name")]
        [StringLength(100, ErrorMessage = "Guest name cannot exceed 100 characters")]
        public string GuestName { get; set; }
        
        [Required(ErrorMessage = "Party size is required")]
        [Display(Name = "Party Size")]
        [Range(1, 50, ErrorMessage = "Party size must be between 1 and 50")]
        public int PartySize { get; set; }
        
        [Display(Name = "Notes/Special Requests")]
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string Notes { get; set; }
        
        [Display(Name = "Target Turn Time (minutes)")]
        [Range(30, 240, ErrorMessage = "Turn time must be between 30 and 240 minutes")]
        public int TargetTurnTime { get; set; } = 90; // Default 90 minutes
        
        // Navigation properties for dropdowns
        public List<SelectListItem> AvailableTables { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Servers { get; set; } = new List<SelectListItem>();
    }
    
    public class ActiveTableViewModel
    {
        public int TurnoverId { get; set; }
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string GuestName { get; set; }
        public int PartySize { get; set; }
        public DateTime SeatedAt { get; set; }
        public DateTime? StartedServiceAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int Status { get; set; } // 0=Seated, 1=InService, 2=CheckRequested, 3=Paid, 4=Completed, 5=Departed
        public string ServerName { get; set; }
        public int ServerId { get; set; }
        public int Duration { get; set; } // Minutes since seated
        public int TargetTurnTime { get; set; } // Target turn time in minutes
        public string MergedTableNames { get; set; } // For merged table display
        public bool IsPartOfMergedOrder { get; set; } // Indicates if table is part of merged order
        
        public string StatusText
        {
            get
            {
                return Status switch
                {
                    0 => "Seated",
                    1 => "In Service",
                    2 => "Check Requested",
                    3 => "Paid",
                    4 => "Completed",
                    5 => "Departed",
                    _ => "Unknown"
                };
            }
        }
        
        public string DurationDisplay => $"{Duration} min";
        
        public bool IsOverTargetTime => Duration > TargetTurnTime;
        
        public string TimerClass
        {
            get
            {
                if (Duration > TargetTurnTime)
                    return "text-danger";
                if (Duration > TargetTurnTime * 0.75)
                    return "text-warning";
                return "text-success";
            }
        }
    }
    
    public class TableServiceDashboardViewModel
    {
        public int AvailableTables { get; set; }
        public int OccupiedTables { get; set; }
        public int DirtyTables { get; set; }
        public int ReservationCount { get; set; }
        public int WaitlistCount { get; set; }
        public List<ActiveTableViewModel> CurrentTurnovers { get; set; } = new List<ActiveTableViewModel>();
        public List<TableViewModel> UnoccupiedTables { get; set; } = new List<TableViewModel>();
        
        // Real-Time Analytics & Performance Metrics
        public decimal AverageTurnoverTime { get; set; } // In minutes
        public decimal TodayRevenue { get; set; }
        public decimal AverageRevenuePerTable { get; set; }
        public List<PeakHourData> PeakHours { get; set; } = new List<PeakHourData>();
        public List<ServerPerformanceData> ServerPerformance { get; set; } = new List<ServerPerformanceData>();
    }
    
    public class PeakHourData
    {
        public int Hour { get; set; } // 0-23
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public string HourDisplay => Hour == 0 ? "12 AM" : Hour < 12 ? $"{Hour} AM" : Hour == 12 ? "12 PM" : $"{Hour - 12} PM";
        public bool IsPeakHour { get; set; }
    }
    
    public class ServerPerformanceData
    {
        public int ServerId { get; set; }
        public string ServerName { get; set; }
        public int ActiveTables { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public string WorkloadLevel => ActiveTables == 0 ? "idle" : ActiveTables <= 2 ? "low" : ActiveTables <= 4 ? "medium" : "high";
        public string WorkloadColor => ActiveTables == 0 ? "secondary" : ActiveTables <= 2 ? "success" : ActiveTables <= 4 ? "warning" : "danger";
    }
}
