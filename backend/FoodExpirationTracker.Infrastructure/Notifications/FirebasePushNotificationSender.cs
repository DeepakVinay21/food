using FirebaseAdmin.Messaging;
using FoodExpirationTracker.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FoodExpirationTracker.Infrastructure.Notifications;

public class FirebasePushNotificationSender : IPushNotificationSender
{
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        IDeviceTokenRepository deviceTokenRepository,
        ILogger<FirebasePushNotificationSender> logger)
    {
        _deviceTokenRepository = deviceTokenRepository;
        _logger = logger;
    }

    public async Task<bool> SendExpiryNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default)
    {
        var tokens = await _deviceTokenRepository.GetByUserAsync(userId, cancellationToken);
        if (tokens.Count == 0)
        {
            _logger.LogWarning("No device tokens found for user {UserId}, falling back to console log", userId);
            Console.WriteLine($"Push to {userId}: {title} | {body}");
            return true;
        }

        var tokenStrings = tokens.Select(t => t.Token).ToList();

        var message = new MulticastMessage
        {
            Tokens = tokenStrings,
            Notification = new Notification
            {
                Title = title,
                Body = body,
            },
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    Sound = "default",
                    ChannelId = "expiry_alerts",
                }
            }
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, cancellationToken);

        // Clean up invalid tokens
        if (response.FailureCount > 0)
        {
            for (int i = 0; i < response.Responses.Count; i++)
            {
                if (!response.Responses[i].IsSuccess)
                {
                    var errorCode = response.Responses[i].Exception?.MessagingErrorCode;
                    if (errorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                    {
                        _logger.LogInformation("Removing invalid FCM token for user {UserId}", userId);
                        await _deviceTokenRepository.RemoveAsync(tokenStrings[i], cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("FCM send failed for user {UserId}: {Error}",
                            userId, response.Responses[i].Exception?.Message);
                    }
                }
            }
        }

        _logger.LogInformation("FCM multicast to user {UserId}: {SuccessCount}/{Total} succeeded",
            userId, response.SuccessCount, tokenStrings.Count);

        return response.SuccessCount > 0;
    }
}
