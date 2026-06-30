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

    public async Task<ParkingReport?> FindAggregatableReportAsync(double latitude, double longitude)
    {
        var zoneKey = GeoHelper.GetVirtualZoneKey(latitude, longitude);
        var inZone = await _parking.FindActiveInZoneAsync(zoneKey);
        if (inZone is not null &&
            GeoHelper.DistanceMeters(latitude, longitude, inZone.Latitude, inZone.Longitude) <= GeoConstants.ClusterRadiusMeters)
        {
            return inZone;
        }

        var active = await _parking.GetActiveAsync();
        return active
            .Where(p => GeoHelper.DistanceMeters(latitude, longitude, p.Latitude, p.Longitude) <= GeoConstants.ClusterRadiusMeters)
            .OrderBy(p => GeoHelper.DistanceMeters(latitude, longitude, p.Latitude, p.Longitude))
            .FirstOrDefault();
    }

    public double CalculateConfidence(ParkingReport report, int validVotes, int invalidVotes)
    {
        var score = report.ConfidenceScore;
        score += validVotes * 8;
        score -= invalidVotes * 12;
        score += Math.Min(report.ReportCount - 1, 5) * 3;
        return Math.Clamp(score, 0, 100);
    }
}
