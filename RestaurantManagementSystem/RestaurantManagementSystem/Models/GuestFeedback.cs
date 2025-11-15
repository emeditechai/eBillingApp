using System;

namespace RestaurantManagementSystem.Models
{
    public class GuestFeedback
    {
        public int Id { get; set; }
        public DateTime VisitDate { get; set; }
        public byte OverallRating { get; set; }
        public byte? FoodRating { get; set; }
        public byte? ServiceRating { get; set; }
        public byte? CleanlinessRating { get; set; }
        public byte? StaffRating { get; set; }
        // New detailed rating dimensions
        public byte? AmbienceRating { get; set; }
        public byte? ValueRating { get; set; }
        public byte? SpeedRating { get; set; }
        // Reference-like extras
        public string? Location { get; set; }
        public bool? FirstVisit { get; set; }
        public string? SurveyJson { get; set; }
    public string? Tags { get; set; }
    public string? Comments { get; set; }
    public string? GuestName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GuestFeedbackSummary
    {
        public int TotalFeedback { get; set; }
        public decimal AvgOverall { get; set; }
        public decimal AvgFood { get; set; }
        public decimal AvgService { get; set; }
        public decimal AvgCleanliness { get; set; }
        public decimal AvgStaff { get; set; }
        // Averages for new dimensions
        public decimal AvgAmbience { get; set; }
        public decimal AvgValue { get; set; }
        public decimal AvgSpeed { get; set; }
    }
}