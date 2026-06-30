namespace Spotster.Domain.Geo;

public static class GeoConstants
{
    public const double ClusterRadiusMeters = 20;
    public const double NearbyDefaultRadiusMeters = 500;
    public const double NearbyMaxRadiusMeters = 5000;
    public const double VirtualZoneCellDegrees = 0.00018;
    /// <summary>~1 km cells used for SignalR map viewport groups.</summary>
    public const double SignalRGridCellDegrees = 0.009;
    public const double SignalRGridCellMeters = 1000;
    public const double MaxSpeedKmh = 200;
    public const double EarthRadiusMeters = 6371000;
}
