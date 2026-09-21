using MassTransit;

namespace NotificationMicroservice.Contracts
{
    [EntityName("push_notifications_exchange")]
    public class PushNotificationEvent
    {
        public Guid RecipientUserId { get; set; }
        public Guid ActorUserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
    }
}
