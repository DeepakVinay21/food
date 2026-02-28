using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class NotificationLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductBatchId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public User? User { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
