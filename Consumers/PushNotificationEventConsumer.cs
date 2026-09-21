using MassTransit;
using NotificationMicroservice.Contracts;
using NotificationMicroservice.Services;

namespace NotificationMicroservice.Consumers
{
    public class PushNotificationEventConsumer(
        INotificationService notificationService,
        ILogger<PushNotificationEventConsumer> logger
    ) : IConsumer<PushNotificationEvent>
    {
        private readonly INotificationService _notificationService = notificationService;
        private readonly ILogger<PushNotificationEventConsumer> _logger = logger;

        public async Task Consume(ConsumeContext<PushNotificationEvent> context)
        {
            var message = context.Message;
            var attempt = context.GetRetryAttempt();

            _logger.LogInformation(
                "RabbitMQ Consumer received PushNotificationEvent {MessageId} for Recipient: {RecipientUserId}, Type: {Type} (retry attempt {Attempt})",
                context.MessageId,
                message.RecipientUserId,
                message.Type,
                attempt
            );

            await _notificationService.CreateAndSendAsync(
                message.RecipientUserId,
                message.ActorUserId,
                message.Type,
                message.EntityId,
                context.MessageId
            );
        }
    }
}
