namespace Spotster.Services.Cache;

public class ParkingRequestCacheRefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ParkingRequestCacheRefreshService> _logger;

    public ParkingRequestCacheRefreshService(IServiceProvider services, ILogger<ParkingRequestCacheRefreshService> logger)
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
                var cache = scope.ServiceProvider.GetRequiredService<IParkingRequestCacheService>();
                await cache.RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing parking request cache");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
