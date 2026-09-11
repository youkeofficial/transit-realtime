namespace TransitRealtime.Web.Models;

public enum AuditEventType
{
    WebhookReceived,
    WebhookAuthFailed,
    ClientConnected,
    ClientDisconnected,
    ServiceCreated,
    ApiKeyCreated,
    ApiKeyRevoked,
}

public class AuditLogEntry
{
    public long Id { get; set; }

    public Guid? ServiceId { get; set; }

    public AuditEventType EventType { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string? SourceIp { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(4000)]
    public string? Detail { get; set; }
}
