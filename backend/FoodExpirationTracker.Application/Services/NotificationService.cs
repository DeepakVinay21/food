using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Services;

public class NotificationService
{
    private readonly IProductRepository _productRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IPushNotificationSender _pushNotificationSender;
    private readonly IUserRepository _userRepository;

    public NotificationService(
        IProductRepository productRepository,
        INotificationRepository notificationRepository,
        IPushNotificationSender pushNotificationSender,
        IUserRepository userRepository)
    {
        _productRepository = productRepository;
        _notificationRepository = notificationRepository;
        _pushNotificationSender = pushNotificationSender;
        _userRepository = userRepository;
    }

    public async Task RunDailyExpiryScanForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var batches = await _productRepository.GetUserActiveBatchesAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var batch in batches)
        {
            var daysUntilExpiry = batch.ExpiryDate.DayNumber - today.DayNumber;
            var notificationType = daysUntilExpiry switch
            {
                7 => "EXPIRY_7_DAYS",
                1 => "EXPIRY_1_DAY",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(notificationType))
            {
                continue;
            }

            var exists = await _notificationRepository.ExistsAsync(batch.Id, notificationType, cancellationToken);
            if (exists)
            {
                continue;
            }

            var success = await _pushNotificationSender.SendExpiryNotificationAsync(
                userId,
                "Food Expiry Reminder",
                $"One of your items expires in {daysUntilExpiry} day(s).",
                cancellationToken);

            await _notificationRepository.AddAsync(new NotificationLog
            {
                UserId = userId,
                ProductBatchId = batch.Id,
                NotificationType = notificationType,
                Success = success,
                ErrorMessage = success ? null : "Failed to send push notification."
            }, cancellationToken);
        }
    }

    public async Task RunDailyExpiryScanForAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllActiveAsync(cancellationToken);

        foreach (var user in users)
        {
            await RunDailyExpiryScanForUserAsync(user.Id, cancellationToken);
        }
    }
}
