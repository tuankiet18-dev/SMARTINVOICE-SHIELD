using System.Threading.Tasks;

namespace SmartInvoice.API.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendVerificationEmailAsync(string toEmail, string verificationToken);
    }
}
