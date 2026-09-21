namespace NotificationMicroservice.Configuration
{
    public class EmailOptions
    {
        public string SmtpHost { get; set; } = "localhost";
        public int SmtpPort { get; set; } = 1025;
        public string FromAddress { get; set; } = "noreply@localhost";


    }
}