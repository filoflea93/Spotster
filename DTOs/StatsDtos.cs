namespace Spotster.DTOs;

public record SystemStatsDto(
    int ActiveUsers,
    int ActiveParkings,
    int TotalUsers,
    int TotalReportsToday,
    int SuspendedUsers,
    int BannedUsers,
    DateTime GeneratedAt);
