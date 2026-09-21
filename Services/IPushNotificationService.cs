namespace NotificationMicroservice.Services
{
    public interface IPushNotificationService
    {
        Task SendAsync(Guid recipientUserId, Guid actorUserId, string type, Guid entityId);
    }
}
