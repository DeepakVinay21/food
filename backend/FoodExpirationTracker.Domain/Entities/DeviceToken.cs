using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
