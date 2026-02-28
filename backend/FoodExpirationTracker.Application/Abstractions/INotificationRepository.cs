using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface INotificationRepository
{
    Task<bool> ExistsAsync(Guid batchId, string notificationType, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<int> CountUsedBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<int> CountExpiredBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
