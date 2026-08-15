using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace WebMinRouteGroup.Services;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
}

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task Send(string emailAddress, string body)
    {
        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = true
            };

            // Extract subject from the first line of body (caller formats "New todo added: {title}\n...")
            var lines = body.Split('\n', 2);
            var subject = lines[0].Trim();

            var message = new MailMessage(
                from: _settings.FromAddress,
                to: emailAddress,
                subject: subject,
                body: body
            );

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {EmailAddress}", emailAddress);
        }
    }
}
