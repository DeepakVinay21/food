using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string NormalizedIngredientName { get; set; } = string.Empty;

    public Recipe? Recipe { get; set; }
}
