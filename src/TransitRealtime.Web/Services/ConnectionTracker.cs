using System.Collections.Concurrent;

namespace TransitRealtime.Web.Services;

public class ConnectionTracker
{
    private readonly ConcurrentDictionary<string, int> _countsByService = new();

    public int Increment(string serviceId) =>
        _countsByService.AddOrUpdate(serviceId, 1, (_, count) => count + 1);

    public int Decrement(string serviceId) =>
        _countsByService.AddOrUpdate(serviceId, 0, (_, count) => Math.Max(0, count - 1));

    public int GetCount(string serviceId) =>
        _countsByService.GetValueOrDefault(serviceId, 0);
}
