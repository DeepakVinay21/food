using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Application.Services;

public class DashboardService
{
    private readonly IProductRepository _productRepository;
    private readonly INotificationRepository _notificationRepository;

    public DashboardService(IProductRepository productRepository, INotificationRepository notificationRepository)
    {
        _productRepository = productRepository;
        _notificationRepository = notificationRepository;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetUserProductsAsync(userId, cancellationToken);
        var allUserBatches = await _productRepository.GetUserBatchesAsync(userId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringSoon = allUserBatches.Count(b =>
            b.Status == BatchStatus.Active &&
            !b.IsDeleted &&
            (b.ExpiryDate.DayNumber - today.DayNumber) is >= 0 and <= 7);

        var now = DateTime.UtcNow;
        var used = await _notificationRepository.CountUsedBatchesInMonthAsync(userId, now.Year, now.Month, cancellationToken);
        var waste = await _notificationRepository.CountExpiredBatchesInMonthAsync(userId, now.Year, now.Month, cancellationToken);

        return new DashboardDto(products.Count, expiringSoon, used, waste);
    }
}
