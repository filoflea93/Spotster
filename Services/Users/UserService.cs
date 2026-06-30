using Spotster.Domain.Enums;
using Spotster.Domain.Reputation;
using Spotster.DTOs;
using Spotster.Entities;
using Spotster.Repositories;
using Spotster.Resources;
using Spotster.Services;
using Microsoft.Extensions.Localization;

namespace Spotster.Services.Users;

public interface IUserService
{
    Task<PagedResult<LeaderboardEntryDto>> GetLeaderboardAsync(int page, int pageSize);
    Task<UserProfileDto> GetProfileAsync(Guid userId);
    Task<string> UpdateProfilePhotoAsync(Guid userId, IFormFile photo);
    Task RemoveProfilePhotoAsync(Guid userId);
    Task ApplyDailyBonusIfEligibleAsync(User entity);
    void RecalculateReputation(User entity);
}

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IPhotoStorageService _photoStorage;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UserService(
        IUserRepository users,
        IPhotoStorageService photoStorage,
        IStringLocalizer<SharedResources> localizer)
    {
        _users = users;
        _photoStorage = photoStorage;
        _localizer = localizer;
    }

    public async Task<PagedResult<LeaderboardEntryDto>> GetLeaderboardAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var (items, total) = await _users.GetLeaderboardAsync(page, pageSize);
        var rankStart = (page - 1) * pageSize + 1;

        var entries = items.Select((user, index) => new LeaderboardEntryDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.ReputationScore,
            user.AccuracyRate,
            user.VerifiedReports,
            rankStart + index)).ToList();

        return new PagedResult<LeaderboardEntryDto>(entries, page, pageSize, total);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException(_localizer["Error_UserNotFound"]);

        RecalculateReputation(user);
        return new UserProfileDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.ReputationScore,
            user.PositiveReports,
            user.NegativeReports,
            user.VerifiedReports,
            user.FalseReports,
            user.VotesCorrect,
            user.AccuracyRate,
            user.Status.ToString(),
            user.ProfilePhotoUrl);
    }

    public async Task<string> UpdateProfilePhotoAsync(Guid userId, IFormFile photo)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException(_localizer["Error_UserNotFound"]);

        _photoStorage.DeletePhoto(user.ProfilePhotoUrl);
        user.ProfilePhotoUrl = await _photoStorage.SaveProfilePhotoAsync(photo, userId);
        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();
        return user.ProfilePhotoUrl;
    }

    public async Task RemoveProfilePhotoAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException(_localizer["Error_UserNotFound"]);

        _photoStorage.DeletePhoto(user.ProfilePhotoUrl);
        user.ProfilePhotoUrl = null;
        user.LastActivityAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();
    }

    public async Task ApplyDailyBonusIfEligibleAsync(User entity)
    {
        if (entity.AccuracyRate < 0.7 || entity.VerifiedReports < 3)
        {
            return;
        }

        if (entity.LastDailyBonusAt.HasValue &&
            entity.LastDailyBonusAt.Value > DateTime.UtcNow.AddHours(-24))
        {
            return;
        }

        entity.ReputationScore += ReputationCalculator.DailyBonusPoints;
        entity.LastDailyBonusAt = DateTime.UtcNow;
        await _users.SaveChangesAsync();
    }

    public void RecalculateReputation(User entity)
    {
        entity.AccuracyRate = ReputationCalculator.CalculateAccuracyRate(
            entity.PositiveReports,
            entity.NegativeReports);
        entity.ReputationScore = ReputationCalculator.CalculateScore(
            entity.VerifiedReports,
            entity.FalseReports,
            entity.VotesCorrect);
    }
}
