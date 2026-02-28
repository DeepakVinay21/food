namespace FoodExpirationTracker.Application.Abstractions;

public interface IPushNotificationSender
{
    Task<bool> SendExpiryNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);
}
