using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Infrastructure.Repositories;

public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly IProductRepository _productRepository;
    private static readonly List<NotificationLog> Logs = new();
    private static readonly Lock Sync = new();

    public InMemoryNotificationRepository(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<bool> ExistsAsync(Guid batchId, string notificationType, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Logs.Any(l => l.ProductBatchId == batchId && l.NotificationType == notificationType));
        }
    }

    public Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            Logs.Add(log);
        }

        return Task.CompletedTask;
    }

    public Task<List<NotificationLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Logs.Where(l => l.UserId == userId).OrderByDescending(l => l.SentAtUtc).ToList());
        }
    }

    public async Task<int> CountUsedBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var batches = await _productRepository.GetUserBatchesAsync(userId, cancellationToken);
        return batches.Count(b => b.Status == BatchStatus.Used && b.UpdatedAtUtc.Year == year && b.UpdatedAtUtc.Month == month);
    }

    public async Task<int> CountExpiredBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var batches = await _productRepository.GetUserBatchesAsync(userId, cancellationToken);
        return batches.Count(b => b.Status == BatchStatus.Expired && b.UpdatedAtUtc.Year == year && b.UpdatedAtUtc.Month == month);
    }
}
