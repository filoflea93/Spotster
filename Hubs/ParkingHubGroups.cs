namespace Spotster.Hubs;

public static class ParkingHubGroups
{
    public static string Grid(string gridKey) => $"grid:{gridKey}";

    public static string Request(Guid requestId) => $"request:{requestId:D}";
}
