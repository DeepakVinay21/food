namespace FoodExpirationTracker.Application.DTOs;

public record RecipeSuggestionDto(
    Guid RecipeId,
    string Name,
    string MealType,
    string Region,
    string ImageUrl,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Steps,
    double MatchPercent,
    double ExpiryPriorityScore,
    double FinalScore);
