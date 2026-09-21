using MassTransit;

namespace NotificationMicroservice.Contracts
{
    [EntityName("send_email_exchange")]
    public class SendEmailEvent
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
    }
}