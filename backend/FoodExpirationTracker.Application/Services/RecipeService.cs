using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Services;

public class RecipeService
{
    private readonly IProductRepository _productRepository;

    private static readonly List<RecipeTemplate> Catalog =
    [
        new("Masala Toast", "Breakfast", "Indian", "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=900&q=80", ["bread", "onion", "tomato"],
            ["Bread - 4 slices", "Onion - 1", "Tomato - 1", "Salt - 1 tsp", "Butter - 1 tbsp"],
            ["Chop onion and tomato.", "Heat butter on pan.", "Add onion and tomato with salt.", "Place mix on bread and toast both sides."]
        ),
        new("Poha", "Breakfast", "Indian", "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=900&q=80", ["onion", "potato", "lemon"],
            ["Flattened rice - 2 cups", "Onion - 1", "Potato - 1", "Salt - 1 tsp", "Lemon - 1"],
            ["Wash poha and keep aside.", "Saute onion and potato.", "Mix poha and salt.", "Finish with lemon juice."]
        ),
        new("Banana Milk Smoothie", "Breakfast", "Global", "https://images.unsplash.com/photo-1464306076886-da185f6a9d05?auto=format&fit=crop&w=900&q=80", ["banana", "milk"],
            ["Banana - 2", "Milk - 300 ml", "Honey - 1 tbsp"],
            ["Add banana, milk and honey to blender.", "Blend until smooth.", "Serve chilled."]
        ),
        new("Tomato Rice", "Lunch", "South Indian", "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=900&q=80", ["tomato", "onion"],
            ["Rice - 500g", "Tomato - 3", "Onion - 1", "Salt - 2 tsp", "Oil - 2 tbsp"],
            ["Cook rice and keep aside.", "Saute onion then tomato in oil.", "Add salt and cook gravy.", "Mix cooked rice and simmer 2 minutes."]
        ),
        new("Vegetable Sandwich", "Lunch", "Western", "https://images.unsplash.com/photo-1481070414801-51fd732d7184?auto=format&fit=crop&w=900&q=80", ["bread", "tomato", "onion"],
            ["Bread - 4 slices", "Tomato - 1", "Onion - 1", "Butter - 1 tbsp", "Salt - 1 tsp"],
            ["Slice vegetables thinly.", "Spread butter on bread.", "Layer vegetables and season.", "Toast and serve."]
        ),
        new("Creamy Tomato Soup", "Dinner", "Continental", "https://images.unsplash.com/photo-1547592166-23ac45744acd?auto=format&fit=crop&w=900&q=80", ["tomato", "milk", "onion"],
            ["Tomato - 500g", "Milk - 250 ml", "Onion - 1", "Salt - 2 tsp", "Pepper - 1 tsp"],
            ["Cook tomato and onion until soft.", "Blend to smooth puree.", "Return to pan and add milk.", "Season and simmer for 5 minutes."]
        ),
        new("Onion Egg Omelette", "Dinner", "Indian", "https://images.unsplash.com/photo-1525351484163-7529414344d8?auto=format&fit=crop&w=900&q=80", ["onion", "egg"],
            ["Eggs - 3", "Onion - 1", "Milk - 2 tbsp", "Salt - 1 tsp"],
            ["Beat eggs with milk and salt.", "Saute onion lightly.", "Pour eggs and cook till set.", "Fold and serve."]
        ),
        new("Vegetable Stir Fry", "Dinner", "Asian", "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=80", ["carrot", "onion", "cabbage"],
            ["Mixed vegetables - 400g", "Onion - 1", "Salt - 1 tsp", "Oil - 1 tbsp"],
            ["Heat oil in wok.", "Add onion and vegetables.", "Stir fry on high flame.", "Season and serve."]
        ),
        new("Milk Biscuits Pudding", "Snacks", "Fusion", "https://images.unsplash.com/photo-1551024601-bec78aea704b?auto=format&fit=crop&w=900&q=80", ["milk", "biscuit"],
            ["Milk - 300 ml", "Biscuits - 8", "Sugar - 2 tbsp"],
            ["Warm milk with sugar.", "Layer crushed biscuits.", "Pour warm milk over biscuits.", "Chill and serve."]
        ),
        new("Chocolate Bread Snack", "Snacks", "Global", "https://images.unsplash.com/photo-1481391243133-f96216dcb5d2?auto=format&fit=crop&w=900&q=80", ["bread", "chocolate"],
            ["Bread - 2 slices", "Chocolate - 50g", "Butter - 1 tsp"],
            ["Spread butter on bread.", "Add grated chocolate.", "Toast until chocolate melts.", "Cut and serve warm."]
        ),
        new("Fruit Chaat", "Snacks", "Indian", "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?auto=format&fit=crop&w=900&q=80", ["apple", "banana", "orange"],
            ["Apple - 1", "Banana - 1", "Orange - 1", "Salt - 1 pinch", "Lemon - 1 tsp"],
            ["Dice all fruits.", "Add lemon and salt.", "Toss gently and serve chilled."]
        )
    ];

    public RecipeService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<RecipeSuggestionDto>> SuggestAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activeBatches = await _productRepository.GetUserActiveBatchesAsync(userId, cancellationToken);
        var products = await _productRepository.GetUserProductsAsync(userId, cancellationToken);
        var available = products
            .Select(p => p.Name.Trim().ToLowerInvariant())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet();

        if (available.Count == 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var productNameById = products.ToDictionary(p => p.Id, p => p.Name.ToLowerInvariant());

        var suggestions = new List<RecipeSuggestionDto>();
        var pantryTokens = BuildPantryTokens(available);

        foreach (var recipe in Catalog)
        {
            var matchedKeywords = recipe.Keywords
                .Where(k => pantryTokens.Any(t => t.Contains(k) || k.Contains(t)))
                .Distinct()
                .ToList();

            var matched = matchedKeywords.Count;
            if (matched == 0)
            {
                continue;
            }

            var matchPercent = (double)matched / recipe.Keywords.Count * 100;

            var expiryPriority = 0.0;
            foreach (var batch in activeBatches)
            {
                if (!productNameById.TryGetValue(batch.ProductId, out var productName))
                {
                    continue;
                }

                if (!recipe.Keywords.Any(k => productName.Contains(k)))
                {
                    continue;
                }

                var days = batch.ExpiryDate.DayNumber - today.DayNumber;
                if (days is >= 0 and <= 3)
                {
                    expiryPriority += days <= 1 ? 8 : 5;
                }
            }

            var finalScore = Math.Round(matchPercent + Math.Min(20, expiryPriority), 2);

            suggestions.Add(new RecipeSuggestionDto(
                recipe.Id,
                recipe.Name,
                recipe.MealType,
                recipe.Region,
                recipe.ImageUrl,
                recipe.Ingredients,
                recipe.Steps,
                Math.Round(matchPercent, 2),
                Math.Round(Math.Min(20, expiryPriority), 2),
                finalScore));
        }

        var ordered = suggestions
            .OrderByDescending(s => s.FinalScore)
            .ThenBy(s => s.MealType)
            .ThenBy(s => s.Name)
            .ToList();

        var diversified = new List<RecipeSuggestionDto>();
        foreach (var meal in new[] { "Breakfast", "Lunch", "Dinner", "Snacks" })
        {
            var topForMeal = ordered.FirstOrDefault(s => string.Equals(s.MealType, meal, StringComparison.OrdinalIgnoreCase));
            if (topForMeal is not null)
            {
                diversified.Add(topForMeal);
            }
        }

        foreach (var item in ordered)
        {
            if (diversified.All(d => d.RecipeId != item.RecipeId))
            {
                diversified.Add(item);
            }
        }

        return diversified;
    }

    private static HashSet<string> BuildPantryTokens(HashSet<string> names)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names)
        {
            tokens.Add(n);
            foreach (var token in n.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                tokens.Add(token);
            }
            if (n.Contains("vegetable")) tokens.Add("tomato");
            if (n.Contains("vegetable")) tokens.Add("onion");
            if (n.Contains("fruit")) tokens.Add("banana");
            if (n.Contains("fruit")) tokens.Add("apple");
            if (n.Contains("bakery")) tokens.Add("bread");
            if (n.Contains("snack")) tokens.Add("biscuit");
        }

        return tokens;
    }

    private sealed record RecipeTemplate(
        string Name,
        string MealType,
        string Region,
        string ImageUrl,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> Ingredients,
        IReadOnlyList<string> Steps)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
