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
        return Ok(logs.Select(l => new
        {
            l.Id,
            l.NotificationType,
            l.SentAtUtc,
            l.Success,
            l.ErrorMessage
        }));
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
