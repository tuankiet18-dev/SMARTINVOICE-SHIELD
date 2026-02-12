using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartInvoice.API.Services.Interfaces;

namespace SmartInvoice.API.Services.Implementations
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _logger.LogInformation($"[MockEmail] To: {toEmail}, Subject: {subject}, Body: {body}");
            return Task.CompletedTask;
        }

        public Task SendVerificationEmailAsync(string toEmail, string verificationToken)
        {
            var link = $"https://smartinvoice.app/verify-email?token={verificationToken}&email={toEmail}";
            var body = $"Please verify your email by clicking here: {link}";
            _logger.LogInformation($"[MockEmail] Sending Verification to {toEmail}: {link}");
            return Task.CompletedTask;
        }
    }
}
