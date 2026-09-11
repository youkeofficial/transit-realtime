using Microsoft.AspNetCore.SignalR;
using TransitRealtime.Web.Data;
using TransitRealtime.Web.Models;
using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Hubs;

public class TransitHub(ApplicationDbContext db, ConnectionTracker tracker) : Hub
{
    private const string JoinedServiceKey = "joinedService";

    public async Task<bool> JoinService(string serviceId)
    {
        if (!Guid.TryParse(serviceId, out var id))
            return false;

        var service = await db.TransitServices.FindAsync(id);
        if (service is null || !service.IsActive)
            return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, serviceId);
        Context.Items[JoinedServiceKey] = serviceId;
        tracker.Increment(serviceId);

        db.AuditLogs.Add(new AuditLogEntry
        {
            ServiceId = id,
            EventType = AuditEventType.ClientConnected,
            SourceIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();

        return true;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(JoinedServiceKey, out var value) && value is string serviceId)
        {
            tracker.Decrement(serviceId);

            if (Guid.TryParse(serviceId, out var id))
            {
                db.AuditLogs.Add(new AuditLogEntry
                {
                    ServiceId = id,
                    EventType = AuditEventType.ClientDisconnected,
                    SourceIp = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
                });
                await db.SaveChangesAsync();
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
