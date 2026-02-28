using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfNotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public EfNotificationRepository(AppDbContext db) => _db = db;

    public async Task<bool> ExistsAsync(Guid batchId, string notificationType, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .AnyAsync(n => n.ProductBatchId == batchId && n.NotificationType == notificationType, cancellationToken);
    }

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        _db.Notifications.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<NotificationLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUsedBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.Product!.UserId == userId
                        && b.Status == BatchStatus.Used
                        && b.UpdatedAtUtc.Year == year
                        && b.UpdatedAtUtc.Month == month)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountExpiredBatchesInMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.Product!.UserId == userId
                        && b.Status == BatchStatus.Expired
                        && b.UpdatedAtUtc.Year == year
                        && b.UpdatedAtUtc.Month == month)
            .CountAsync(cancellationToken);
    }
}
