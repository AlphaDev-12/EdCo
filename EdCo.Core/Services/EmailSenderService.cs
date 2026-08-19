using EdCo.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class EmailSenderService : IEmailSenderService
    {
        private readonly ILogger<EmailSenderService> _logger;

        public EmailSenderService(ILogger<EmailSenderService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Log OTP message in development / system output
            _logger.LogInformation("================ EMAIL SENT ================");
            _logger.LogInformation("To: {Email}", email);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Content: {Message}", htmlMessage);
            _logger.LogInformation("============================================");

            return Task.CompletedTask;
        }
    }
}
