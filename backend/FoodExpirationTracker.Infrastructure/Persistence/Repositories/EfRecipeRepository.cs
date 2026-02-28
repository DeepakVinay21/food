using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfRecipeRepository : IRecipeRepository
{
    private readonly AppDbContext _db;

    public EfRecipeRepository(AppDbContext db) => _db = db;

    public async Task<List<Recipe>> GetAllRecipesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Recipes.ToListAsync(cancellationToken);
    }

    public async Task<List<RecipeIngredient>> GetIngredientsByRecipeIdAsync(Guid recipeId, CancellationToken cancellationToken = default)
    {
        return await _db.RecipeIngredients
            .Where(i => i.RecipeId == recipeId)
            .ToListAsync(cancellationToken);
    }
}
