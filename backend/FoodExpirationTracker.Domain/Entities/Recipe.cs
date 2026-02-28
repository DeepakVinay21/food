using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class Recipe : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Instructions { get; set; }

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
