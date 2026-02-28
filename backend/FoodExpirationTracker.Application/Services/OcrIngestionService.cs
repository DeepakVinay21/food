using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Services;

public class OcrIngestionService
{
    private readonly IOcrService _ocrService;
    private readonly IGeminiVisionService _geminiVisionService;
    private readonly ProductService _productService;

    public OcrIngestionService(IOcrService ocrService, IGeminiVisionService geminiVisionService, ProductService productService)
    {
        _ocrService = ocrService;
        _geminiVisionService = geminiVisionService;
        _productService = productService;
    }

    public async Task<ProductDto> ScanAndAddAsync(Guid userId, OcrScanRequest request, CancellationToken cancellationToken = default)
    {
        var parsed = _ocrService.ParseText(request.RawText);

        var addRequest = new AddProductRequest(
            parsed.ProductName,
            parsed.CategoryName,
            parsed.ExpiryDate,
            Math.Max(1, request.Quantity));

        return await _productService.AddProductBatchAsync(userId, addRequest, cancellationToken);
    }

    public async Task<OcrImagePreviewResponse> ScanImagePreviewAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var geminiResult = await _geminiVisionService.ExtractFieldsAsync([imageBytes], cancellationToken);
        var rawText = await ExtractTextWithGeminiRequiredAsync([imageBytes], cancellationToken);
        var parsedFromText = _ocrService.ParseText(rawText, imageBytes);
        var combined = MergeAiAndParsed(geminiResult?.ScanResult, parsedFromText);
        var perItemDetails = geminiResult?.PerItemDetails ?? BuildPerItemDetails(combined);
        return new OcrImagePreviewResponse(combined, rawText, perItemDetails);
    }

    public async Task<OcrImagePreviewResponse> ScanFrontBackPreviewAsync(byte[] frontImageBytes, byte[] backImageBytes, CancellationToken cancellationToken = default)
    {
        var images = new[] { frontImageBytes, backImageBytes };
        var geminiResult = await _geminiVisionService.ExtractFieldsAsync(images, cancellationToken);
        var rawText = await ExtractTextWithGeminiRequiredAsync(images, cancellationToken);
        var parsedFromText = _ocrService.ParseText(rawText, frontImageBytes);
        var combined = MergeAiAndParsed(geminiResult?.ScanResult, parsedFromText);
        var perItemDetails = geminiResult?.PerItemDetails ?? BuildPerItemDetails(combined);
        return new OcrImagePreviewResponse(combined, rawText, perItemDetails);
    }

    public async Task<OcrImagePreviewResponse> ScanMultiPreviewAsync(IReadOnlyList<byte[]> imageBytesList, CancellationToken cancellationToken = default)
    {
        if (imageBytesList.Count == 0)
        {
            throw new InvalidOperationException("At least one image is required.");
        }

        var geminiResult = await _geminiVisionService.ExtractFieldsAsync(imageBytesList, cancellationToken);
        var rawText = await ExtractTextWithGeminiRequiredAsync(imageBytesList, cancellationToken);
        var parsedFromText = _ocrService.ParseText(rawText, imageBytesList[0]);
        var combined = MergeAiAndParsed(geminiResult?.ScanResult, parsedFromText);
        var perItemDetails = geminiResult?.PerItemDetails ?? BuildPerItemDetails(combined);
        return new OcrImagePreviewResponse(combined, rawText, perItemDetails);
    }

    public async Task<OcrImageScanResponse> ScanImageAndAddAsync(Guid userId, byte[] imageBytes, int quantity, CancellationToken cancellationToken = default)
    {
        var preview = await ScanImagePreviewAsync(imageBytes, cancellationToken);
        return await AddFromPreviewAsync(userId, preview, quantity, cancellationToken);
    }

    public async Task<OcrImageScanResponse> ScanFrontBackAndAddAsync(Guid userId, byte[] frontImageBytes, byte[] backImageBytes, int quantity, CancellationToken cancellationToken = default)
    {
        var preview = await ScanFrontBackPreviewAsync(frontImageBytes, backImageBytes, cancellationToken);
        return await AddFromPreviewAsync(userId, preview, quantity, cancellationToken);
    }

    public async Task<OcrImageScanResponse> ScanMultiAndAddAsync(Guid userId, IReadOnlyList<byte[]> imageBytesList, int quantity, CancellationToken cancellationToken = default)
    {
        var preview = await ScanMultiPreviewAsync(imageBytesList, cancellationToken);
        return await AddFromPreviewAsync(userId, preview, quantity, cancellationToken);
    }

    /// <summary>
    /// Split-add: adds each detected item with its OWN category and expiry, not reusing a single set of values.
    /// </summary>
    public async Task<OcrMultiAddResponse> SplitAddAllAsync(Guid userId, OcrImagePreviewResponse preview, int defaultQuantity, CancellationToken cancellationToken = default)
    {
        var addedProducts = new List<ProductDto>();

        if (preview.DetectedItems is { Count: > 0 })
        {
            foreach (var item in preview.DetectedItems)
            {
                var addRequest = new AddProductRequest(
                    item.ProductName,
                    item.CategoryName,
                    item.ExpiryDate,
                    Math.Max(1, defaultQuantity));

                var product = await _productService.AddProductBatchAsync(userId, addRequest, cancellationToken);
                addedProducts.Add(product);
            }
        }
        else if (preview.Extracted.ProductCandidates is { Count: > 0 })
        {
            // Fallback: use candidates with per-item category and expiry
            foreach (var candidateName in preview.Extracted.ProductCandidates)
            {
                var itemCategory = InferCategoryFromName(candidateName) ?? preview.Extracted.CategoryName;

                // Only use the global expiry for the primary product; compute per-item fallback for others
                var isPrimary = candidateName.Equals(preview.Extracted.ProductName, StringComparison.OrdinalIgnoreCase);
                var itemExpiry = isPrimary && preview.Extracted.ExpiryDate != default
                    ? preview.Extracted.ExpiryDate
                    : GetCategoryFallbackExpiry(itemCategory, candidateName);

                var addRequest = new AddProductRequest(
                    candidateName,
                    itemCategory,
                    itemExpiry,
                    Math.Max(1, defaultQuantity));

                var product = await _productService.AddProductBatchAsync(userId, addRequest, cancellationToken);
                addedProducts.Add(product);
            }
        }
        else
        {
            var addRequest = new AddProductRequest(
                preview.Extracted.ProductName,
                preview.Extracted.CategoryName,
                preview.Extracted.ExpiryDate,
                Math.Max(1, defaultQuantity));

            var product = await _productService.AddProductBatchAsync(userId, addRequest, cancellationToken);
            addedProducts.Add(product);
        }

        return new OcrMultiAddResponse(preview.Extracted, addedProducts, preview.RawText);
    }

    private async Task<OcrImageScanResponse> AddFromPreviewAsync(Guid userId, OcrImagePreviewResponse preview, int quantity, CancellationToken cancellationToken)
    {
        var addRequest = new AddProductRequest(
            preview.Extracted.ProductName,
            preview.Extracted.CategoryName,
            preview.Extracted.ExpiryDate,
            Math.Max(1, quantity));

        var product = await _productService.AddProductBatchAsync(userId, addRequest, cancellationToken);
        return new OcrImageScanResponse(preview.Extracted, product, preview.RawText);
    }

    private async Task<string> ExtractTextWithGeminiRequiredAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken)
    {
        var text = await _geminiVisionService.ExtractTextAsync(images, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini could not extract text from image. Check GEMINI_API_KEY and retry with a clearer image.");
        }

        return text.Trim();
    }

    private static OcrScanResult MergeAiAndParsed(OcrScanResult? ai, OcrScanResult parsed)
    {
        if (ai is null)
        {
            var fallbackMfg = parsed.ManufacturingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var fallbackExpiry = EnsureFutureExpiry(parsed.ExpiryDate, parsed.CategoryName, parsed.ProductName);
            var fallbackDaysLeft = fallbackExpiry.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
            var parsedCandidates = BuildCandidates(parsed.ProductName, parsed.ProductCandidates);
            var score = Math.Max(parsed.ConfidenceScore, 20);
            return new OcrScanResult(parsed.ProductName, fallbackMfg, fallbackExpiry, fallbackDaysLeft, parsed.CategoryName, parsed.IsConfidenceLow, parsedCandidates, score, parsed.FieldConfidence, parsed.NeedsHumanReview);
        }

        var productName = ai.ProductName != "Unknown Product" ? ai.ProductName : parsed.ProductName;
        var categoryName = ai.CategoryName != "General" ? ai.CategoryName : parsed.CategoryName;
        var manufacturingDate = ai.ManufacturingDate ?? parsed.ManufacturingDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var expiryDate = SelectBestExpiry(ai.ExpiryDate, parsed.ExpiryDate);

        // When both sources have low expiry confidence (both used fallbacks),
        // recompute using the merged category for better accuracy
        var aiExpiryLow = ai.FieldConfidence?.ExpiryConfidence == "low";
        var parsedExpiryLow = parsed.FieldConfidence?.ExpiryConfidence == "low";
        if (aiExpiryLow && parsedExpiryLow)
        {
            expiryDate = GetCategoryFallbackExpiry(categoryName, productName);
        }

        expiryDate = EnsureFutureExpiry(expiryDate, categoryName, productName);

        var daysLeft = expiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        var lowConfidence = ai.IsConfidenceLow && parsed.IsConfidenceLow;
        var candidates = BuildCandidates(productName, ai.ProductCandidates, parsed.ProductCandidates);
        var confidenceScore = Math.Max(ai.ConfidenceScore, parsed.ConfidenceScore);

        // Merge per-field confidences: take the higher confidence per field
        var fieldConfidence = MergeFieldConfidence(ai.FieldConfidence, parsed.FieldConfidence);
        var needsReview = (ai.NeedsHumanReview && parsed.NeedsHumanReview)
            || fieldConfidence is not null && (fieldConfidence.NameConfidence == "low" || fieldConfidence.ExpiryConfidence == "low")
            || confidenceScore < 50;

        return new OcrScanResult(productName, manufacturingDate, expiryDate, daysLeft, categoryName, lowConfidence, candidates, confidenceScore, fieldConfidence, needsReview);
    }

    private static FieldConfidenceDto? MergeFieldConfidence(FieldConfidenceDto? a, FieldConfidenceDto? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return new FieldConfidenceDto(
            HigherConfidence(a.NameConfidence, b.NameConfidence),
            HigherConfidence(a.ExpiryConfidence, b.ExpiryConfidence),
            HigherConfidence(a.CategoryConfidence, b.CategoryConfidence));
    }

    private static string HigherConfidence(string a, string b)
    {
        static int Rank(string c) => c switch { "high" => 3, "medium" => 2, _ => 1 };
        return Rank(a) >= Rank(b) ? a : b;
    }

    private static DateOnly EnsureFutureExpiry(DateOnly expiry, string categoryName = "General", string? productName = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expiry == default || expiry <= today)
        {
            return GetCategoryFallbackExpiry(categoryName, productName);
        }

        return expiry;
    }

    /// <summary>
    /// Product-name-aware category fallback expiry. Accounts for product-specific shelf lives
    /// (e.g., UHT milk 90 days vs fresh milk 14 days, frozen meat 90 days vs fresh meat 3 days).
    /// </summary>
    internal static DateOnly GetCategoryFallbackExpiry(string category, string? productName = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lower = productName?.ToLowerInvariant() ?? "";

        return category switch
        {
            "Dairy" when lower.Contains("uht") || lower.Contains("long life") => today.AddDays(90),
            "Dairy" when lower.Contains("parmesan") || lower.Contains("aged") => today.AddDays(60),
            "Dairy" when lower.Contains("yogurt") || lower.Contains("curd") => today.AddDays(14),
            "Dairy" => today.AddDays(14),
            "Meat" when lower.Contains("frozen") => today.AddDays(90),
            "Meat" when lower.Contains("canned") => today.AddDays(365),
            "Meat" when lower.Contains("dried") || lower.Contains("jerky") => today.AddDays(180),
            "Meat" => today.AddDays(3),
            "Fruits" when lower.Contains("dried") || lower.Contains("raisin") => today.AddDays(180),
            "Fruits" when lower.Contains("canned") => today.AddDays(365),
            "Fruits" when lower.Contains("jam") || lower.Contains("preserve") => today.AddDays(180),
            "Fruits" => today.AddDays(5),
            "Vegetables" when lower.Contains("canned") => today.AddDays(365),
            "Vegetables" when lower.Contains("frozen") => today.AddDays(90),
            "Vegetables" when lower.Contains("pickled") || lower.Contains("pickle") => today.AddDays(180),
            "Vegetables" => today.AddDays(7),
            "Bakery Item" when lower.Contains("frozen") => today.AddDays(90),
            "Bakery Item" => today.AddDays(5),
            "Snacks" => today.AddDays(90),
            "Grains" => today.AddDays(180),
            "Beverages" when lower.Contains("fresh") => today.AddDays(7),
            "Beverages" when lower.Contains("uht") || lower.Contains("tetra") => today.AddDays(180),
            "Beverages" => today.AddDays(90),
            "Condiments" => today.AddDays(180),
            "Frozen" => today.AddDays(90),
            _ => today.AddDays(30)
        };
    }

    private static DateOnly SelectBestExpiry(DateOnly aiExpiry, DateOnly parsedExpiry)
    {
        if (aiExpiry == default) return parsedExpiry;
        if (parsedExpiry == default) return aiExpiry;

        // When AI and parser disagree, prefer the later date to avoid premature-expiry regressions
        return aiExpiry > parsedExpiry ? aiExpiry : parsedExpiry;
    }

    private static IReadOnlyList<string> BuildCandidates(string productName, params IReadOnlyList<string>?[] candidateLists)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(productName) && !string.Equals(productName, "Unknown Product", StringComparison.OrdinalIgnoreCase))
        {
            set.Add(productName.Trim());
        }

        foreach (var list in candidateLists)
        {
            if (list is null)
            {
                continue;
            }

            foreach (var item in list)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                var cleaned = item.Trim();
                if (cleaned.Length is < 2 or > 60)
                {
                    continue;
                }

                set.Add(cleaned);
            }
        }

        return set.Take(12).ToArray();
    }

    /// <summary>
    /// Build per-item details from candidates with their own category for split-add.
    /// </summary>
    private static IReadOnlyList<DetectedItemResult>? BuildPerItemDetails(OcrScanResult combined)
    {
        if (combined.ProductCandidates is not { Count: > 1 })
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<DetectedItemResult>();

        foreach (var candidateName in combined.ProductCandidates)
        {
            var category = InferCategoryFromName(candidateName) ?? combined.CategoryName;
            var isPrimary = candidateName.Equals(combined.ProductName, StringComparison.OrdinalIgnoreCase);
            var expiry = isPrimary ? combined.ExpiryDate : GetCategoryFallbackExpiry(category, candidateName);
            var daysLeft = expiry.DayNumber - today.DayNumber;
            var needsReview = !isPrimary || combined.NeedsHumanReview;

            results.Add(new DetectedItemResult(candidateName, category, expiry, daysLeft, combined.ConfidenceScore, needsReview));
        }

        return results.Count > 0 ? results : null;
    }

    private static string? InferCategoryFromName(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("milk") || lower.Contains("cheese") || lower.Contains("butter") || lower.Contains("yogurt") || lower.Contains("cream") || lower.Contains("paneer") || lower.Contains("curd")) return "Dairy";
        if (lower.Contains("bread") || lower.Contains("bun") || lower.Contains("cake") || lower.Contains("pastry")) return "Bakery Item";
        if (lower.Contains("biscuit") || lower.Contains("cookie") || lower.Contains("chocolate") || lower.Contains("chips") || lower.Contains("wafer")) return "Snacks";
        if (lower.Contains("banana") || lower.Contains("apple") || lower.Contains("orange") || lower.Contains("mango") || lower.Contains("grape")) return "Fruits";
        if (lower.Contains("chicken") || lower.Contains("beef") || lower.Contains("fish") || lower.Contains("mutton") || lower.Contains("meat")) return "Meat";
        if (lower.Contains("tomato") || lower.Contains("onion") || lower.Contains("potato") || lower.Contains("carrot") || lower.Contains("spinach")) return "Vegetables";
        if (lower.Contains("rice") || lower.Contains("pasta") || lower.Contains("noodle") || lower.Contains("oats") || lower.Contains("cereal")) return "Grains";
        if (lower.Contains("juice") || lower.Contains("soda") || lower.Contains("water") || lower.Contains("tea") || lower.Contains("coffee")) return "Beverages";
        return null;
    }

}
