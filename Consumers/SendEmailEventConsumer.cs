using MassTransit;
using NotificationMicroservice.Contracts;
using NotificationMicroservice.Services;

namespace NotificationMicroservice.Consumers
{
    public class SendEmailEventConsumer(IEmailService emailService, ILogger<SendEmailEventConsumer> logger) : IConsumer<SendEmailEvent>
    {
        private readonly IEmailService _emailService = emailService;
        private readonly ILogger<SendEmailEventConsumer> _logger = logger;

        public async Task Consume(ConsumeContext<SendEmailEvent> context)
        {
            var message = context.Message;
            var attempt = context.GetRetryAttempt();

            _logger.LogInformation(
                "Email consumer received SendEmailEvent for {To} (retry attempt {Attempt}): {Subject}",
                message.To, attempt, message.Subject
            );

            await _emailService.SendEmailAsync(message.To, message.Subject, message.Body, message.IsHtml);

            _logger.LogInformation("Email delivered to {To}: {Subject}", message.To, message.Subject);
        }
    }
}