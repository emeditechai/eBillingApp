using System.Threading.Tasks;

namespace RestaurantManagementSystem.Services
{
    public interface IEmailSender
    {
        Task<(bool Success, string? ErrorMessage)> SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            string? emailType = null,
            string? sentFrom = null);
    }
}
