using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfDeviceTokenRepository : IDeviceTokenRepository
{
    private readonly AppDbContext _db;

    public EfDeviceTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default)
    {
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == deviceToken.Token, cancellationToken);

        if (existing is not null)
        {
            existing.UserId = deviceToken.UserId;
            existing.Platform = deviceToken.Platform;
            existing.LastUsedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _db.DeviceTokens.Add(deviceToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<DeviceToken>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.DeviceTokens
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveAsync(string token, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == token, cancellationToken);

        if (entity is not null)
        {
            _db.DeviceTokens.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
