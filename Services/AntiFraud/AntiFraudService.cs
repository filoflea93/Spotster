using System.Security.Cryptography;
using System.Text;
using Spotster.Domain.Enums;
using Spotster.Domain.Geo;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services.Realtime;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;

namespace Spotster.Services.AntiFraud;

public class AntiFraudService : IAntiFraudService
{
    private const int MaxReportsPerWindow = 5;
    private const int RateWindowMinutes = 10;
    private const int MaxIpReportsPerWindow = 8;
    private const int SuspiciousThreshold = 50;
    private const int SuspensionMinutes = 30;
    private const int SpamZoneReportsThreshold = 3;

    private readonly IUserRepository _users;
    private readonly IParkingRepository _parking;
    private readonly IDistributedCache _cache;
    private readonly IParkingRealtimeNotifier _notifier;
    private readonly ILogger<AntiFraudService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AntiFraudService(
        IUserRepository users,
        IParkingRepository parking,
        IDistributedCache cache,
        IParkingRealtimeNotifier notifier,
        ILogger<AntiFraudService> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _users = users;
        _parking = parking;
        _cache = cache;
        _notifier = notifier;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<FraudCheckResult> ValidateReportAsync(
        Guid userId,
        double latitude,
        double longitude,
        string? ipAddress,
        string? userAgent)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return new FraudCheckResult { IsAllowed = false, DenyReason = _localizer["Error_UserNotFound"] };
        }

        await RestoreSuspendedIfExpiredAsync(user);

        if (user.Status == UserStatus.Banned)
        {
            return new FraudCheckResult { IsAllowed = false, DenyReason = _localizer["Error_AccountBanned"] };
        }

        if (user.Status == UserStatus.Suspended)
        {
            return new FraudCheckResult
            {
                IsAllowed = false,
                DenyReason = _localizer["Error_AccountSuspended", user.SuspendedUntil?.ToString("HH:mm") ?? ""]
            };
        }

        var since = DateTime.UtcNow.AddMinutes(-RateWindowMinutes);
        var userReports = await _parking.CountRecentReportsByUserAsync(userId, since);
        if (userReports >= MaxReportsPerWindow)
        {
            await RecordSuspiciousActivityAsync(userId, 10, "User rate limit exceeded");
            return new FraudCheckResult
            {
                IsAllowed = false,
                DenyReason = _localizer["Error_ReportRateLimit"]
            };
        }

        var ipHash = ComputeIpHash(ipAddress);
        var ipKey = $"rate:ip:{ipHash}";
        var ipCountRaw = await _cache.GetStringAsync(ipKey);
        var ipCount = int.TryParse(ipCountRaw, out var parsedIpCount) ? parsedIpCount : 0;

        if (ipCount >= MaxIpReportsPerWindow)
        {
            await RecordSuspiciousActivityAsync(userId, 15, "IP rate limit exceeded");
            return new FraudCheckResult
            {
                IsAllowed = false,
                DenyReason = _localizer["Error_ReportDeviceLimit"]
            };
        }

        await _cache.SetStringAsync(
            ipKey,
            (ipCount + 1).ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(RateWindowMinutes)
            });

        var zoneKey = GeoHelper.GetVirtualZoneKey(latitude, longitude);
        var zoneSpamCount = await _parking.CountRecentReportsAtZoneAsync(userId, zoneKey, since);
        var isSpamPattern = zoneSpamCount >= SpamZoneReportsThreshold;

        var geoSpoof = CheckGeoSpoofing(user, latitude, longitude);
        if (geoSpoof.IsSuspicious)
        {
            await RecordSuspiciousActivityAsync(userId, geoSpoof.SuspiciousScoreDelta, geoSpoof.SuspiciousMessage!);
            return new FraudCheckResult
            {
                IsAllowed = false,
                DenyReason = _localizer["Error_GpsSuspicious"],
                IsSuspicious = true,
                SuspiciousScoreDelta = geoSpoof.SuspiciousScoreDelta,
                SuspiciousMessage = geoSpoof.SuspiciousMessage
            };
        }

        user.DeviceFingerprintHash = ComputeDeviceFingerprint(userAgent, ipAddress);
        user.LastIpHash = ipHash;
        user.LastLatitude = latitude;
        user.LastLongitude = longitude;
        user.LastLocationAt = DateTime.UtcNow;
        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();

        if (isSpamPattern)
        {
            await RecordSuspiciousActivityAsync(userId, 20, "Repeated reports at the same location");
            return new FraudCheckResult
            {
                IsAllowed = true,
                IsSuspicious = true,
                SuspiciousScoreDelta = 20,
                SuspiciousMessage = "Clustered report pattern detected"
            };
        }

        return new FraudCheckResult { IsAllowed = true };
    }

    public async Task RecordSuspiciousActivityAsync(Guid userId, int scoreDelta, string reason)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        user.SuspiciousScore += scoreDelta;
        user.ReputationScore = Math.Max(0, user.ReputationScore - Math.Min(scoreDelta, 15));

        if (user.SuspiciousScore >= SuspiciousThreshold && user.Status == UserStatus.Active)
        {
            user.Status = UserStatus.Suspended;
            user.SuspendedUntil = DateTime.UtcNow.AddMinutes(SuspensionMinutes);
            _logger.LogWarning("User {UserId} suspended for suspicious activity", userId);
        }

        await _users.SaveChangesAsync();

        var suspiciousPayload = new
        {
            userId,
            message = reason,
            suspiciousScore = user.SuspiciousScore,
            status = user.Status.ToString(),
            suspendedUntil = user.SuspendedUntil,
            occurredAt = DateTime.UtcNow
        };
        await _notifier.NotifyUserAsync(userId, "UserSuspiciousActivity", suspiciousPayload);
    }

    public string ComputeDeviceFingerprint(string? userAgent, string? ipAddress)
    {
        var raw = $"{userAgent ?? "unknown"}|{ipAddress ?? "unknown"}";
        return Hash(raw);
    }

    public string ComputeIpHash(string? ipAddress) => Hash(ipAddress ?? "unknown");

    public async Task<bool> IsUserAllowedAsync(Guid userId)
    {
        var user = await SyncUserStatusAsync(userId);
        return user is not null && user.Status == UserStatus.Active;
    }

    public async Task<Entities.User?> SyncUserStatusAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        await RestoreSuspendedIfExpiredAsync(user);
        return user;
    }

    private FraudCheckResult CheckGeoSpoofing(Entities.User user, double latitude, double longitude)
    {
        if (!user.LastLatitude.HasValue || !user.LastLongitude.HasValue || !user.LastLocationAt.HasValue)
        {
            return new FraudCheckResult { IsAllowed = true };
        }

        var lastLat = user.LastLatitude.Value;
        var lastLng = user.LastLongitude.Value;

        if (!IsPlausibleCoordinate(lastLat, lastLng) || !IsPlausibleCoordinate(latitude, longitude))
        {
            return new FraudCheckResult { IsAllowed = true };
        }

        var distanceMeters = GeoHelper.DistanceMeters(lastLat, lastLng, latitude, longitude);
        if (distanceMeters > 100_000)
        {
            // Previous coordinates likely corrupted (e.g. culture parsing) or a real zone change
            return new FraudCheckResult { IsAllowed = true };
        }

        var elapsed = DateTime.UtcNow - user.LastLocationAt.Value;
        if (elapsed.TotalSeconds < 10)
        {
            return new FraudCheckResult { IsAllowed = true };
        }

        var speed = GeoHelper.SpeedKmh(
            lastLat,
            lastLng,
            user.LastLocationAt.Value,
            latitude,
            longitude,
            DateTime.UtcNow);

        if (speed > GeoConstants.MaxSpeedKmh)
        {
            return new FraudCheckResult
            {
                IsAllowed = false,
                IsSuspicious = true,
                SuspiciousScoreDelta = 30,
                SuspiciousMessage = $"Impossible GPS speed: {speed:F0} km/h"
            };
        }

        return new FraudCheckResult { IsAllowed = true };
    }

    private static bool IsPlausibleCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 &&
        longitude is >= -180 and <= 180;

    private async Task RestoreSuspendedIfExpiredAsync(Entities.User user)
    {
        if (user.Status == UserStatus.Suspended &&
            user.SuspendedUntil.HasValue &&
            user.SuspendedUntil <= DateTime.UtcNow)
        {
            user.Status = UserStatus.Active;
            user.SuspendedUntil = null;
            user.SuspiciousScore = Math.Max(0, user.SuspiciousScore - 10);
            await _users.SaveChangesAsync();
        }
    }

    private static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
