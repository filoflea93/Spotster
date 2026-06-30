namespace Spotster.Domain.Geo;

public static class GeoHelper
{
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return GeoConstants.EarthRadiusMeters * c;
    }

    public static double SpeedKmh(double lat1, double lon1, DateTime time1, double lat2, double lon2, DateTime time2)
    {
        var hours = (time2 - time1).TotalHours;
        if (hours <= 0)
        {
            return 0;
        }

        var km = DistanceMeters(lat1, lon1, lat2, lon2) / 1000;
        return km / hours;
    }

    public static string GetVirtualZoneKey(double latitude, double longitude)
    {
        var latCell = Math.Round(latitude / GeoConstants.VirtualZoneCellDegrees);
        var lngCell = Math.Round(longitude / GeoConstants.VirtualZoneCellDegrees);
        return $"{latCell}:{lngCell}";
    }

    public static (double Latitude, double Longitude) SnapToVirtualZone(double latitude, double longitude)
    {
        var latCell = Math.Round(latitude / GeoConstants.VirtualZoneCellDegrees);
        var lngCell = Math.Round(longitude / GeoConstants.VirtualZoneCellDegrees);
        return (latCell * GeoConstants.VirtualZoneCellDegrees, lngCell * GeoConstants.VirtualZoneCellDegrees);
    }

    public static string GetSignalRGridKey(double latitude, double longitude)
    {
        var latCell = Math.Round(latitude / GeoConstants.SignalRGridCellDegrees);
        var lngCell = Math.Round(longitude / GeoConstants.SignalRGridCellDegrees);
        return $"{latCell}:{lngCell}";
    }

    public static IReadOnlyList<string> GetSignalRGridKeysForRadius(double latitude, double longitude, double radiusMeters)
    {
        var steps = Math.Max(0, (int)Math.Ceiling(radiusMeters / GeoConstants.SignalRGridCellMeters));
        var latCell = Math.Round(latitude / GeoConstants.SignalRGridCellDegrees);
        var lngCell = Math.Round(longitude / GeoConstants.SignalRGridCellDegrees);
        var keys = new HashSet<string>();

        for (var dLat = -steps; dLat <= steps; dLat++)
        {
            for (var dLng = -steps; dLng <= steps; dLng++)
            {
                keys.Add($"{latCell + dLat}:{lngCell + dLng}");
            }
        }

        return keys.ToList();
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
