using Spotster.DTOs;
using Spotster.Repositories;
using Microsoft.Extensions.Caching.Distributed;

namespace Spotster.Services.Cache;

public interface IParkingRequestCacheService
{
    Task<IReadOnlyList<ParkingRequestDto>> GetActiveAsync();
    Task InvalidateAsync();
    Task RefreshAsync();
}

public class ParkingRequestCacheService : IParkingRequestCacheService
{
    private const string CacheKey = "requests:active";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    private readonly IParkingRequestRepository _requests;
    private readonly IDistributedCache _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ParkingRequestCacheService(IParkingRequestRepository requests, IDistributedCache cache)
    {
        _requests = requests;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ParkingRequestDto>> GetActiveAsync()
    {
        var cached = await DistributedCacheJson.GetAsync<List<ParkingRequestDto>>(_cache, CacheKey);
        if (cached is not null)
        {
            return cached;
        }

        return await RefreshInternalAsync();
    }

    public Task InvalidateAsync() => DistributedCacheJson.RemoveAsync(_cache, CacheKey);

    public Task RefreshAsync() => RefreshInternalAsync();

    private async Task<IReadOnlyList<ParkingRequestDto>> RefreshInternalAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var cached = await DistributedCacheJson.GetAsync<List<ParkingRequestDto>>(_cache, CacheKey);
            if (cached is not null)
            {
                return cached;
            }

            var requests = await _requests.GetActiveAsync();
            var dtos = requests.Select(r => ParkingRequestMapper.ToDto(r)).ToList();
            await DistributedCacheJson.SetAsync(_cache, CacheKey, dtos, CacheTtl);
            return dtos;
        }
        finally
        {
            _lock.Release();
        }
    }
}
