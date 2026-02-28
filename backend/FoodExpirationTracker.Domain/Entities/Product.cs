using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class Product : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public User? User { get; set; }
    public Category? Category { get; set; }
    public List<ProductBatch> Batches { get; set; } = [];
}
