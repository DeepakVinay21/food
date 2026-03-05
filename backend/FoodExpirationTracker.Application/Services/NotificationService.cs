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
        var products = await _productRepository.GetUserProductsAsync(userId, cancellationToken);
        var productNameById = products.ToDictionary(p => p.Id, p => p.Name);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var batch in batches)
        {
            var daysUntilExpiry = batch.ExpiryDate.DayNumber - today.DayNumber;

            // Determine which notification types apply for this batch
            var typesToSend = new List<(string type, string title, string body)>();

            if (daysUntilExpiry <= 0)
            {
                var productName = productNameById.GetValueOrDefault(batch.ProductId, "Unknown item");
                typesToSend.Add(("EXPIRY_TODAY", "Expired!", $"{productName} has expired! Consider discarding or using it immediately."));
            }
            else if (daysUntilExpiry == 1)
            {
                var productName = productNameById.GetValueOrDefault(batch.ProductId, "Unknown item");
                typesToSend.Add(("EXPIRY_1_DAY", "Expires Tomorrow", $"{productName} expires tomorrow! Use it before it goes to waste."));
            }
            else if (daysUntilExpiry <= 3)
            {
                var productName = productNameById.GetValueOrDefault(batch.ProductId, "Unknown item");
                typesToSend.Add(("EXPIRY_3_DAYS", "Expiring Soon", $"{productName} expires in {daysUntilExpiry} days."));
            }
            else if (daysUntilExpiry <= 7)
            {
                var productName = productNameById.GetValueOrDefault(batch.ProductId, "Unknown item");
                typesToSend.Add(("EXPIRY_7_DAYS", "Expiry Reminder", $"{productName} expires in {daysUntilExpiry} days."));
            }

            foreach (var (notificationType, title, body) in typesToSend)
            {
                var exists = await _notificationRepository.ExistsAsync(batch.Id, notificationType, cancellationToken);
                if (exists) continue;

                // Send push notification
                var pushSuccess = false;
                try
                {
                    pushSuccess = await _pushNotificationSender.SendExpiryNotificationAsync(
                        userId, title, body, cancellationToken);
                }
                catch { /* Push failure should not block */ }

                try
                {
                    await _notificationRepository.AddAsync(new NotificationLog
                    {
                        UserId = userId,
                        ProductBatchId = batch.Id,
                        NotificationType = notificationType,
                        Success = pushSuccess,
                        ErrorMessage = pushSuccess ? null : "Push notification failed."
                    }, cancellationToken);
                }
                catch { /* Duplicate constraint — skip */ }
            }
        }
    }

    /// <summary>
    /// Sends immediate notifications for ALL items expiring within 7 days (today, tomorrow, soon).
    /// Used by the "Test Notification" button — skips duplicate checks so it always sends.
    /// </summary>
    public async Task SendTestNotificationAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var batches = await _productRepository.GetUserActiveBatchesAsync(userId, cancellationToken);
        var products = await _productRepository.GetUserProductsAsync(userId, cancellationToken);
        var productNameById = products.ToDictionary(p => p.Id, p => p.Name);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Collect all items expiring within 7 days (or already expired)
        var alertItems = new List<(ProductBatch batch, string productName, int daysLeft)>();
        foreach (var batch in batches)
        {
            var daysLeft = batch.ExpiryDate.DayNumber - today.DayNumber;
            if (daysLeft <= 7)
            {
                var name = productNameById.GetValueOrDefault(batch.ProductId, "Unknown item");
                alertItems.Add((batch, name, daysLeft));
            }
        }

        if (alertItems.Count == 0)
        {
            // Nothing expiring soon — send a generic test notification
            var title = "Test Notification";
            var body = "No items expiring soon. Your pantry looks great!";
            try
            {
                await _pushNotificationSender.SendExpiryNotificationAsync(userId, title, body, cancellationToken);
            }
            catch { }
            return;
        }

        // Send a notification for each expiring item
        foreach (var (batch, productName, daysLeft) in alertItems)
        {
            var title = daysLeft <= 0 ? "Expired!" : daysLeft == 1 ? "Expires Tomorrow!" : "Expiring Soon!";
            var body = daysLeft <= 0
                ? $"{productName} has expired! Consider discarding or using it immediately."
                : daysLeft == 1
                    ? $"{productName} expires tomorrow! Use it before it goes to waste."
                    : $"{productName} expires in {daysLeft} days.";

            try
            {
                await _pushNotificationSender.SendExpiryNotificationAsync(userId, title, body, cancellationToken);
            }
            catch { }

            // Log the notification (clean up old TEST entries first)
            try
            {
                var existing = await _notificationRepository.ExistsAsync(batch.Id, "TEST", cancellationToken);
                if (existing)
                    await _notificationRepository.DeleteByBatchAndTypeAsync(batch.Id, "TEST", cancellationToken);

                await _notificationRepository.AddAsync(new NotificationLog
                {
                    UserId = userId,
                    ProductBatchId = batch.Id,
                    NotificationType = "TEST",
                    Success = true,
                }, cancellationToken);
            }
            catch { }
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
