using Spotster.Domain.Geo;
using Microsoft.AspNetCore.SignalR;

namespace Spotster.Hubs;

[Microsoft.AspNetCore.Authorization.Authorize]
public class ParkingHub : Hub
{
    private const string GridKeysItem = "signalr:gridKeys";
    private const string RequestGroupItem = "signalr:requestGroup";

    public async Task SetMapViewport(double latitude, double longitude, double radiusMeters)
    {
        radiusMeters = Math.Clamp(radiusMeters, 50, GeoConstants.NearbyMaxRadiusMeters);

        if (Context.Items.TryGetValue(GridKeysItem, out var existing) && existing is HashSet<string> previous)
        {
            foreach (var key in previous)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, ParkingHubGroups.Grid(key));
            }
        }

        var keys = GeoHelper.GetSignalRGridKeysForRadius(latitude, longitude, radiusMeters).ToHashSet();
        foreach (var key in keys)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ParkingHubGroups.Grid(key));
        }

        Context.Items[GridKeysItem] = keys;
    }

    public async Task JoinRequestChat(Guid requestId)
    {
        if (Context.Items.TryGetValue(RequestGroupItem, out var existing) &&
            existing is string previous &&
            !string.IsNullOrEmpty(previous))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous);
        }

        var group = ParkingHubGroups.Request(requestId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        Context.Items[RequestGroupItem] = group;
    }

    public async Task LeaveRequestChat(Guid requestId)
    {
        var group = ParkingHubGroups.Request(requestId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        if (Context.Items.TryGetValue(RequestGroupItem, out var existing) &&
            Equals(existing, group))
        {
            Context.Items.Remove(RequestGroupItem);
        }
    }
}
