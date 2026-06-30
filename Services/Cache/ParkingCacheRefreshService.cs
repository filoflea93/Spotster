namespace Spotster.Services.Cache;

public class ParkingCacheRefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ParkingCacheRefreshService> _logger;

    public ParkingCacheRefreshService(IServiceProvider services, ILogger<ParkingCacheRefreshService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var cache = scope.ServiceProvider.GetRequiredService<IParkingCacheService>();
                await cache.RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing parking cache");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
