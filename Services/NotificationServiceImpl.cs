using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotificationMicroservice.Data;
using NotificationMicroservice.Models;

namespace NotificationMicroservice.Services
{
    public class NotificationServiceImpl(
        AppDbContext appDbContext,
        IPushNotificationService pushNotificationService,
        ILogger<NotificationServiceImpl> logger
    ) : INotificationService
    {
        private readonly AppDbContext _dbContext = appDbContext;
        private readonly IPushNotificationService _pushNotificationService = pushNotificationService;
        private readonly ILogger<NotificationServiceImpl> _logger = logger;

        public async Task CreateAndSendAsync(
            Guid recipientUserId,
            Guid actorUserId,
            string type,
            Guid entityId,
            Guid? messageId = null
        )
        {
            await StoreNotificationAsync(recipientUserId, actorUserId, type, entityId, messageId);

            // Always attempt delivery, even when the row already exists: the previous attempt may
            // have stored the notification and then failed while talking to FCM.
            await _pushNotificationService.SendAsync(recipientUserId, actorUserId, type, entityId);
        }

        private async Task StoreNotificationAsync(
            Guid recipientUserId,
            Guid actorUserId,
            string type,
            Guid entityId,
            Guid? messageId
        )
        {
            if (
                messageId.HasValue
                && await _dbContext
                    .Notifications.AsNoTracking()
                    .AnyAsync(n => n.MessageId == messageId.Value)
            )
            {
                _logger.LogInformation(
                    "Notification for message {MessageId} is already stored; skipping duplicate insert.",
                    messageId
                );
                return;
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                RecipientUserId = recipientUserId,
                ActorUserId = actorUserId,
                Type = type,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
            };

            _dbContext.Notifications.Add(notification);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateMessageId(ex))
            {
                // Two deliveries of the same message raced. The unique index kept the data clean.
                _dbContext.Entry(notification).State = EntityState.Detached;
                _logger.LogWarning(
                    "Concurrent delivery of message {MessageId} detected; the notification was already stored.",
                    messageId
                );
            }
        }

        private static bool IsDuplicateMessageId(DbUpdateException exception) =>
            exception.InnerException is SqlException sqlException
            && (sqlException.Number == 2601 || sqlException.Number == 2627);
    }
}
