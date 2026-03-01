using System.Text.Json;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Services;

public class RecipeService
{
    private readonly IProductRepository _productRepository;
    private readonly IGeminiVisionService _geminiService;

    public RecipeService(IProductRepository productRepository, IGeminiVisionService geminiService)
    {
        _productRepository = productRepository;
        _geminiService = geminiService;
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
            return [];

        // Build expiry info for the prompt
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var productNameById = products.ToDictionary(p => p.Id, p => p.Name.ToLowerInvariant());
        var expiryInfo = new List<string>();
        foreach (var batch in activeBatches)
        {
            if (!productNameById.TryGetValue(batch.ProductId, out var name)) continue;
            var days = batch.ExpiryDate.DayNumber - today.DayNumber;
            if (days <= 3)
                expiryInfo.Add($"{name} (expires in {days} day{(days == 1 ? "" : "s")})");
        }

        // Try AI-generated recipes first
        var aiRecipes = await TryGenerateAiRecipesAsync(available, expiryInfo, cancellationToken);
        if (aiRecipes is { Count: > 0 })
            return aiRecipes;

        // Fallback to hardcoded catalog matching
        return MatchFromCatalog(available, activeBatches, productNameById, today);
    }

    private async Task<List<RecipeSuggestionDto>?> TryGenerateAiRecipesAsync(
        HashSet<string> pantryItems,
        List<string> expiryInfo,
        CancellationToken cancellationToken)
    {
        var itemsList = string.Join(", ", pantryItems);
        var expirySection = expiryInfo.Count > 0
            ? $"\n\nItems expiring soon (prioritize these): {string.Join(", ", expiryInfo)}"
            : "";

        var prompt = $$"""
            You are a recipe suggestion engine. The user has these items in their pantry: {{itemsList}}{{expirySection}}

            Generate 5-6 diverse recipes that can be made using these available ingredients.
            Assume the user has common staples like salt, pepper, oil, water, and basic spices.
            Include a good mix of meal types: 1-2 Breakfast, 1-2 Lunch, 1-2 Dinner, 1 Snacks.
            Prioritize recipes that use items expiring soon.
            Include a variety of cuisines and difficulty levels.

            Return ONLY a JSON array (no markdown, no backticks) with this exact structure:
            [
              {
                "name": "Recipe Name",
                "mealType": "Breakfast|Lunch|Dinner|Snacks",
                "region": "Indian|South Indian|North Indian|Western|Asian|Italian|Global|Continental|Fusion",
                "ingredients": ["ingredient 1 - quantity", "ingredient 2 - quantity"],
                "steps": ["Step 1 description.", "Step 2 description.", "Step 3 description."],
                "matchPercent": 85,
                "expiryPriorityScore": 10
              }
            ]

            Rules:
            - matchPercent should reflect what percentage of recipe ingredients the user already has (60-100)
            - expiryPriorityScore: 0-20, higher if the recipe uses soon-to-expire ingredients
            - Keep recipes practical with 3-6 steps each
            - Include both simple quick recipes (under 15 min) and more elaborate ones
            - Be creative - combine ingredients in interesting ways
            - Every recipe MUST use at least one pantry item
            """;

        try
        {
            var response = await _geminiService.GenerateTextAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(response)) return null;

            // Clean response - strip markdown code fences if present
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                if (firstNewline >= 0) json = json[(firstNewline + 1)..];
                if (json.EndsWith("```")) json = json[..^3];
                json = json.Trim();
            }

            var recipes = JsonSerializer.Deserialize<List<AiRecipeResponse>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (recipes is null or { Count: 0 }) return null;

            return recipes.Select(r => new RecipeSuggestionDto(
                Guid.NewGuid(),
                r.Name ?? "Unknown Recipe",
                r.MealType ?? "Lunch",
                r.Region ?? "Global",
                "",
                r.Ingredients ?? [],
                r.Steps ?? [],
                Math.Round(Math.Clamp(r.MatchPercent, 0, 100), 2),
                Math.Round(Math.Clamp(r.ExpiryPriorityScore, 0, 20), 2),
                Math.Round(Math.Clamp(r.MatchPercent, 0, 100) + Math.Min(20, Math.Clamp(r.ExpiryPriorityScore, 0, 20)), 2)
            )).OrderByDescending(r => r.FinalScore).ToList();
        }
        catch
        {
            return null;
        }
    }

    private sealed class AiRecipeResponse
    {
        public string? Name { get; set; }
        public string? MealType { get; set; }
        public string? Region { get; set; }
        public List<string>? Ingredients { get; set; }
        public List<string>? Steps { get; set; }
        public double MatchPercent { get; set; }
        public double ExpiryPriorityScore { get; set; }
    }

    // ── Hardcoded catalog fallback ──

    private List<RecipeSuggestionDto> MatchFromCatalog(
        HashSet<string> available,
        IReadOnlyList<Domain.Entities.ProductBatch> activeBatches,
        Dictionary<Guid, string> productNameById,
        DateOnly today)
    {
        var pantryTokens = BuildPantryTokens(available);
        var suggestions = new List<RecipeSuggestionDto>();

        foreach (var recipe in Catalog)
        {
            var matchedKeywords = recipe.Keywords
                .Where(k => pantryTokens.Any(t => t.Contains(k) || k.Contains(t)))
                .Distinct()
                .ToList();

            if (matchedKeywords.Count == 0) continue;

            var matchPercent = (double)matchedKeywords.Count / recipe.Keywords.Count * 100;

            var expiryPriority = 0.0;
            foreach (var batch in activeBatches)
            {
                if (!productNameById.TryGetValue(batch.ProductId, out var productName)) continue;
                if (!recipe.Keywords.Any(k => productName.Contains(k))) continue;
                var days = batch.ExpiryDate.DayNumber - today.DayNumber;
                if (days is >= 0 and <= 3)
                    expiryPriority += days <= 1 ? 8 : 5;
            }

            suggestions.Add(new RecipeSuggestionDto(
                recipe.Id, recipe.Name, recipe.MealType, recipe.Region, recipe.ImageUrl,
                recipe.Ingredients, recipe.Steps,
                Math.Round(matchPercent, 2),
                Math.Round(Math.Min(20, expiryPriority), 2),
                Math.Round(matchPercent + Math.Min(20, expiryPriority), 2)));
        }

        var ordered = suggestions.OrderByDescending(s => s.FinalScore).ThenBy(s => s.MealType).ThenBy(s => s.Name).ToList();
        var diversified = new List<RecipeSuggestionDto>();
        foreach (var meal in new[] { "Breakfast", "Lunch", "Dinner", "Snacks" })
        {
            var top = ordered.FirstOrDefault(s => string.Equals(s.MealType, meal, StringComparison.OrdinalIgnoreCase));
            if (top is not null) diversified.Add(top);
        }
        foreach (var item in ordered)
        {
            if (diversified.All(d => d.RecipeId != item.RecipeId))
                diversified.Add(item);
        }
        return diversified.Take(6).ToList();
    }

    // ── Synonyms & token expansion ──

    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["curd"] = ["yogurt"], ["yoghurt"] = ["yogurt"], ["dahi"] = ["yogurt"],
        ["atta"] = ["flour"], ["wheat"] = ["flour"], ["maida"] = ["flour"],
        ["palak"] = ["spinach"], ["aloo"] = ["potato"], ["tamatar"] = ["tomato"],
        ["pyaz"] = ["onion"], ["shimla mirch"] = ["capsicum"], ["bell pepper"] = ["capsicum"],
        ["chana"] = ["chickpea"], ["rajma"] = ["rajma"], ["kidney bean"] = ["rajma"],
        ["maggi"] = ["noodle"], ["instant noodle"] = ["noodle"],
        ["spaghetti"] = ["pasta"], ["macaroni"] = ["pasta"], ["penne"] = ["pasta"],
        ["toor dal"] = ["dal"], ["moong dal"] = ["dal"], ["masoor dal"] = ["dal"], ["lentil"] = ["dal"],
        ["ghee"] = ["butter"], ["cottage cheese"] = ["paneer"],
    };

    private static HashSet<string> BuildPantryTokens(HashSet<string> names)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names)
        {
            tokens.Add(n);
            foreach (var token in n.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                tokens.Add(token);
                if (token.EndsWith("s") && token.Length > 3) tokens.Add(token[..^1]);
                if (token.EndsWith("es") && token.Length > 4) tokens.Add(token[..^2]);
            }
            foreach (var (key, values) in Synonyms)
            {
                if (n.Contains(key, StringComparison.OrdinalIgnoreCase))
                    foreach (var v in values) tokens.Add(v);
            }
            if (n.Contains("vegetable")) { tokens.Add("tomato"); tokens.Add("onion"); tokens.Add("carrot"); tokens.Add("cabbage"); tokens.Add("potato"); tokens.Add("peas"); }
            if (n.Contains("fruit")) { tokens.Add("banana"); tokens.Add("apple"); tokens.Add("orange"); tokens.Add("mango"); }
            if (n.Contains("bakery")) { tokens.Add("bread"); tokens.Add("biscuit"); }
            if (n.Contains("snack")) tokens.Add("biscuit");
            if (n.Contains("dairy")) { tokens.Add("milk"); tokens.Add("yogurt"); tokens.Add("cheese"); tokens.Add("butter"); }
        }
        return tokens;
    }

    // ── Hardcoded recipe catalog ──

    private static readonly List<RecipeTemplate> Catalog =
    [
        // ── Breakfast ──
        new("Masala Toast", "Breakfast", "Indian", "", ["bread", "onion", "tomato"], ["Bread - 4 slices", "Onion - 1", "Tomato - 1", "Salt - 1 tsp", "Butter - 1 tbsp"], ["Chop onion and tomato.", "Heat butter on pan.", "Add onion and tomato with salt.", "Place mix on bread and toast both sides."]),
        new("Poha", "Breakfast", "Indian", "", ["onion", "potato", "lemon"], ["Flattened rice - 2 cups", "Onion - 1", "Potato - 1", "Salt - 1 tsp", "Lemon - 1"], ["Wash poha and keep aside.", "Saute onion and potato.", "Mix poha and salt.", "Finish with lemon juice."]),
        new("Banana Milk Smoothie", "Breakfast", "Global", "", ["banana", "milk"], ["Banana - 2", "Milk - 300 ml", "Honey - 1 tbsp"], ["Add banana, milk and honey to blender.", "Blend until smooth.", "Serve chilled."]),
        new("Cheese Omelette", "Breakfast", "Western", "", ["egg", "cheese"], ["Eggs - 3", "Cheese - 50g", "Butter - 1 tsp", "Salt - 1 tsp", "Pepper - pinch"], ["Beat eggs with salt and pepper.", "Melt butter in pan on medium heat.", "Pour eggs and cook until edges set.", "Add grated cheese, fold and serve."]),
        new("Paratha with Yogurt", "Breakfast", "North Indian", "", ["flour", "yogurt", "butter"], ["Wheat flour - 2 cups", "Yogurt - 1 cup", "Butter/Ghee - 2 tbsp", "Salt - 1 tsp"], ["Knead dough with flour, salt and water.", "Roll into circles and cook on hot tawa.", "Apply butter on both sides.", "Serve hot with yogurt."]),
        new("Upma", "Breakfast", "South Indian", "", ["onion", "carrot", "peas"], ["Rava/Semolina - 1 cup", "Onion - 1", "Carrot - 1", "Green peas - 1/4 cup", "Salt - 1 tsp", "Oil - 2 tbsp"], ["Dry roast rava until golden.", "Saute onion, carrot and peas in oil.", "Add 2 cups water and salt, bring to boil.", "Add rava slowly, stir until thick."]),
        new("Egg Fried Rice", "Breakfast", "Asian", "", ["egg", "rice", "onion"], ["Eggs - 2", "Cooked rice - 2 cups", "Onion - 1", "Soy sauce - 1 tbsp", "Oil - 1 tbsp"], ["Scramble eggs in oil and set aside.", "Saute chopped onion.", "Add rice and soy sauce, toss well.", "Mix in scrambled eggs and serve."]),
        new("Milk Oats Porridge", "Breakfast", "Global", "", ["milk", "oats", "banana"], ["Milk - 1 cup", "Oats - 1/2 cup", "Banana - 1", "Honey - 1 tbsp"], ["Heat milk in a pan.", "Add oats and cook for 3-4 minutes.", "Top with sliced banana and honey.", "Serve warm."]),

        // ── Lunch ──
        new("Tomato Rice", "Lunch", "South Indian", "", ["tomato", "rice", "onion"], ["Rice - 500g", "Tomato - 3", "Onion - 1", "Salt - 2 tsp", "Oil - 2 tbsp"], ["Cook rice and keep aside.", "Saute onion then tomato in oil.", "Add salt and cook gravy.", "Mix cooked rice and simmer 2 minutes."]),
        new("Dal Rice", "Lunch", "Indian", "", ["dal", "rice", "onion"], ["Toor dal - 1 cup", "Rice - 2 cups", "Onion - 1", "Tomato - 1", "Turmeric - 1/2 tsp", "Salt - 2 tsp", "Ghee - 1 tbsp"], ["Cook dal with turmeric until soft.", "Cook rice separately.", "Prepare tadka with onion and tomato.", "Pour tadka over dal and serve with rice."]),
        new("Paneer Butter Masala", "Lunch", "North Indian", "", ["paneer", "tomato", "butter"], ["Paneer - 250g", "Tomato - 4", "Butter - 2 tbsp", "Cream - 2 tbsp", "Onion - 1", "Salt - 1 tsp", "Garam masala - 1 tsp"], ["Blend tomato and onion into smooth paste.", "Cook paste in butter until oil separates.", "Add paneer cubes and cream.", "Simmer 5 min and serve with rice or naan."]),
        new("Vegetable Pulao", "Lunch", "Indian", "", ["rice", "carrot", "peas", "potato"], ["Rice - 2 cups", "Carrot - 1", "Peas - 1/2 cup", "Potato - 1", "Onion - 1", "Whole spices - 1 tsp", "Salt - 2 tsp", "Oil - 2 tbsp"], ["Saute whole spices and onion in oil.", "Add diced vegetables and cook 3 min.", "Add rice and 4 cups water.", "Cook covered until rice is done."]),
        new("Curd Rice", "Lunch", "South Indian", "", ["rice", "yogurt", "milk"], ["Rice - 1 cup", "Yogurt - 1 cup", "Milk - 1/4 cup", "Salt - 1 tsp", "Mustard seeds - 1/2 tsp"], ["Cook rice and let it cool.", "Mix yogurt and milk into rice.", "Prepare tempering with mustard seeds.", "Mix everything and serve chilled."]),
        new("Aloo Gobi", "Lunch", "North Indian", "", ["potato", "cauliflower", "onion", "tomato"], ["Potato - 2", "Cauliflower - 1 small", "Onion - 1", "Tomato - 1", "Turmeric - 1/2 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"], ["Cut potato and cauliflower into pieces.", "Saute onion, add tomato and spices.", "Add vegetables with little water.", "Cook covered until tender."]),
        new("Jeera Rice with Raita", "Lunch", "North Indian", "", ["rice", "yogurt", "cucumber"], ["Rice - 2 cups", "Cumin seeds - 1 tsp", "Yogurt - 1 cup", "Cucumber - 1", "Salt - 1 tsp", "Ghee - 1 tbsp"], ["Cook rice with cumin seeds and ghee.", "Grate cucumber into yogurt.", "Add salt and mix for raita.", "Serve jeera rice with raita."]),
        new("Pasta Arrabiata", "Lunch", "Italian", "", ["pasta", "tomato", "onion"], ["Pasta - 200g", "Tomato - 3", "Onion - 1", "Garlic - 3 cloves", "Olive oil - 2 tbsp", "Chili flakes - 1 tsp", "Salt - 1 tsp"], ["Boil pasta until al dente.", "Saute garlic and onion in olive oil.", "Add chopped tomato, chili flakes and simmer.", "Toss pasta in sauce and serve."]),

        // ── Dinner ──
        new("Creamy Tomato Soup", "Dinner", "Continental", "", ["tomato", "milk", "onion"], ["Tomato - 500g", "Milk - 250 ml", "Onion - 1", "Salt - 2 tsp", "Pepper - 1 tsp"], ["Cook tomato and onion until soft.", "Blend to smooth puree.", "Return to pan and add milk.", "Season and simmer for 5 minutes."]),
        new("Palak Paneer", "Dinner", "North Indian", "", ["spinach", "paneer", "onion"], ["Spinach - 500g", "Paneer - 200g", "Onion - 1", "Garlic - 3 cloves", "Cream - 2 tbsp", "Salt - 1 tsp"], ["Blanch spinach and blend to paste.", "Saute onion and garlic.", "Add spinach paste and paneer cubes.", "Finish with cream and serve."]),
        new("Chicken Curry", "Dinner", "Indian", "", ["chicken", "onion", "tomato"], ["Chicken - 500g", "Onion - 2", "Tomato - 2", "Ginger-garlic paste - 1 tbsp", "Salt - 1 tsp", "Oil - 2 tbsp", "Garam masala - 1 tsp"], ["Heat oil and saute onion until golden.", "Add ginger-garlic paste and tomato.", "Add chicken and spices, cook covered 20 min.", "Garnish and serve with rice or roti."]),
        new("Egg Curry", "Dinner", "Indian", "", ["egg", "onion", "tomato"], ["Eggs - 4", "Onion - 2", "Tomato - 2", "Ginger-garlic paste - 1 tbsp", "Turmeric - 1/2 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"], ["Boil eggs and halve them.", "Saute onion, add ginger-garlic paste.", "Add tomato and spices, cook gravy.", "Add eggs and simmer 5 minutes."]),
        new("Mushroom Masala", "Dinner", "Indian", "", ["mushroom", "onion", "tomato"], ["Mushroom - 250g", "Onion - 2", "Tomato - 2", "Garam masala - 1 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"], ["Clean and slice mushrooms.", "Saute onion until golden.", "Add tomato and spices, cook until soft.", "Add mushrooms and cook 8-10 minutes."]),
        new("Chapati with Sabzi", "Dinner", "Indian", "", ["flour", "potato", "onion"], ["Wheat flour - 2 cups", "Potato - 2", "Onion - 1", "Tomato - 1", "Salt - 1 tsp", "Oil - 1 tbsp"], ["Knead soft dough and roll chapatis.", "Cook chapatis on hot tawa.", "Saute onion, potato, tomato with spices.", "Serve chapatis with sabzi."]),
        new("Lemon Rice", "Dinner", "South Indian", "", ["rice", "lemon", "peanut"], ["Rice - 2 cups", "Lemon - 2", "Peanuts - 2 tbsp", "Turmeric - 1/2 tsp", "Mustard seeds - 1 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"], ["Cook rice and spread on plate to cool.", "Heat oil, add mustard seeds and peanuts.", "Add turmeric, mix into rice.", "Squeeze lemon juice, toss and serve."]),

        // ── Snacks ──
        new("Milk Biscuits Pudding", "Snacks", "Fusion", "", ["milk", "biscuit"], ["Milk - 300 ml", "Biscuits - 8", "Sugar - 2 tbsp"], ["Warm milk with sugar.", "Layer crushed biscuits.", "Pour warm milk over biscuits.", "Chill and serve."]),
        new("Fruit Chaat", "Snacks", "Indian", "", ["apple", "banana", "orange"], ["Apple - 1", "Banana - 1", "Orange - 1", "Salt - 1 pinch", "Lemon - 1 tsp"], ["Dice all fruits.", "Add lemon and salt.", "Toss gently and serve chilled."]),
        new("Paneer Tikka", "Snacks", "North Indian", "", ["paneer", "yogurt", "capsicum"], ["Paneer - 250g", "Yogurt - 3 tbsp", "Capsicum/Bell pepper - 1", "Onion - 1", "Tikka masala - 1 tbsp", "Salt - 1 tsp"], ["Cut paneer and vegetables into cubes.", "Marinate in yogurt and spices for 30 min.", "Thread onto skewers.", "Grill or pan-fry until charred and serve."]),
        new("Bread Pakora", "Snacks", "Indian", "", ["bread", "potato", "flour"], ["Bread - 4 slices", "Potato - 2 (boiled, mashed)", "Besan/Gram flour - 1 cup", "Salt - 1 tsp", "Oil - for frying"], ["Make filling with mashed potato and spices.", "Spread filling on bread slices.", "Dip in besan batter.", "Deep fry until golden and crispy."]),
        new("Banana Chips", "Snacks", "South Indian", "", ["banana"], ["Raw banana - 3", "Salt - 1 tsp", "Turmeric - 1/4 tsp", "Oil - for frying"], ["Peel and slice bananas thinly.", "Mix turmeric in water and soak slices.", "Drain and pat dry.", "Deep fry until crispy, sprinkle salt."]),
        new("Cucumber Raita", "Snacks", "Indian", "", ["yogurt", "cucumber"], ["Yogurt - 1 cup", "Cucumber - 1", "Salt - 1/2 tsp", "Cumin powder - 1/4 tsp"], ["Grate cucumber and squeeze out water.", "Mix into yogurt.", "Add salt and cumin powder.", "Chill and serve."]),
        new("Mango Lassi", "Snacks", "North Indian", "", ["mango", "yogurt", "milk"], ["Mango - 1", "Yogurt - 1 cup", "Milk - 1/2 cup", "Sugar - 2 tbsp"], ["Peel and chop mango.", "Blend mango, yogurt, milk and sugar.", "Pour into glasses.", "Serve chilled."]),
    ];

    private sealed record RecipeTemplate(
        string Name, string MealType, string Region, string ImageUrl,
        IReadOnlyList<string> Keywords, IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
