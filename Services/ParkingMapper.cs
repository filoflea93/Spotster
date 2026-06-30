using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Repositories;

namespace Spotster.Services;

public static class ParkingMapper
{
    private static (double Latitude, double Longitude) ResolveCoordinates(ParkingReport report)
    {
        var lat = report.Latitude;
        var lng = report.Longitude;
        if (lat == 0 && lng == 0 && report.Location is not null)
        {
            lat = report.Location.Y;
            lng = report.Location.X;
        }

        return (lat, lng);
    }

    public static ParkingReportDto ToDto(ParkingReport report, Guid? viewerUserId = null)
    {
        var validVotes = report.Votes.Count(v => v.IsValid);
        var invalidVotes = report.Votes.Count(v => !v.IsValid);
        var hasVoted = viewerUserId.HasValue
            && report.Votes.Any(v => v.UserId == viewerUserId.Value);
        var (latitude, longitude) = ResolveCoordinates(report);

        return new ParkingReportDto(
            report.Id,
            latitude,
            longitude,
            report.CreatedAt,
            report.LastUpdatedAt,
            report.ExpiresAt,
            report.Status,
            report.CreatedByUser?.UserName ?? "unknown",
            report.CreatedByUserId,
            validVotes,
            invalidVotes,
            GetMarkerColor(report, validVotes, invalidVotes),
            report.PhotoUrl,
            report.ConfidenceScore,
            report.ReportCount,
            hasVoted);
    }

    public static ParkingSignalDto ToSignalDto(ParkingReport report)
    {
        var validVotes = report.Votes.Count(v => v.IsValid);
        var invalidVotes = report.Votes.Count(v => !v.IsValid);
        var (latitude, longitude) = ResolveCoordinates(report);

        return new ParkingSignalDto(
            report.Id,
            latitude,
            longitude,
            report.ExpiresAt,
            GetMarkerColor(report, validVotes, invalidVotes),
            report.PhotoUrl,
            report.ConfidenceScore);
    }

    public static string GetMarkerColor(ParkingReport report, int validVotes, int invalidVotes)
    {
        if (report.Status == ParkingStatus.Expired || report.ExpiresAt <= DateTime.UtcNow)
        {
            return "red";
        }

        if (report.Status == ParkingStatus.Invalid || invalidVotes > validVotes)
        {
            return "red";
        }

        if (report.ConfidenceScore >= 70 || (validVotes > 0 && validVotes > invalidVotes))
        {
            return "green";
        }

        return "yellow";
    }
}
