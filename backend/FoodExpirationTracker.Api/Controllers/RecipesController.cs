using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/recipes")]
public class RecipesController : ControllerBase
{
    private readonly RecipeService _recipeService;

    public RecipesController(RecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<List<RecipeSuggestionDto>>> Suggestions(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var suggestions = await _recipeService.SuggestAsync(userId, cancellationToken);
        return Ok(suggestions);
    }
}
