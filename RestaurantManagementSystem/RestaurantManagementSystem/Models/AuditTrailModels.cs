using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Models
{
    public class OrderAuditTrail
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string? FieldName { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public int ChangedBy { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
        public DateTime ChangedDate { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? AdditionalInfo { get; set; }
    }

    public class AuditTrailViewModel
    {
        public List<OrderAuditTrail> AuditRecords { get; set; } = new List<OrderAuditTrail>();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        
        // Filter properties
        public int? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? UserId { get; set; }
        public string? EntityType { get; set; }
        public string? SearchTerm { get; set; }
        
        // Available filter options
        public List<(string Value, string Text)> EntityTypes { get; set; } = new List<(string Value, string Text)>();
        public List<(string Value, string Text)> Users { get; set; } = new List<(string Value, string Text)>();
        
        // Display-friendly versions
        public List<OrderAuditTrail> Records => AuditRecords;
    }

    public class AuditTrailStatistics
    {
        public int TotalAuditRecords { get; set; }
        public int OrdersModifiedToday { get; set; }
        public int OrdersModifiedThisWeek { get; set; }
        public int OrdersModifiedThisMonth { get; set; }
        public Dictionary<string, int> ActionBreakdown { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> EntityTypeBreakdown { get; set; } = new Dictionary<string, int>();
        public List<TopUserActivity> TopUsers { get; set; } = new List<TopUserActivity>();
    }

    public class TopUserActivity
    {
        public string UserName { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
    }
}
