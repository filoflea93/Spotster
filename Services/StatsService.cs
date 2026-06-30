using Spotster.Domain.Enums;
using Spotster.DTOs;
using Spotster.Repositories;

namespace Spotster.Services;

public interface IStatsService
{
    Task<SystemStatsDto> GetSystemStatsAsync();
}

public class StatsService : IStatsService
{
    private readonly IUserRepository _users;
    private readonly IParkingRepository _parking;

    public StatsService(IUserRepository users, IParkingRepository parking)
    {
        _users = users;
        _parking = parking;
    }

    public async Task<SystemStatsDto> GetSystemStatsAsync()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        return new SystemStatsDto(
            await _users.CountActiveSinceAsync(since),
            await _parking.CountActiveAsync(),
            await _users.CountAllAsync(),
            await _parking.CountReportsTodayAsync(),
            await _users.CountByStatusAsync(UserStatus.Suspended),
            await _users.CountByStatusAsync(UserStatus.Banned),
            DateTime.UtcNow);
    }
}
