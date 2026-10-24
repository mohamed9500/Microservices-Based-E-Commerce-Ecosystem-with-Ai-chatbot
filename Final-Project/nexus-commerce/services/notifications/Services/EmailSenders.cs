using System.Net;
using System.Net.Mail;

namespace Nexus.Notifications.Services;

public sealed record EmailMessage(string To, string Subject, string Body);
public sealed record EmailResult(string Status, string Provider, string? Error);

public interface IEmailSender
{
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Development fallback: writes the email to the log instead of sending it.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task<EmailResult> SendAsync(EmailMessage m, CancellationToken ct)
    {
        logger.LogInformation("[DEV EMAIL - not actually sent] To: {To} | Subject: {Subject}\n{Body}", m.To, m.Subject, m.Body);
        return Task.FromResult(new EmailResult("Logged", "log", null));
    }
}

public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<EmailResult> SendAsync(EmailMessage m, CancellationToken ct)
    {
        try
        {
            var s = config.GetSection("Email:Smtp");
            using var client = new SmtpClient(s["Host"], s.GetValue("Port", 587))
            {
                EnableSsl = s.GetValue("EnableSsl", true),
                Credentials = string.IsNullOrEmpty(s["User"]) ? null : new NetworkCredential(s["User"], s["Password"])
            };
            using var mail = new MailMessage(config["Email:From"] ?? "no-reply@localhost", m.To, m.Subject, m.Body);
            await client.SendMailAsync(mail, ct);
            return new EmailResult("Sent", "smtp", null);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "SMTP send failed for {To}", m.To);
            return new EmailResult("Failed", "smtp", ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
        }
    }
}
