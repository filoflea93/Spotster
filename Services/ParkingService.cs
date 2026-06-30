using Spotster.Domain;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Infrastructure.Geo;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services.AntiFraud;
using Spotster.Services.Cache;
using Spotster.Services.Geo;
using Spotster.Services.Realtime;
using Microsoft.Extensions.Localization;

namespace Spotster.Services;

public class ParkingService : IParkingService
{
    private readonly IParkingRepository _parking;
    private readonly IUserRepository _users;
    private readonly IReputationService _reputation;
    private readonly IPhotoStorageService _photoStorage;
    private readonly IAntiFraudService _antiFraud;
    private readonly IGeoService _geo;
    private readonly IParkingCacheService _cache;
    private readonly IParkingRequestService _parkingRequests;
    private readonly IParkingRealtimeNotifier _notifier;
    private readonly ILogger<ParkingService> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ParkingService(
        IParkingRepository parking,
        IUserRepository users,
        IReputationService reputation,
        IPhotoStorageService photoStorage,
        IAntiFraudService antiFraud,
        IGeoService geo,
        IParkingCacheService cache,
        IParkingRequestService parkingRequests,
        IParkingRealtimeNotifier notifier,
        ILogger<ParkingService> logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _parking = parking;
        _users = users;
        _reputation = reputation;
        _photoStorage = photoStorage;
        _antiFraud = antiFraud;
        _geo = geo;
        _cache = cache;
        _parkingRequests = parkingRequests;
        _notifier = notifier;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<ParkingReportDto> CreateReportAsync(
        Guid userId,
        CreateParkingReportRequest request,
        IFormFile? photo,
        string? ipAddress,
        string? userAgent)
    {
        var fraudCheck = await _antiFraud.ValidateReportAsync(
            userId, request.Latitude, request.Longitude, ipAddress, userAgent);

        if (!fraudCheck.IsAllowed)
        {
            throw new InvalidOperationException(fraudCheck.DenyReason ?? _localizer["Error_ReportNotAllowed"]);
        }

        var user = await _users.GetByIdAsync(userId)
            ?? throw new UnauthorizedAccessException(_localizer["Error_UserNotFound"]);

        var normalized = _geo.NormalizeLocation(request.Latitude, request.Longitude);
        var existing = await _geo.FindAggregatableReportAsync(request.Latitude, request.Longitude);
        var now = DateTime.UtcNow;

        if (existing is not null && existing.CreatedByUserId != userId)
        {
            existing = null;
        }

        if (existing is not null)
        {
            GeoPointFactory.ApplyCoordinates(existing, request.Latitude, request.Longitude);
            existing.VirtualZoneKey = normalized.ZoneKey;
            existing.LastUpdatedAt = now;
            existing.ExpiresAt = now.AddMinutes(ListingConstants.TtlMinutes);
            existing.Status = ParkingStatus.Active;
            existing.ReportCount++;

            if (photo is not null && photo.Length > 0)
            {
                _photoStorage.DeletePhoto(existing.PhotoUrl);
                existing.PhotoUrl = await _photoStorage.SaveParkingPhotoAsync(photo, existing.Id);
            }

            var validVotes = existing.Votes.Count(v => v.IsValid);
            var invalidVotes = existing.Votes.Count(v => !v.IsValid);
            existing.ConfidenceScore = _geo.CalculateConfidence(existing, validVotes, invalidVotes);

            await _parking.SaveChangesAsync();
            await _cache.InvalidateAsync();
            await _parkingRequests.NotifyNearbyRequestsOnReportAsync(
                request.Latitude, request.Longitude, userId);

            var updatedDto = ParkingMapper.ToDto(existing);
            await _notifier.BroadcastMapEventAsync(
                existing.Latitude, existing.Longitude, "ParkingUpdated", ParkingMapper.ToSignalDto(existing));
            _logger.LogInformation("Parking report {Id} aggregated in zone {Zone}", existing.Id, normalized.ZoneKey);
            return updatedDto;
        }

        var activeReports = await _parking.CountActiveReportsByUserAsync(userId);
        if (activeReports >= ListingConstants.MaxActiveReportsPerUser)
        {
            throw new InvalidOperationException(
                _localizer["Error_MaxActiveReports", ListingConstants.MaxActiveReportsPerUser]);
        }

        var reportId = Guid.NewGuid();
        var photoUrl = await _photoStorage.SaveParkingPhotoAsync(photo, reportId);

        var report = new ParkingReport
        {
            Id = reportId,
            VirtualZoneKey = normalized.ZoneKey,
            CreatedAt = now,
            LastUpdatedAt = now,
            ExpiresAt = now.AddMinutes(ListingConstants.TtlMinutes),
            Status = ParkingStatus.Active,
            PhotoUrl = photoUrl,
            ConfidenceScore = 50,
            ReportCount = 1,
            CreatedByUserId = userId
        };
        GeoPointFactory.ApplyCoordinates(report, request.Latitude, request.Longitude);

        await _parking.AddAsync(report);
        report.CreatedByUser = user;
        await _parking.SaveChangesAsync();
        await _cache.InvalidateAsync();
        await _parkingRequests.NotifyNearbyRequestsOnReportAsync(
            request.Latitude, request.Longitude, userId);

        var dto = ParkingMapper.ToDto(report);
        await _notifier.BroadcastMapEventAsync(
            report.Latitude, report.Longitude, "ParkingCreated", ParkingMapper.ToSignalDto(report));
        _logger.LogInformation("New parking report {Id} in zone {Zone} by {User}", report.Id, normalized.ZoneKey, user.UserName);
        return dto;
    }

    public Task<IReadOnlyList<ParkingReportDto>> GetActiveAsync() => _cache.GetActiveAsync();

    public async Task<IReadOnlyList<ParkingReportDto>> GetMyActiveAsync(Guid userId)
    {
        var reports = await _parking.GetActiveByUserAsync(userId);
        return reports.Select(r => ParkingMapper.ToDto(r, userId)).ToList();
    }

    public async Task<PagedResult<ParkingReportDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        int page,
        int pageSize,
        Guid? viewerUserId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        radiusMeters = Math.Clamp(radiusMeters, 50, Domain.Geo.GeoConstants.NearbyMaxRadiusMeters);

        var total = await _parking.CountNearbyAsync(latitude, longitude, radiusMeters);
        var reports = await _parking.GetNearbyAsync(latitude, longitude, radiusMeters, page, pageSize);
        var items = reports.Select(r => ParkingMapper.ToDto(r, viewerUserId)).ToList();

        return new PagedResult<ParkingReportDto>(items, page, pageSize, total);
    }

    public async Task<ParkingReportDto> VoteAsync(Guid userId, VoteRequest request)
    {
        if (!await _antiFraud.IsUserAllowedAsync(userId))
        {
            throw new InvalidOperationException(_localizer["Error_AccountUnauthorizedVote"]);
        }

        var report = await _parking.GetByIdAsync(request.ParkingReportId)
            ?? throw new KeyNotFoundException(_localizer["Error_ReportNotFound"]);

        if (report.Status != ParkingStatus.Active || report.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(_localizer["Error_VoteNotAllowed"]);
        }

        if (report.Votes.Any(v => v.UserId == userId))
        {
            throw new InvalidOperationException(_localizer["Error_AlreadyVoted"]);
        }

        if (report.CreatedByUserId == userId)
        {
            throw new InvalidOperationException(_localizer["Error_VoteOwnReport"]);
        }

        var vote = new ReportVote
        {
            Id = Guid.NewGuid(),
            ParkingReportId = report.Id,
            UserId = userId,
            IsValid = request.IsValid,
            CreatedAt = DateTime.UtcNow
        };

        await _parking.AddVoteAsync(vote);
        report.Votes.Add(vote);

        var validVotes = report.Votes.Count(v => v.IsValid);
        var invalidVotes = report.Votes.Count(v => !v.IsValid);
        report.ConfidenceScore = _geo.CalculateConfidence(report, validVotes, invalidVotes);
        report.LastUpdatedAt = DateTime.UtcNow;

        if (!request.IsValid && invalidVotes >= 2)
        {
            report.Status = ParkingStatus.Invalid;
            await _reputation.ApplyFalseReportAsync(report.CreatedByUserId);
        }

        await _parking.SaveChangesAsync();
        await _cache.InvalidateAsync();

        var dto = ParkingMapper.ToDto(report, userId);
        await _notifier.BroadcastMapEventAsync(
            report.Latitude, report.Longitude, "ParkingUpdated", ParkingMapper.ToSignalDto(report));
        return dto;
    }

    public async Task DeleteReportAsync(Guid userId, Guid reportId)
    {
        var report = await _parking.GetByIdAsync(reportId)
            ?? throw new KeyNotFoundException(_localizer["Error_ReportNotFound"]);

        if (report.CreatedByUserId != userId)
        {
            throw new UnauthorizedAccessException(_localizer["Error_DeleteReportOwnOnly"]);
        }

        _photoStorage.DeletePhoto(report.PhotoUrl);
        await _notifier.BroadcastMapEventAsync(
            report.Latitude, report.Longitude, "ParkingExpired", ParkingMapper.ToSignalDto(report));
        _parking.Delete(report);
        await _parking.SaveChangesAsync();
        await _cache.InvalidateAsync();
        _logger.LogInformation("Report {Id} deleted by {UserId}", reportId, userId);
    }

    public async Task ProcessExpiredReportsAsync()
    {
        var toPurge = await _parking.GetReadyToPurgeAsync(DateTime.UtcNow);
        if (toPurge.Count == 0)
        {
            return;
        }

        var since = DateTime.UtcNow.AddMinutes(-ListingConstants.TtlMinutes);

        foreach (var report in toPurge)
        {
            if (report.Status == ParkingStatus.Active)
            {
                var validVotes = report.Votes.Count(v => v.IsValid);
                var invalidVotes = report.Votes.Count(v => !v.IsValid);

                if (validVotes >= invalidVotes)
                {
                    var consecutive = await _parking.CountRecentReportsByUserAsync(report.CreatedByUserId, since);
                    await _reputation.ApplyConfirmedValidAsync(report.CreatedByUserId, consecutive);
                }

                await _reputation.ApplyVoteOutcomesAsync(report);
            }
            else if (report.Status == ParkingStatus.Invalid)
            {
                await _reputation.ApplyVoteOutcomesAsync(report);
            }

            _photoStorage.DeletePhoto(report.PhotoUrl);
            await _notifier.BroadcastMapEventAsync(
                report.Latitude, report.Longitude, "ParkingExpired", ParkingMapper.ToSignalDto(report));
            _parking.Delete(report);
            _logger.LogInformation("Parking report {Id} deleted after expiration", report.Id);
        }

        await _parking.SaveChangesAsync();
        await _cache.InvalidateAsync();
    }
}
