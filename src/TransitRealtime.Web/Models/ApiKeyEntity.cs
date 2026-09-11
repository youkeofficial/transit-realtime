namespace TransitRealtime.Web.Models;

public class ApiKeyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ServiceId { get; set; }
    public TransitService? Service { get; set; }

    public string KeyHash { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTimeOffset.UtcNow);
}
