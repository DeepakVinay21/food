using FoodExpirationTracker.Application.Services;

namespace FoodExpirationTracker.Api.Middleware;

public class DailyExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyExpiryHostedService> _logger;

    public DailyExpiryHostedService(IServiceScopeFactory scopeFactory, ILogger<DailyExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once on startup after a short delay (let the app finish initializing)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await RunScanAsync(stoppingToken);

        // Then run at scheduled intervals
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc();
            _logger.LogInformation("Next expiry scan scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);
            await RunScanAsync(stoppingToken);
        }
    }

    private async Task RunScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.RunDailyExpiryScanForAllUsersAsync(stoppingToken);
            _logger.LogInformation("Expiry scan completed at {Timestamp}", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expiry scan failed");
        }
    }

    /// <summary>
    /// Runs at 8:00 UTC and 20:00 UTC (twice daily), whichever comes next.
    /// </summary>
    private static TimeSpan GetDelayUntilNextRunUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var today8 = new DateTimeOffset(now.Year, now.Month, now.Day, 8, 0, 0, TimeSpan.Zero);
        var today20 = new DateTimeOffset(now.Year, now.Month, now.Day, 20, 0, 0, TimeSpan.Zero);

        DateTimeOffset next;
        if (now < today8)
            next = today8;
        else if (now < today20)
            next = today20;
        else
            next = today8.AddDays(1);

        return next - now;
    }
}
