namespace Spotster.Services;

public class ParkingExpirationService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ParkingExpirationService> _logger;

    public ParkingExpirationService(IServiceProvider services, ILogger<ParkingExpirationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ParkingExpirationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var parkingService = scope.ServiceProvider.GetRequiredService<IParkingService>();
                var requestService = scope.ServiceProvider.GetRequiredService<IParkingRequestService>();
                await parkingService.ProcessExpiredReportsAsync();
                await requestService.ProcessExpiredRequestsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing expirations");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
