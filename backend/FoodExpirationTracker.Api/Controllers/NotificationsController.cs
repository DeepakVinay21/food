using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.Services;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public NotificationsController(
        NotificationService notificationService,
        INotificationRepository notificationRepository,
        IDeviceTokenRepository deviceTokenRepository)
    {
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
        _deviceTokenRepository = deviceTokenRepository;
    }

    [HttpPost("run-daily-job")]
    public async Task<IActionResult> RunDailyJob(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _notificationService.RunDailyExpiryScanForUserAsync(userId, cancellationToken);
        return Accepted();
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var logs = await _notificationRepository.GetByUserAsync(userId, cancellationToken);
        return Ok(logs.Select(l =>
        {
            var productName = l.ProductBatch?.Product?.Name ?? "Unknown item";
            var daysLeft = l.ProductBatch is not null
                ? l.ProductBatch.ExpiryDate.DayNumber - DateOnly.FromDateTime(l.SentAtUtc).DayNumber
                : (int?)null;
            var title = l.NotificationType switch
            {
                "EXPIRY_TODAY" => "Expired!",
                "EXPIRY_1_DAY" => "Expires Tomorrow!",
                "EXPIRY_3_DAYS" => "Expiring Soon",
                "EXPIRY_7_DAYS" => "Expiry Reminder",
                "TEST" => "Test Notification",
                _ => "Expiry Alert"
            };
            var body = l.NotificationType switch
            {
                "EXPIRY_TODAY" => $"{productName} has expired! Consider discarding or using it immediately.",
                "EXPIRY_1_DAY" => $"{productName} expires tomorrow! Use it before it goes to waste.",
                "EXPIRY_3_DAYS" => $"{productName} expires in 3 days.",
                "EXPIRY_7_DAYS" => $"{productName} expires in 7 days.",
                "TEST" => $"{productName} — test notification sent.",
                _ => $"{productName} — {l.NotificationType}"
            };
            return new
            {
                l.Id,
                l.NotificationType,
                l.SentAtUtc,
                l.Success,
                l.ErrorMessage,
                Title = title,
                Body = body,
                ProductId = l.ProductBatch?.Product?.Id,
                ProductName = productName,
            };
        }));
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestNotification(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _notificationService.SendTestNotificationAsync(userId, cancellationToken);
        return Ok(new { message = "Test notification sent (push + email)." });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearNotifications(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _notificationRepository.DeleteAllByUserAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();

        var deviceToken = new DeviceToken
        {
            UserId = userId,
            Token = request.Token,
            Platform = request.Platform ?? "android",
            LastUsedAtUtc = DateTime.UtcNow,
        };

        await _deviceTokenRepository.UpsertAsync(deviceToken, cancellationToken);
        return Ok(new { message = "Device registered" });
    }
}

public record RegisterDeviceRequest(string Token, string? Platform);
