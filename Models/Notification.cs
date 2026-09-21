namespace NotificationMicroservice.Models
{
    public class Notification
    {
        public Guid Id { get; set; }

        /// <summary>
        /// MassTransit message id that produced this row. Used to keep the consumer idempotent when
        /// a message is retried, redelivered or replayed. Null for notifications created from the API.
        /// </summary>
        public Guid? MessageId { get; set; }

        public Guid RecipientUserId { get; set; }
        public Guid ActorUserId { get; set; }
        public string Type { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
