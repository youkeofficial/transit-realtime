using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;

namespace TransitRealtime.Web.Services;

public class ApiKeyService(ApplicationDbContext db)
{
    private readonly PasswordHasher<ApiKeyEntity> _hasher = new();

    public record GeneratedKey(ApiKeyEntity Entity, string RawKey);

    public async Task<GeneratedKey> CreateApiKeyAsync(Guid serviceId, DateTimeOffset? expiresAt = null)
    {
        var rawKey = GenerateRawKey();
        var entity = new ApiKeyEntity
        {
            ServiceId = serviceId,
            KeyPrefix = rawKey[..Math.Min(12, rawKey.Length)],
            ExpiresAt = expiresAt,
        };
        entity.KeyHash = _hasher.HashPassword(entity, rawKey);

        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        return new GeneratedKey(entity, rawKey);
    }

    public async Task RevokeApiKeyAsync(Guid keyId)
    {
        var key = await db.ApiKeys.FindAsync(keyId);
        if (key is not null && key.RevokedAt is null)
        {
            key.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> VerifyAsync(Guid serviceId, string rawKey)
    {
        var candidateKeys = await db.ApiKeys
            .Where(k => k.ServiceId == serviceId && k.RevokedAt == null)
            .ToListAsync();

        foreach (var key in candidateKeys)
        {
            if (key.ExpiresAt is not null && key.ExpiresAt <= DateTimeOffset.UtcNow)
                continue;

            var result = _hasher.VerifyHashedPassword(key, key.KeyHash, rawKey);
            if (result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
                return true;
        }

        return false;
    }

    private static string GenerateRawKey() => $"svc_{SecretGenerator.Generate()}";
}
