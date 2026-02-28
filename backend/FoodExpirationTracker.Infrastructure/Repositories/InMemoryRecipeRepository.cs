using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Infrastructure.Repositories;

public class InMemoryRecipeRepository : IRecipeRepository
{
    private static readonly List<Recipe> Recipes =
    [
        new() { Name = "Tomato Milk Toast" },
        new() { Name = "Onion Bread Soup" }
    ];

    private static readonly List<RecipeIngredient> Ingredients =
    [
        new() { RecipeId = Recipes[0].Id, IngredientName = "Tomato" },
        new() { RecipeId = Recipes[0].Id, IngredientName = "Milk" },
        new() { RecipeId = Recipes[0].Id, IngredientName = "Bread" },
        new() { RecipeId = Recipes[1].Id, IngredientName = "Onion" },
        new() { RecipeId = Recipes[1].Id, IngredientName = "Bread" },
        new() { RecipeId = Recipes[1].Id, IngredientName = "Milk" }
    ];

    public Task<List<Recipe>> GetAllRecipesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Recipes.ToList());

    public Task<List<RecipeIngredient>> GetIngredientsByRecipeIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
        => Task.FromResult(Ingredients.Where(i => i.RecipeId == recipeId).ToList());
}
