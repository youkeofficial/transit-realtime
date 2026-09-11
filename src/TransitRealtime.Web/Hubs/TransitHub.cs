using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;
using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Hubs;

public class TransitHub(ApplicationDbContext db, ConnectionTracker tracker, ILogger<TransitHub> logger) : Hub
{
    private const string GroupKeyItem = "joinedGroupKey";
    private const string ServiceIdItem = "joinedServiceId";

    /// <summary>Joins the broadcast-all group for a service. No auth beyond knowing the serviceId (see README limitations).</summary>
    public async Task<bool> JoinService(string serviceId)
    {
        if (!Guid.TryParse(serviceId, out var id))
            return false;

        var service = await db.TransitServices.FindAsync(id);
        if (service is null || !service.IsActive)
            return false;

        await JoinGroupAsync(id, TransitGroups.For(id, null));
        return true;
    }

    /// <summary>
    /// Joins a single-recipient group, gated by a short-lived HMAC token signed with the service's
    /// SubscriberSecret (claims: sub=recipientKey, svc=serviceId). Prevents one subscriber from
    /// listening in on another recipient's channel within the same service.
    /// </summary>
    public async Task<bool> JoinChannel(string serviceId, string recipientKey, string token)
    {
        if (!Guid.TryParse(serviceId, out var id) || string.IsNullOrWhiteSpace(recipientKey))
            return false;

        var service = await db.TransitServices.FindAsync(id);
        if (service is null || !service.IsActive || string.IsNullOrEmpty(service.SubscriberSecret))
            return false;

        if (!IsValidSubscriberToken(service.SubscriberSecret, serviceId, recipientKey, token, logger))
            return false;

        await JoinGroupAsync(id, TransitGroups.For(id, recipientKey));
        return true;
    }

    private async Task JoinGroupAsync(Guid serviceId, string groupKey)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey);
        Context.Items[GroupKeyItem] = groupKey;
        Context.Items[ServiceIdItem] = serviceId;
        tracker.Increment(groupKey);

        db.AuditLogs.Add(new AuditLogEntry
        {
            ServiceId = serviceId,
            EventType = AuditEventType.ClientConnected,
            SourceIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(GroupKeyItem, out var groupValue) && groupValue is string groupKey)
        {
            tracker.Decrement(groupKey);

            if (Context.Items.TryGetValue(ServiceIdItem, out var idValue) && idValue is Guid serviceId)
            {
                db.AuditLogs.Add(new AuditLogEntry
                {
                    ServiceId = serviceId,
                    EventType = AuditEventType.ClientDisconnected,
                    SourceIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
                });
                await db.SaveChangesAsync();
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static bool IsValidSubscriberToken(string secret, string serviceId, string recipientKey, string token, ILogger logger)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var svc = principal.FindFirst("svc")?.Value;
            var ok = sub == recipientKey && svc == serviceId;
            if (!ok)
                logger.LogWarning("Subscriber token claim mismatch: sub={Sub} expected={RecipientKey} svc={Svc} expectedSvc={ServiceId}", sub, recipientKey, svc, serviceId);
            return ok;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Subscriber token validation failed");
            return false;
        }
    }
}
