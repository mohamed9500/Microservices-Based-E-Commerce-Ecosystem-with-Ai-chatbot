namespace Nexus.Notifications.Domain;

public sealed class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OrderId { get; set; }
    public string Kind { get; set; } = "";
    public string ToEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>Logged (dev logging provider) | Sent (SMTP) | Failed</summary>
    public string Status { get; set; } = "Logged";
    public string Provider { get; set; } = "log";
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
