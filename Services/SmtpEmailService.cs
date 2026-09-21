using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationMicroservice.Configuration;

namespace NotificationMicroservice.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
    public class SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger = logger;
        private readonly string _host = options.Value.SmtpHost;
        private readonly string _fromAddress = options.Value.FromAddress;
        private readonly int _port = options.Value.SmtpPort;


        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_fromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart(isHtml ? "html" : "plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.None);


            // For ZeptoMail integration later
            if (!string.IsNullOrEmpty(Options.DefaultName))
            {
                // Auth using zeptoMail creds
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
    }
}

// IMP : We knowingly didnt kept try/catch for the MassTransit to detect Exception and Try restries.