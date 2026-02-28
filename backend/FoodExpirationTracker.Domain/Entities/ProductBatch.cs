using FoodExpirationTracker.Domain.Common;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Domain.Entities;

public class ProductBatch : BaseEntity
{
    public Guid ProductId { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Active;
    public bool Notify7DaysSent { get; set; }
    public bool Notify1DaySent { get; set; }

    public Product? Product { get; set; }
}
