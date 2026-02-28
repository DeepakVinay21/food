using FoodExpirationTracker.Application.Abstractions;

namespace FoodExpirationTracker.Infrastructure.Notifications;

public class ConsolePushNotificationSender : IPushNotificationSender
{
    public Task<bool> SendExpiryNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Push to {userId}: {title} | {body}");
        return Task.FromResult(true);
    }
}
