using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Services;

public class RecipeService
{
    private readonly IProductRepository _productRepository;

    private static readonly List<RecipeTemplate> Catalog =
    [
        // ── Breakfast ──
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
        new("Cheese Omelette", "Breakfast", "Western", "https://images.unsplash.com/photo-1525351484163-7529414344d8?auto=format&fit=crop&w=900&q=80", ["egg", "cheese"],
            ["Eggs - 3", "Cheese - 50g", "Butter - 1 tsp", "Salt - 1 tsp", "Pepper - pinch"],
            ["Beat eggs with salt and pepper.", "Melt butter in pan on medium heat.", "Pour eggs and cook until edges set.", "Add grated cheese, fold and serve."]
        ),
        new("Upma", "Breakfast", "South Indian", "https://images.unsplash.com/photo-1567337710282-00832b415979?auto=format&fit=crop&w=900&q=80", ["onion", "carrot", "peas"],
            ["Semolina/Rava - 1 cup", "Onion - 1", "Carrot - 1 small", "Green peas - 2 tbsp", "Salt - 1 tsp", "Oil - 2 tbsp"],
            ["Dry roast semolina until golden.", "Saute onion, carrot and peas in oil.", "Add 2 cups water and salt, bring to boil.", "Add semolina stirring constantly, cook 3 min."]
        ),
        new("Paratha with Yogurt", "Breakfast", "North Indian", "https://images.unsplash.com/photo-1565557623262-b51c2513a641?auto=format&fit=crop&w=900&q=80", ["flour", "yogurt", "butter"],
            ["Wheat flour - 2 cups", "Yogurt - 1 cup", "Butter/Ghee - 2 tbsp", "Salt - 1 tsp"],
            ["Knead dough with flour, salt and water.", "Roll into circles and cook on hot tawa.", "Apply butter on both sides.", "Serve hot with yogurt."]
        ),
        new("Masala Dosa", "Breakfast", "South Indian", "https://images.unsplash.com/photo-1630383249896-424e482df921?auto=format&fit=crop&w=900&q=80", ["rice", "potato", "onion"],
            ["Dosa batter/Rice - 2 cups", "Potato - 2", "Onion - 1", "Mustard seeds - 1 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"],
            ["Boil and mash potatoes.", "Saute onion with mustard seeds, mix in potato.", "Spread batter on hot tawa to make thin crepe.", "Add potato filling, fold and serve."]
        ),
        new("Peanut Butter Toast", "Breakfast", "Western", "https://images.unsplash.com/photo-1484723091739-30a097e8f929?auto=format&fit=crop&w=900&q=80", ["bread", "peanut"],
            ["Bread - 2 slices", "Peanut butter - 2 tbsp", "Banana - 1 (optional)", "Honey - 1 tsp"],
            ["Toast bread until golden.", "Spread peanut butter evenly.", "Top with sliced banana and drizzle honey.", "Serve immediately."]
        ),

        // ── Lunch ──
        new("Tomato Rice", "Lunch", "South Indian", "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=900&q=80", ["tomato", "rice", "onion"],
            ["Rice - 500g", "Tomato - 3", "Onion - 1", "Salt - 2 tsp", "Oil - 2 tbsp"],
            ["Cook rice and keep aside.", "Saute onion then tomato in oil.", "Add salt and cook gravy.", "Mix cooked rice and simmer 2 minutes."]
        ),
        new("Vegetable Sandwich", "Lunch", "Western", "https://images.unsplash.com/photo-1481070414801-51fd732d7184?auto=format&fit=crop&w=900&q=80", ["bread", "tomato", "onion"],
            ["Bread - 4 slices", "Tomato - 1", "Onion - 1", "Butter - 1 tbsp", "Salt - 1 tsp"],
            ["Slice vegetables thinly.", "Spread butter on bread.", "Layer vegetables and season.", "Toast and serve."]
        ),
        new("Dal Rice", "Lunch", "Indian", "https://images.unsplash.com/photo-1596797038530-2c107229654b?auto=format&fit=crop&w=900&q=80", ["dal", "rice", "onion"],
            ["Toor dal - 1 cup", "Rice - 2 cups", "Onion - 1", "Tomato - 1", "Turmeric - 1/2 tsp", "Salt - 2 tsp", "Ghee - 1 tbsp"],
            ["Cook dal with turmeric until soft.", "Cook rice separately.", "Prepare tadka with onion and tomato.", "Pour tadka over dal and serve with rice."]
        ),
        new("Chicken Fried Rice", "Lunch", "Indo-Chinese", "https://images.unsplash.com/photo-1603133872878-684f208fb84b?auto=format&fit=crop&w=900&q=80", ["chicken", "rice", "onion"],
            ["Cooked rice - 3 cups", "Chicken - 200g", "Onion - 1", "Soy sauce - 2 tbsp", "Oil - 2 tbsp", "Salt - 1 tsp"],
            ["Cook and dice chicken pieces.", "Stir-fry onion in oil on high heat.", "Add chicken and cooked rice.", "Add soy sauce, toss well and serve."]
        ),
        new("Paneer Butter Masala", "Lunch", "North Indian", "https://images.unsplash.com/photo-1631452180519-c014fe946bc7?auto=format&fit=crop&w=900&q=80", ["paneer", "tomato", "butter"],
            ["Paneer - 250g", "Tomato - 4", "Butter - 2 tbsp", "Cream - 2 tbsp", "Onion - 1", "Salt - 1 tsp", "Garam masala - 1 tsp"],
            ["Blend tomato and onion into smooth paste.", "Cook paste in butter until oil separates.", "Add paneer cubes and cream.", "Simmer 5 min and serve with rice or naan."]
        ),
        new("Egg Curry with Rice", "Lunch", "Indian", "https://images.unsplash.com/photo-1574484284002-952d92456975?auto=format&fit=crop&w=900&q=80", ["egg", "rice", "onion", "tomato"],
            ["Eggs - 4", "Rice - 2 cups", "Onion - 2", "Tomato - 2", "Turmeric - 1/2 tsp", "Salt - 1 tsp", "Oil - 2 tbsp"],
            ["Boil eggs and halve them.", "Saute onion and tomato with spices.", "Add eggs to the gravy and simmer.", "Serve hot with steamed rice."]
        ),
        new("Pasta Arrabiata", "Lunch", "Italian", "https://images.unsplash.com/photo-1621996346565-e3dbc646d9a9?auto=format&fit=crop&w=900&q=80", ["pasta", "tomato", "garlic"],
            ["Pasta - 250g", "Tomato - 4", "Garlic - 4 cloves", "Chilli flakes - 1 tsp", "Olive oil - 2 tbsp", "Salt - 1 tsp"],
            ["Cook pasta according to package directions.", "Saute garlic in olive oil, add chopped tomato.", "Add chilli flakes and cook into sauce.", "Toss pasta in sauce and serve."]
        ),
        new("Curd Rice", "Lunch", "South Indian", "https://images.unsplash.com/photo-1585937421612-70a008356fbe?auto=format&fit=crop&w=900&q=80", ["rice", "yogurt", "cucumber"],
            ["Rice - 2 cups cooked", "Yogurt/Curd - 1 cup", "Cucumber - 1 small", "Mustard seeds - 1 tsp", "Salt - 1 tsp"],
            ["Mash cooked rice slightly.", "Mix in yogurt and salt.", "Add finely chopped cucumber.", "Temper with mustard seeds and serve."]
        ),
        new("Lemon Rice", "Lunch", "South Indian", "https://images.unsplash.com/photo-1536304929831-ee1ca9d44906?auto=format&fit=crop&w=900&q=80", ["rice", "lemon", "peanut"],
            ["Rice - 2 cups cooked", "Lemon juice - 2 tbsp", "Peanuts - 2 tbsp", "Turmeric - 1/4 tsp", "Salt - 1 tsp", "Oil - 1 tbsp"],
            ["Roast peanuts in oil.", "Add turmeric and cooked rice.", "Squeeze lemon juice and mix well.", "Garnish with coriander and serve."]
        ),

        // ── Dinner ──
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
        new("Chicken Curry", "Dinner", "Indian", "https://images.unsplash.com/photo-1603894584373-5ac82b2ae398?auto=format&fit=crop&w=900&q=80", ["chicken", "onion", "tomato"],
            ["Chicken - 500g", "Onion - 2", "Tomato - 2", "Ginger-garlic paste - 1 tbsp", "Salt - 1 tsp", "Oil - 2 tbsp", "Garam masala - 1 tsp"],
            ["Heat oil and saute onion until golden.", "Add ginger-garlic paste and tomato.", "Add chicken and spices, cook covered 20 min.", "Garnish and serve with rice or roti."]
        ),
        new("Palak Paneer", "Dinner", "North Indian", "https://images.unsplash.com/photo-1618449840665-9ed506d73a34?auto=format&fit=crop&w=900&q=80", ["spinach", "paneer", "onion"],
            ["Spinach - 500g", "Paneer - 200g", "Onion - 1", "Garlic - 3 cloves", "Cream - 2 tbsp", "Salt - 1 tsp"],
            ["Blanch spinach and blend to paste.", "Saute onion and garlic.", "Add spinach paste and paneer cubes.", "Finish with cream and serve."]
        ),
        new("Fish Fry", "Dinner", "Coastal Indian", "https://images.unsplash.com/photo-1534422298391-e4f8c172dddb?auto=format&fit=crop&w=900&q=80", ["fish", "lemon", "garlic"],
            ["Fish fillets - 4", "Lemon juice - 2 tbsp", "Garlic paste - 1 tsp", "Chilli powder - 1 tsp", "Salt - 1 tsp", "Oil - 3 tbsp"],
            ["Marinate fish with lemon, garlic, chilli and salt.", "Rest for 15 minutes.", "Shallow fry in oil until golden on both sides.", "Serve hot with onion rings and lemon wedge."]
        ),
        new("Mushroom Stir Fry", "Dinner", "Asian", "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=900&q=80", ["mushroom", "garlic", "onion"],
            ["Mushrooms - 300g", "Garlic - 4 cloves", "Onion - 1", "Soy sauce - 1 tbsp", "Butter - 1 tbsp", "Salt - 1 tsp"],
            ["Slice mushrooms and onion.", "Saute garlic in butter.", "Add mushrooms and cook on high heat.", "Add soy sauce and serve."]
        ),
        new("Rajma Chawal", "Dinner", "North Indian", "https://images.unsplash.com/photo-1585937421612-70a008356fbe?auto=format&fit=crop&w=900&q=80", ["rajma", "rice", "onion", "tomato"],
            ["Rajma/Kidney beans - 1 cup", "Rice - 2 cups", "Onion - 1", "Tomato - 2", "Garam masala - 1 tsp", "Salt - 1 tsp"],
            ["Soak rajma overnight and pressure cook.", "Prepare gravy with onion and tomato.", "Add cooked rajma and simmer 15 min.", "Serve hot with steamed rice."]
        ),
        new("Noodle Stir Fry", "Dinner", "Asian", "https://images.unsplash.com/photo-1569718212165-3a8278d5f624?auto=format&fit=crop&w=900&q=80", ["noodle", "carrot", "cabbage", "onion"],
            ["Noodles - 200g", "Carrot - 1", "Cabbage - 1 cup", "Onion - 1", "Soy sauce - 2 tbsp", "Oil - 2 tbsp"],
            ["Boil noodles and drain.", "Stir-fry vegetables in oil on high heat.", "Add noodles and soy sauce.", "Toss well and serve hot."]
        ),

        // ── Snacks ──
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
        ),
        new("Aloo Tikki", "Snacks", "North Indian", "https://images.unsplash.com/photo-1601050690597-df0568f70950?auto=format&fit=crop&w=900&q=80", ["potato", "onion", "peas"],
            ["Potato - 3", "Onion - 1 small", "Green peas - 2 tbsp", "Bread crumbs - 2 tbsp", "Salt - 1 tsp", "Oil - 3 tbsp"],
            ["Boil and mash potatoes.", "Mix in chopped onion, peas and salt.", "Shape into patties and coat with breadcrumbs.", "Shallow fry until golden on both sides."]
        ),
        new("Corn Chat", "Snacks", "Indian", "https://images.unsplash.com/photo-1551754655-cd27e38d2076?auto=format&fit=crop&w=900&q=80", ["corn", "onion", "lemon"],
            ["Sweet corn - 2 cups", "Onion - 1 small", "Lemon juice - 1 tbsp", "Chilli powder - 1/2 tsp", "Salt - 1 tsp", "Butter - 1 tsp"],
            ["Boil or roast corn kernels.", "Mix with chopped onion.", "Add lemon juice, chilli powder and salt.", "Top with butter and serve warm."]
        ),
        new("Paneer Tikka", "Snacks", "North Indian", "https://images.unsplash.com/photo-1567188040759-fb8a883dc6d8?auto=format&fit=crop&w=900&q=80", ["paneer", "yogurt", "capsicum"],
            ["Paneer - 250g", "Yogurt - 3 tbsp", "Capsicum/Bell pepper - 1", "Onion - 1", "Tikka masala - 1 tbsp", "Salt - 1 tsp"],
            ["Cut paneer and vegetables into cubes.", "Marinate in yogurt and spices for 30 min.", "Thread onto skewers.", "Grill or pan-fry until charred and serve."]
        ),
        new("Mango Lassi", "Snacks", "Indian", "https://images.unsplash.com/photo-1527661591475-527312dd65f5?auto=format&fit=crop&w=900&q=80", ["mango", "yogurt", "milk"],
            ["Mango - 1 large", "Yogurt - 1 cup", "Milk - 1/2 cup", "Sugar - 2 tbsp"],
            ["Peel and chop mango.", "Blend mango, yogurt, milk and sugar.", "Pour into glasses.", "Serve chilled with ice."]
        ),
        new("Garlic Bread", "Snacks", "Italian", "https://images.unsplash.com/photo-1573140401552-3fab0b24306f?auto=format&fit=crop&w=900&q=80", ["bread", "garlic", "butter", "cheese"],
            ["Bread/Baguette - 1", "Garlic - 4 cloves", "Butter - 3 tbsp", "Cheese - 50g", "Parsley - 1 tbsp"],
            ["Mix minced garlic with softened butter.", "Slice bread and spread garlic butter.", "Top with grated cheese.", "Bake at 180°C for 10 min until golden."]
        ),
        new("Coconut Ladoo", "Snacks", "Indian", "https://images.unsplash.com/photo-1589249472831-35e4bc9ead5d?auto=format&fit=crop&w=900&q=80", ["coconut", "milk", "sugar"],
            ["Desiccated coconut - 2 cups", "Condensed milk - 200 ml", "Sugar - 3 tbsp", "Cardamom - 2 pods"],
            ["Mix coconut and condensed milk in a pan.", "Cook on low heat stirring constantly.", "Add sugar and cardamom.", "Cool slightly, shape into balls and serve."]
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

    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["curd"] = ["yogurt"],
        ["yoghurt"] = ["yogurt"],
        ["dahi"] = ["yogurt"],
        ["atta"] = ["flour"],
        ["wheat"] = ["flour"],
        ["maida"] = ["flour"],
        ["palak"] = ["spinach"],
        ["aloo"] = ["potato"],
        ["tamatar"] = ["tomato"],
        ["pyaz"] = ["onion"],
        ["shimla mirch"] = ["capsicum"],
        ["bell pepper"] = ["capsicum"],
        ["chana"] = ["chickpea"],
        ["rajma"] = ["rajma"],
        ["kidney bean"] = ["rajma"],
        ["maggi"] = ["noodle"],
        ["instant noodle"] = ["noodle"],
        ["spaghetti"] = ["pasta"],
        ["macaroni"] = ["pasta"],
        ["penne"] = ["pasta"],
        ["toor dal"] = ["dal"],
        ["moong dal"] = ["dal"],
        ["masoor dal"] = ["dal"],
        ["lentil"] = ["dal"],
        ["ghee"] = ["butter"],
        ["cottage cheese"] = ["paneer"],
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
                // Handle common plurals
                if (token.EndsWith("s") && token.Length > 3) tokens.Add(token[..^1]);
                if (token.EndsWith("es") && token.Length > 4) tokens.Add(token[..^2]);
            }

            // Synonym expansion
            foreach (var (key, values) in Synonyms)
            {
                if (n.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var v in values) tokens.Add(v);
                }
            }

            // Category-based expansions
            if (n.Contains("vegetable")) { tokens.Add("tomato"); tokens.Add("onion"); tokens.Add("carrot"); tokens.Add("cabbage"); tokens.Add("potato"); tokens.Add("peas"); }
            if (n.Contains("fruit")) { tokens.Add("banana"); tokens.Add("apple"); tokens.Add("orange"); tokens.Add("mango"); }
            if (n.Contains("bakery")) { tokens.Add("bread"); tokens.Add("biscuit"); }
            if (n.Contains("snack")) tokens.Add("biscuit");
            if (n.Contains("dairy")) { tokens.Add("milk"); tokens.Add("yogurt"); tokens.Add("cheese"); tokens.Add("butter"); }
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
