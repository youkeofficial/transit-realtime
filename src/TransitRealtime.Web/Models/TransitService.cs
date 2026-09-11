using System.ComponentModel.DataAnnotations;

namespace TransitRealtime.Web.Models;

public class TransitService
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedByUserId { get; set; }

    public List<ApiKeyEntity> ApiKeys { get; set; } = [];
}
