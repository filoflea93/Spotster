using NetTopologySuite.Geometries;
using Spotster.Entities;

namespace Spotster.Infrastructure.Geo;

public static class GeoPointFactory
{
    public static Point Create(double latitude, double longitude) =>
        new(longitude, latitude) { SRID = 4326 };

    public static void ApplyCoordinates(ParkingReport report, double latitude, double longitude)
    {
        report.Latitude = latitude;
        report.Longitude = longitude;
        report.Location = Create(latitude, longitude);
    }

    public static void ApplyCoordinates(ParkingRequest request, double latitude, double longitude)
    {
        request.Latitude = latitude;
        request.Longitude = longitude;
        request.Location = Create(latitude, longitude);
    }
}
