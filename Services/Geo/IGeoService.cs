using Spotster.Domain.Geo;
using Spotster.Entities;
using Spotster.Repositories;

namespace Spotster.Services.Geo;

public interface IGeoService
{
    (double Latitude, double Longitude, string ZoneKey) NormalizeLocation(double latitude, double longitude);
    Task<ParkingReport?> FindAggregatableReportAsync(double latitude, double longitude);
    double CalculateConfidence(ParkingReport report, int validVotes, int invalidVotes);
}

public class GeoService : IGeoService
{
    private readonly IParkingRepository _parking;

    public GeoService(IParkingRepository parking)
    {
        _parking = parking;
    }

    public (double Latitude, double Longitude, string ZoneKey) NormalizeLocation(double latitude, double longitude)
    {
        var snapped = GeoHelper.SnapToVirtualZone(latitude, longitude);
        var zoneKey = GeoHelper.GetVirtualZoneKey(latitude, longitude);
        return (snapped.Latitude, snapped.Longitude, zoneKey);
    }

    public Task<ParkingReport?> FindAggregatableReportAsync(double latitude, double longitude) =>
        _parking.FindNearestActiveAsync(latitude, longitude, GeoConstants.ClusterRadiusMeters);

    public double CalculateConfidence(ParkingReport report, int validVotes, int invalidVotes)
    {
        var score = report.ConfidenceScore;
        score += validVotes * 8;
        score -= invalidVotes * 12;
        score += Math.Min(report.ReportCount - 1, 5) * 3;
        return Math.Clamp(score, 0, 100);
    }
}
