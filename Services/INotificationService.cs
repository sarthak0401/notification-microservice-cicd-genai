namespace NotificationMicroservice.Services
{
    public interface INotificationService
    {
        /// <param name="messageId">
        /// MassTransit message id. When supplied the notification row is written at most once for
        /// that id, which makes retries, redeliveries and replays idempotent.
        /// </param>
        Task CreateAndSendAsync(
            Guid recipientUserId,
            Guid actorUserId,
            string type,
            Guid entityId,
            Guid? messageId = null
        );
    }
}
