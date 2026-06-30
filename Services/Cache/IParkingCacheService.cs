using Spotster.DTOs;
using Spotster.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace Spotster.Services.Cache;

public interface IParkingCacheService
{
    Task<IReadOnlyList<ParkingReportDto>> GetActiveAsync();
    Task InvalidateAsync();
    Task RefreshAsync();
}

public class ParkingCacheService : IParkingCacheService
{
    private const string CacheKey = "parking:active";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    private readonly IParkingRepository _parking;
    private readonly IDistributedCache _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ParkingCacheService(IParkingRepository parking, IDistributedCache cache)
    {
        _parking = parking;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ParkingReportDto>> GetActiveAsync()
    {
        var cached = await DistributedCacheJson.GetAsync<List<ParkingReportDto>>(_cache, CacheKey);
        if (cached is not null)
        {
            return cached;
        }

        return await RefreshInternalAsync();
    }

    public Task InvalidateAsync() => DistributedCacheJson.RemoveAsync(_cache, CacheKey);

    public Task RefreshAsync() => RefreshInternalAsync();

    private async Task<IReadOnlyList<ParkingReportDto>> RefreshInternalAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var cached = await DistributedCacheJson.GetAsync<List<ParkingReportDto>>(_cache, CacheKey);
            if (cached is not null)
            {
                return cached;
            }

            var reports = await _parking.GetActiveAsync();
            var dtos = reports.Select(r => ParkingMapper.ToDto(r)).ToList();
            await DistributedCacheJson.SetAsync(_cache, CacheKey, dtos, CacheTtl);
            return dtos;
        }
        finally
        {
            _lock.Release();
        }
    }
}
