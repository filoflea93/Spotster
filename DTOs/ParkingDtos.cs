using Spotster.Entities;

namespace Spotster.DTOs;

public record CreateParkingReportRequest(double Latitude, double Longitude);

public record VoteRequest(Guid ParkingReportId, bool IsValid);

public record ParkingReportDto(
    Guid Id,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    DateTime LastUpdatedAt,
    DateTime ExpiresAt,
    ParkingStatus Status,
    string CreatedByUsername,
    Guid CreatedByUserId,
    int ValidVotes,
    int InvalidVotes,
    string MarkerColor,
    string? PhotoUrl,
    double ConfidenceScore,
    int ReportCount,
    bool HasVotedByMe = false);

public record ParkingSignalDto(
    Guid Id,
    double Latitude,
    double Longitude,
    DateTime ExpiresAt,
    string MarkerColor,
    string? PhotoUrl,
    double ConfidenceScore);

public record SuspiciousActivityDto(
    Guid UserId,
    string Message,
    int SuspiciousScore,
    DateTime OccurredAt);
