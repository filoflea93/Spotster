namespace Spotster.DTOs;

public record LeaderboardEntryDto(
    Guid UserId,
    string Username,
    int ReputationScore,
    double AccuracyRate,
    int VerifiedReports,
    int Rank);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record UserProfileDto(
    Guid UserId,
    string Username,
    int ReputationScore,
    int PositiveReports,
    int NegativeReports,
    int VerifiedReports,
    int FalseReports,
    int VotesCorrect,
    double AccuracyRate,
    string Status,
    string? ProfilePhotoUrl);
