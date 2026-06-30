using Spotster.Domain.Geo;
using Microsoft.AspNetCore.SignalR;

namespace Spotster.Services.Realtime;

public interface IParkingRealtimeNotifier
{
    Task BroadcastMapEventAsync(double latitude, double longitude, string method, object? payload);

    Task BroadcastMapEventAsync(string virtualZoneKey, double latitude, double longitude, string method, object? payload);

    Task NotifyUserAsync(Guid userId, string method, object? payload);

    Task NotifyRequestAsync(Guid requestId, string method, object? payload);
}

public class ParkingRealtimeNotifier : IParkingRealtimeNotifier
{
    private readonly IHubContext<Hubs.ParkingHub> _hub;

    public ParkingRealtimeNotifier(IHubContext<Hubs.ParkingHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastMapEventAsync(double latitude, double longitude, string method, object? payload) =>
        BroadcastMapEventAsync(string.Empty, latitude, longitude, method, payload);

    public async Task BroadcastMapEventAsync(
        string virtualZoneKey,
        double latitude,
        double longitude,
        string method,
        object? payload)
    {
        var gridKeys = GeoHelper.GetSignalRGridKeysForRadius(latitude, longitude, GeoConstants.SignalRGridCellMeters);
        var tasks = gridKeys
            .Select(key => _hub.Clients.Group(Hubs.ParkingHubGroups.Grid(key)).SendAsync(method, payload))
            .ToList();

        if (!string.IsNullOrWhiteSpace(virtualZoneKey))
        {
            _ = virtualZoneKey;
        }

        await Task.WhenAll(tasks);
    }

    public Task NotifyUserAsync(Guid userId, string method, object? payload) =>
        _hub.Clients.User(userId.ToString()).SendAsync(method, payload);

    public Task NotifyRequestAsync(Guid requestId, string method, object? payload) =>
        _hub.Clients.Group(Hubs.ParkingHubGroups.Request(requestId)).SendAsync(method, payload);
}
