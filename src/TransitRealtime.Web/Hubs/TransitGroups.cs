namespace TransitRealtime.Web.Hubs;

public static class TransitGroups
{
    /// <summary>Broadcast-all group for a service (JoinService), or a scoped group for a single recipient (JoinChannel).</summary>
    public static string For(Guid serviceId, string? recipientKey) =>
        string.IsNullOrEmpty(recipientKey) ? serviceId.ToString() : $"{serviceId}::{recipientKey}";
}
