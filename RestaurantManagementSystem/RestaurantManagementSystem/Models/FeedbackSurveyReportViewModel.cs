using System;
using System.Collections.Generic;

namespace RestaurantManagementSystem.Models
{
    public class FeedbackSurveyReportItem
    {
        public int Id { get; set; }
        public DateTime VisitDate { get; set; }
        public string Location { get; set; }
        public bool? IsFirstVisit { get; set; }
        public int OverallRating { get; set; }
        public int? FoodRating { get; set; }
        public int? ServiceRating { get; set; }
        public int? CleanlinessRating { get; set; }
        public int? StaffRating { get; set; }
        public int? AmbienceRating { get; set; }
        public int? ValueRating { get; set; }
        public int? SpeedRating { get; set; }
        public string Tags { get; set; }
        public string Comments { get; set; }
        public string GuestName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string SurveyJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal AverageRating { get; set; }
        public int SurveyQuestionsAnswered { get; set; }
    }

    public class FeedbackSurveyReportSummary
    {
        public int TotalFeedback { get; set; }
        public int UniqueLocations { get; set; }
        public int UniqueGuests { get; set; }
        public int FirstTimeVisitors { get; set; }
        public int ReturningVisitors { get; set; }
        public decimal AvgOverallRating { get; set; }
        public decimal AvgFoodRating { get; set; }
        public decimal AvgServiceRating { get; set; }
        public decimal AvgCleanlinessRating { get; set; }
        public decimal AvgStaffRating { get; set; }
        public decimal AvgAmbienceRating { get; set; }
        public decimal AvgValueRating { get; set; }
        public decimal AvgSpeedRating { get; set; }
        public int FeedbackWithSurvey { get; set; }
        public DateTime? EarliestFeedback { get; set; }
        public DateTime? LatestFeedback { get; set; }
    }

    public class RatingDistribution
    {
        public int Rating { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    public class TagCount
    {
        public string Tag { get; set; }
        public int Count { get; set; }
    }

    public class FeedbackSurveyReportViewModel
    {
        public List<FeedbackSurveyReportItem> FeedbackItems { get; set; }
        public FeedbackSurveyReportSummary Summary { get; set; }
        public List<RatingDistribution> RatingDistribution { get; set; }
        public List<TagCount> TopTags { get; set; }
        
        // Filter parameters
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Location { get; set; }
        public int? MinRating { get; set; }
        public int? MaxRating { get; set; }

        public FeedbackSurveyReportViewModel()
        {
            FeedbackItems = new List<FeedbackSurveyReportItem>();
            RatingDistribution = new List<RatingDistribution>();
            TopTags = new List<TagCount>();
            Summary = new FeedbackSurveyReportSummary();
        }
    }
}
