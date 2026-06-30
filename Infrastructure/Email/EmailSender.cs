using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Spotster.Infrastructure.Email;

public class EmailSender : IEmailSender
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<SmtpSettings> smtp, ILogger<EmailSender> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_smtp.IsConfigured)
        {
            _logger.LogWarning(
                "SMTP not configured: email to {Email} with subject '{Subject}' was not sent. HTML body logged below.",
                toEmail,
                subject);
            _logger.LogInformation("Email body for {Email}:\n{Body}", toEmail, htmlBody);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl = _smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(_smtp.User, _smtp.Password)
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email send failed to {Email}. Registration was not blocked.", toEmail);
            _logger.LogInformation("Confirmation link for {Email}:\n{Body}", toEmail, htmlBody);
        }
    }
}
