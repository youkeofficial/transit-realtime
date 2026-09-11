using Microsoft.AspNetCore.SignalR;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Hubs;
using TransitRealtime.Web.Models;

namespace TransitRealtime.Web.Services;

public enum DispatchResult
{
    Ok,
    ServiceNotFound,
    Unauthorized,
}

public class WebhookDispatcher(
    ApplicationDbContext db,
    ApiKeyService apiKeys,
    IHubContext<TransitHub> hub)
{
    private const int MaxLoggedPayloadLength = 4000;

    public async Task<DispatchResult> DispatchAsync(Guid serviceId, string rawApiKey, string payload, string? sourceIp)
    {
        var service = await db.TransitServices.FindAsync(serviceId);
        if (service is null || !service.IsActive)
            return DispatchResult.ServiceNotFound;

        if (!await apiKeys.VerifyAsync(serviceId, rawApiKey))
        {
            db.AuditLogs.Add(new AuditLogEntry
            {
                ServiceId = serviceId,
                EventType = AuditEventType.WebhookAuthFailed,
                SourceIp = sourceIp,
            });
            await db.SaveChangesAsync();
            return DispatchResult.Unauthorized;
        }

        await hub.Clients.Group(serviceId.ToString()).SendAsync("ReceivePayload", payload);

        db.AuditLogs.Add(new AuditLogEntry
        {
            ServiceId = serviceId,
            EventType = AuditEventType.WebhookReceived,
            SourceIp = sourceIp,
            Detail = payload.Length > MaxLoggedPayloadLength ? payload[..MaxLoggedPayloadLength] : payload,
        });
        await db.SaveChangesAsync();

        return DispatchResult.Ok;
    }
}
