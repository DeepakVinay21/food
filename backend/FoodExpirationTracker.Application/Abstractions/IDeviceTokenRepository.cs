using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IDeviceTokenRepository
{
    Task UpsertAsync(DeviceToken deviceToken, CancellationToken cancellationToken = default);
    Task<List<DeviceToken>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string token, CancellationToken cancellationToken = default);
}
