using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Spotster.Hubs;

public class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
