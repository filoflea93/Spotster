using Spotster.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Spotster.Entities;

public class User : IdentityUser<Guid>
{
    public int ReputationScore { get; set; }
    public int PositiveReports { get; set; }
    public int NegativeReports { get; set; }
    public int VerifiedReports { get; set; }
    public int FalseReports { get; set; }
    public int VotesCorrect { get; set; }
    public double AccuracyRate { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public int SuspiciousScore { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? LastDailyBonusAt { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime? LastLocationAt { get; set; }
    public string? DeviceFingerprintHash { get; set; }
    public string? LastIpHash { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    public ICollection<ParkingReport> ParkingReports { get; set; } = new List<ParkingReport>();
    public ICollection<ParkingRequest> ParkingRequests { get; set; } = new List<ParkingRequest>();
    public ICollection<ReportVote> Votes { get; set; } = new List<ReportVote>();
    public ICollection<UserReview> ReviewsGiven { get; set; } = new List<UserReview>();
    public ICollection<UserReview> ReviewsReceived { get; set; } = new List<UserReview>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
