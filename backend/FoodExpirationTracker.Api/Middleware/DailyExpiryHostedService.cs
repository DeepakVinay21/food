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
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(8);
            _logger.LogInformation("Daily expiry scan scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                await notificationService.RunDailyExpiryScanForAllUsersAsync(stoppingToken);
                _logger.LogInformation("Daily expiry scan completed at {Timestamp}", DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily expiry scan failed");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(int targetHourUtc)
    {
        var now = DateTimeOffset.UtcNow;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, targetHourUtc, 0, 0, TimeSpan.Zero);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
