namespace RestaurantManagementSystem.Models
{
    /// <summary>
    /// UPI Payment Settings Model
    /// Stores configuration for generating UPI payment QR codes
    /// </summary>
    public class UPISettings
    {
        public int Id { get; set; }
        public string UPIId { get; set; } = string.Empty; // UPI ID like restaurant@paytm
        public string PayeeName { get; set; } = string.Empty; // Restaurant/Business name
        public bool IsEnabled { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }
    
    /// <summary>
    /// ViewModel for UPI Settings page
    /// </summary>
    public class UPISettingsViewModel
    {
        public string UPIId { get; set; } = string.Empty;
        public string PayeeName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
    }
}
