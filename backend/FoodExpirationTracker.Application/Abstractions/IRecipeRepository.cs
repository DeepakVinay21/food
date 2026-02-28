using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IRecipeRepository
{
    Task<List<Recipe>> GetAllRecipesAsync(CancellationToken cancellationToken = default);
    Task<List<RecipeIngredient>> GetIngredientsByRecipeIdAsync(Guid recipeId, CancellationToken cancellationToken = default);
}
