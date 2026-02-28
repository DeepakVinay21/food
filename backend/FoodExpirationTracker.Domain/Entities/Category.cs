using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public List<Product> Products { get; set; } = [];
}
