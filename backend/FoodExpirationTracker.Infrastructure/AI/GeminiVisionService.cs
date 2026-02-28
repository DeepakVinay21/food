using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Infrastructure.Ocr;

namespace FoodExpirationTracker.Infrastructure.AI;

public class GeminiVisionService : IGeminiVisionService
{
    private readonly HttpClient _httpClient;
    private static readonly SemaphoreSlim GeminiThrottle = new(5, 5);

    public GeminiVisionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GeminiExtractionResult?> ExtractFieldsAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default)
    {
        var enabled = Environment.GetEnvironmentVariable("GEMINI_ENABLED");
        if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase) || images.Count == 0)
        {
            return null;
        }

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing GEMINI_API_KEY.");
        }

        var parts = new List<object>
        {
            new
            {
                text = """
                       Extract and return JSON only:
                       {
                         "productName": "...",
                         "detectedItems": [
                           {"name": "item1", "category": "Dairy", "expiryDate": "YYYY-MM-DD or null"},
                           {"name": "item2", "category": "Fruits", "expiryDate": "YYYY-MM-DD or null"}
                         ],
                         "categoryName": "...",
                         "manufacturingDate": "YYYY-MM-DD or null",
                         "expiryDate": "YYYY-MM-DD or null",
                         "bestBeforeText": "original best-before text if found, e.g. 'best before 6 months from mfg'",
                         "bestBeforeValue": 0,
                         "bestBeforeUnit": "days|months|years|null",
                         "confidence": "high|medium|low",
                         "fieldConfidence": {
                           "name": "high|medium|low",
                           "expiry": "high|medium|low",
                           "category": "high|medium|low"
                         }
                       }
                       Rules:
                       - Handle difficult images: blurred text, glare, rotated labels, handwriting. Try reading from all orientations (0, 90, 180, 270 degrees).
                       - For blurry or low-resolution images: focus on the largest/clearest text first. Expiry/best-before dates are often on a separate label area or printed in a different font/color.
                       - Look for date stamps, embossed/ink-jet-printed dates, and sticker labels which may differ from the main label text.
                       - If text is partially readable, extract what you can and lower the field confidence accordingly.
                       - Prefer visible printed label text from product box/pack.
                       - If multiple distinct products are visible, you MUST list EACH as a separate object in detectedItems with its OWN name, category, and expiryDate. Do NOT combine multiple products into one entry.
                       - Each detectedItems entry MUST represent a DISTINCT physical product (different brand, type, or package). Do NOT split a single product's ingredients or label text into multiple items.
                       - If the same product appears in multiple images, merge into ONE detectedItems entry.
                       - Each detectedItems entry MUST have a meaningful food product name (not a date, barcode, or label fragment).
                       - If no readable label text, classify visible food products directly from image appearance.
                       - When you see multiple loose or unpackaged fruits or vegetables together (e.g., a pile of produce on a table or counter), identify EACH visually distinct type of fruit or vegetable as a separate detectedItems entry with its own name and category. For example, if you see tomatoes, carrots, and spinach together, return 3 separate items: 'Tomato', 'Carrot', 'Spinach'. Do NOT group them as 'Mixed Vegetables', 'Assorted Produce', or 'Mixed Fresh Vegetables'.
                       - For unpackaged produce without labels, set expiryDate to null, confidence to 'low', and fieldConfidence.expiry to 'low'. The system will apply category-based shelf-life defaults automatically.
                       - Ignore background text, shelf labels, and unrelated objects.
                       - Use category from: Vegetables, Fruits, Bakery Item, Snacks, Dairy, Meat, Grains, Beverages, Condiments, Frozen, General.
                       - If expiry not explicit and text says best before X months/years/days from mfg, fill bestBeforeValue and bestBeforeUnit.
                       - Copy the original best-before phrasing into bestBeforeText so the parser can handle edge cases.
                       - confidence: "high" if label text is clearly readable, "medium" if partially readable, "low" if guessing from image.
                       - fieldConfidence: per-field - "high" if clearly read from label, "medium" if partially readable, "low" if inferred or guessed.
                       """
            }
        };

        foreach (var imageBytes in images.Take(4))
        {
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/jpeg",
                    data = Convert.ToBase64String(imageBytes)
                }
            });
        }

        var body = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var content = await TryGenerateContentAsync(body, apiKey, cancellationToken, throwOnFailure: true);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var modelJson = ExtractModelJson(content);
        if (string.IsNullOrWhiteSpace(modelJson))
        {
            return null;
        }

        var extracted = JsonSerializer.Deserialize<GeminiOcrResponse>(modelJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (extracted is null)
        {
            return null;
        }

        var detectedNames = NormalizeDetectedItemNames(extracted.DetectedItemDetails, extracted.ProductName);
        var productName = string.IsNullOrWhiteSpace(extracted.ProductName) ? "Unknown Product" : extracted.ProductName.Trim();
        if (productName == "Unknown Product" && detectedNames.Count > 0)
        {
            productName = detectedNames[0];
        }

        var categoryName = string.IsNullOrWhiteSpace(extracted.CategoryName) ? "General" : extracted.CategoryName.Trim();
        if (categoryName == "General" && detectedNames.Count > 0)
        {
            categoryName = InferCategoryFromItems(detectedNames);
        }

        var manufacturingDate = ParseDate(extracted.ManufacturingDate);
        var expiryParsed = ParseDate(extracted.ExpiryDate);

        // Try structured bestBeforeValue/Unit first
        expiryParsed ??= TryDeriveExpiryFromBestBefore(
            manufacturingDate,
            extracted.BestBeforeValue,
            extracted.BestBeforeUnit,
            extracted.ExpiryDate);

        // Try parsing the raw bestBeforeText for edge-case phrasings the structured fields missed
        if (!expiryParsed.HasValue && !string.IsNullOrWhiteSpace(extracted.BestBeforeText))
        {
            expiryParsed = TryDeriveExpiryFromBestBeforeText(extracted.BestBeforeText, manufacturingDate);
        }

        // Smart category fallback instead of blanket +7
        var expiryDate = expiryParsed ?? RegexOcrService.GetCategoryFallbackExpiry(categoryName, productName);
        var daysLeft = expiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        var lowConfidence = string.Equals(extracted.Confidence, "low", StringComparison.OrdinalIgnoreCase) || !expiryParsed.HasValue;

        var (confidenceScore, fieldConfidence, needsReview) = ComputeGeminiConfidenceDetailed(
            extracted.Confidence, extracted.FieldConfidence, expiryParsed.HasValue, productName != "Unknown Product", detectedNames.Count);

        var scanResult = new OcrScanResult(productName, manufacturingDate, expiryDate, daysLeft, categoryName, lowConfidence, detectedNames, confidenceScore, fieldConfidence, needsReview);
        var perItemDetails = ExtractPerItemDetails(extracted);
        return new GeminiExtractionResult(scanResult, perItemDetails);
    }

    public async Task<OcrScanResult?> RefineAsync(
        string rawText,
        IReadOnlyList<byte[]> images,
        OcrScanResult baseline,
        CancellationToken cancellationToken = default)
    {
        var enabled = Environment.GetEnvironmentVariable("GEMINI_ENABLED");
        if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase) || images.Count == 0)
        {
            return null;
        }

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var parts = new List<object>
            {
                new
                {
                    text = BuildPrompt(rawText, baseline)
                }
            };

            foreach (var imageBytes in images.Take(4))
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = Convert.ToBase64String(imageBytes)
                    }
                });
            }

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var content = await TryGenerateContentAsync(body, apiKey, cancellationToken, throwOnFailure: false);
            if (content is null)
            {
                return null;
            }

            var modelJson = ExtractModelJson(content);
            if (string.IsNullOrWhiteSpace(modelJson))
            {
                return null;
            }

            var extracted = JsonSerializer.Deserialize<GeminiOcrResponse>(modelJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (extracted is null)
            {
                return null;
            }

            var detectedNames = NormalizeDetectedItemNames(extracted.DetectedItemDetails, extracted.ProductName);
            var productName = string.IsNullOrWhiteSpace(extracted.ProductName) ? baseline.ProductName : extracted.ProductName.Trim();
            if ((string.IsNullOrWhiteSpace(productName) || productName == "Unknown Product") && detectedNames.Count > 0)
            {
                productName = detectedNames[0];
            }

            var categoryName = string.IsNullOrWhiteSpace(extracted.CategoryName) ? baseline.CategoryName : extracted.CategoryName.Trim();
            if ((string.IsNullOrWhiteSpace(categoryName) || categoryName == "General") && detectedNames.Count > 0)
            {
                categoryName = InferCategoryFromItems(detectedNames);
            }

            var manufacturingDate = ParseDate(extracted.ManufacturingDate) ?? baseline.ManufacturingDate;
            var expiryDate = ParseDate(extracted.ExpiryDate) ?? baseline.ExpiryDate;
            var daysLeft = expiryDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;

            var lowConfidence = string.Equals(extracted.Confidence, "low", StringComparison.OrdinalIgnoreCase)
                                || (productName == "Unknown Product" && categoryName == "General");

            var (confidenceScore, fieldConfidence, needsReview) = ComputeGeminiConfidenceDetailed(
                extracted.Confidence, extracted.FieldConfidence, true, productName != "Unknown Product", detectedNames.Count);
            var mergedCandidates = MergeCandidates(baseline.ProductCandidates, detectedNames);
            return new OcrScanResult(productName, manufacturingDate, expiryDate, daysLeft, categoryName, lowConfidence, mergedCandidates, confidenceScore, fieldConfidence, needsReview);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts per-item details from the Gemini response for multi-product support.
    /// </summary>
    public IReadOnlyList<DetectedItemResult>? ExtractPerItemDetails(GeminiOcrResponse? extracted)
    {
        if (extracted?.DetectedItemDetails is not { Length: > 0 })
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<DetectedItemResult>();

        foreach (var item in extracted.DetectedItemDetails)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length < 2)
            {
                continue;
            }

            var name = item.Name.Trim();
            var category = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category.Trim();
            var expiryParsed = ParseDate(item.ExpiryDate);
            var expiry = expiryParsed ?? RegexOcrService.GetCategoryFallbackExpiry(category, name);
            var daysLeft = expiry.DayNumber - today.DayNumber;
            var needsReview = !expiryParsed.HasValue;

            results.Add(new DetectedItemResult(name, category, expiry, daysLeft, 50, needsReview));
        }

        return results.Count > 0 ? results : null;
    }

    public async Task<string?> ExtractTextAsync(IReadOnlyList<byte[]> images, CancellationToken cancellationToken = default)
    {
        var enabled = Environment.GetEnvironmentVariable("GEMINI_ENABLED");
        if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase) || images.Count == 0)
        {
            return null;
        }

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing GEMINI_API_KEY.");
        }

        try
        {
            var parts = new List<object>
            {
                new
                {
                    text = "Read only the product/package label text from these images. Ignore background. Return plain text lines only."
                }
            };

            foreach (var imageBytes in images.Take(4))
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = Convert.ToBase64String(imageBytes)
                    }
                });
            }

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1
                }
            };

            var content = await TryGenerateContentAsync(body, apiKey, cancellationToken, throwOnFailure: true);
            if (content is null)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var first = candidates[0];
            if (!first.TryGetProperty("content", out var messageContent)
                || !messageContent.TryGetProperty("parts", out var messageParts))
            {
                return null;
            }

            var text = string.Join(
                "\n",
                messageParts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(string ProductName, string CategoryName)?> ClassifyProductImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var enabled = Environment.GetEnvironmentVariable("GEMINI_ENABLED");
        if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase) || imageBytes.Length == 0)
        {
            return null;
        }

        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            var parts = new List<object>
            {
                new
                {
                    text = """
                           Look at this image and identify the food product. Return JSON only:
                           {"productName": "...", "categoryName": "..."}
                           Use category from: Vegetables, Fruits, Bakery Item, Snacks, Dairy, Meat, Grains, Beverages, Condiments, Frozen, General.
                           If you cannot identify a food product, return {"productName": "Unknown Product", "categoryName": "General"}.
                           """
                },
                new
                {
                    inline_data = new
                    {
                        mime_type = "image/jpeg",
                        data = Convert.ToBase64String(imageBytes)
                    }
                }
            };

            var body = new
            {
                contents = new[] { new { role = "user", parts } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json" }
            };

            var content = await TryGenerateContentAsync(body, apiKey, cancellationToken, throwOnFailure: false);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var modelJson = ExtractModelJson(content);
            if (string.IsNullOrWhiteSpace(modelJson))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(modelJson);
            var root = doc.RootElement;
            var productName = root.TryGetProperty("productName", out var pn) ? pn.GetString() : null;
            var categoryName = root.TryGetProperty("categoryName", out var cn) ? cn.GetString() : null;

            if (string.IsNullOrWhiteSpace(productName) || productName == "Unknown Product")
            {
                return null;
            }

            return (productName.Trim(), categoryName?.Trim() ?? "General");
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryGenerateContentAsync(object body, string apiKey, CancellationToken cancellationToken, bool throwOnFailure)
    {
        await GeminiThrottle.WaitAsync(cancellationToken);
        try
        {
            return await TryGenerateContentCoreAsync(body, apiKey, cancellationToken, throwOnFailure);
        }
        finally
        {
            GeminiThrottle.Release();
        }
    }

    private async Task<string?> TryGenerateContentCoreAsync(object body, string apiKey, CancellationToken cancellationToken, bool throwOnFailure)
    {
        var preferredModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        var models = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredModel))
        {
            models.Add(preferredModel);
        }

        models.Add("gemini-2.5-flash");
        models.Add("gemini-2.0-flash");
        models.Add("gemini-1.5-flash");

        var discovered = await ListGenerateContentModelsAsync(apiKey, cancellationToken);
        models.AddRange(discovered);

        var attemptedErrors = new List<string>();

        foreach (var model in models.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalizedModel = NormalizeModelName(model);
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{normalizedModel}:generateContent?key={apiKey}";
            var response = await _httpClient.PostAsJsonAsync(endpoint, body, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return content;
            }

            attemptedErrors.Add($"Model={normalizedModel}, Status={(int)response.StatusCode}, Body={Truncate(content, 300)}");
        }

        if (throwOnFailure)
        {
            throw new InvalidOperationException($"Gemini API request failed. Attempts: {string.Join(" | ", attemptedErrors)}");
        }

        return null;
    }

    private async Task<List<string>> ListGenerateContentModelsAsync(string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("models", out var modelsElement))
            {
                return [];
            }

            var discovered = new List<string>();
            foreach (var modelEl in modelsElement.EnumerateArray())
            {
                var supportsGenerate = false;
                if (modelEl.TryGetProperty("supportedGenerationMethods", out var methods))
                {
                    supportsGenerate = methods.EnumerateArray()
                        .Any(m => string.Equals(m.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase));
                }

                if (!supportsGenerate || !modelEl.TryGetProperty("name", out var nameEl))
                {
                    continue;
                }

                var name = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var normalized = NormalizeModelName(name);
                if (normalized.Contains("flash", StringComparison.OrdinalIgnoreCase))
                {
                    discovered.Add(normalized);
                }
            }

            return discovered;
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeModelName(string model)
    {
        var trimmed = model.Trim();
        return trimmed.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? trimmed["models/".Length..]
            : trimmed;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }

    private static string BuildPrompt(string rawText, OcrScanResult baseline)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Extract grocery product info from images and OCR text.");
        sb.AppendLine("Return JSON only with fields:");
        sb.AppendLine("productName, detectedItems (array of {name, category, expiryDate}), categoryName, manufacturingDate, expiryDate, bestBeforeText, bestBeforeValue, bestBeforeUnit, confidence, fieldConfidence ({name, expiry, category} each high|medium|low)");
        sb.AppendLine("Rules:");
        sb.AppendLine("- Handle difficult images: blurred text, glare, rotated labels, handwriting. Try all orientations (0, 90, 180, 270).");
        sb.AppendLine("- Look for date stamps, embossed/ink-jet-printed dates, and sticker labels separate from main label text.");
        sb.AppendLine("- Use categories: Vegetables, Fruits, Bakery Item, Snacks, Dairy, Meat, Grains, Beverages, Condiments, Frozen, General.");
        sb.AppendLine("- If there are MULTIPLE distinct products, you MUST list each in detectedItems with its own name, category, and expiryDate.");
        sb.AppendLine("- Each detectedItems entry MUST be a DISTINCT physical product. Do NOT split ingredients or label fragments into separate items.");
        sb.AppendLine("- Merge duplicate products across images into ONE entry.");
        sb.AppendLine("- If there is no clear product label text, detect visible food items and populate detectedItems.");
        sb.AppendLine("- Dates must be YYYY-MM-DD or null.");
        sb.AppendLine("- If text says 'best before X months/years/days from manufacture', compute expiryDate from manufacturingDate and also copy the raw phrasing into bestBeforeText.");
        sb.AppendLine("- confidence is one of: high, medium, low.");
        sb.AppendLine("- fieldConfidence: per-field - high if clearly read, medium if partially readable, low if inferred or guessed.");
        sb.AppendLine("Baseline extraction:");
        sb.AppendLine($"productName={baseline.ProductName}, categoryName={baseline.CategoryName}, manufacturingDate={baseline.ManufacturingDate}, expiryDate={baseline.ExpiryDate}");
        sb.AppendLine("OCR text:");
        sb.AppendLine(rawText);
        return sb.ToString();
    }

    private static string? ExtractModelJson(string responseContent)
    {
        using var doc = JsonDocument.Parse(responseContent);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        var text = string.Join(
            "\n",
            parts.EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text[start..(end + 1)];
            }
        }

        return text;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace('|', '/');

        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "dd-MM-yyyy", "dd/MM/yyyy", "dd.MM.yyyy",
            "MM-dd-yyyy", "MM/dd/yyyy", "MM.dd.yyyy",
            "d-M-yyyy", "d/M/yyyy", "d.M.yyyy",
            "dd-MM-yy", "dd/MM/yy", "dd.MM.yy",
            "MM-dd-yy", "MM/dd/yy", "MM.dd.yy",
            "d-M-yy", "d/M/yy", "d.M.yy",
            "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd MMMM yyyy",
            "d MMM yy", "dd MMM yy", "d MMMM yy", "dd MMMM yy",
            "MMM yyyy", "MMMM yyyy"
        };

        if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var candidate = new DateOnly(dt.Year, dt.Month, Math.Max(1, dt.Day));
            return IsReasonableDate(candidate) ? candidate : null;
        }

        if (DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return IsReasonableDate(date) ? date : null;
        }

        var slashLike = Regex.Match(normalized, @"^(?<a>\d{1,2})[/-](?<b>\d{1,2})[/-](?<y>\d{2,4})$");
        if (slashLike.Success)
        {
            var a = int.Parse(slashLike.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(slashLike.Groups["b"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(slashLike.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (year < 100)
            {
                year += 2000;
            }

            // Apply same DD/MM vs MM/DD disambiguation as regex service
            if (a > 12 && b is >= 1 and <= 12)
            {
                var maxDay = DateTime.DaysInMonth(year, b);
                return new DateOnly(year, b, Math.Clamp(a, 1, maxDay));
            }

            if (b > 12 && a is >= 1 and <= 12)
            {
                var maxDay = DateTime.DaysInMonth(year, a);
                return new DateOnly(year, a, Math.Clamp(b, 1, maxDay));
            }

            if (a is >= 1 and <= 12 && b is >= 1 and <= 12)
            {
                // Prefer DD/MM
                var maxDay = DateTime.DaysInMonth(year, b);
                return new DateOnly(year, b, Math.Clamp(a, 1, maxDay));
            }
        }

        return null;
    }

    private static bool IsReasonableDate(DateOnly date)
    {
        return date.Year is >= 2000 and <= 2100;
    }

    private static DateOnly? TryDeriveExpiryFromBestBefore(DateOnly? mfgDate, int? bestBeforeValue, string? bestBeforeUnit, string? expiryText)
    {
        var baseDate = mfgDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var value = bestBeforeValue;
        var unit = bestBeforeUnit;

        if ((!value.HasValue || value <= 0 || string.IsNullOrWhiteSpace(unit)) && !string.IsNullOrWhiteSpace(expiryText))
        {
            var m = Regex.Match(
                expiryText,
                @"(?:best\s*(?:if\s+used\s+)?before|use\s*(?:with)?in|consume\s*(?:with)?in|shelf\s*life\s*(?:of|is|:)?|has\s+a\s+shelf\s+life\s+of|valid\s*(?:for|upto)|good\s*for|keeps?\s*(?:for|up\s*to)|stays?\s*fresh\s*(?:for)?|lasts?\s*(?:for)?|not\s+to\s+be\s+used\s+after)\s*(?:within\s*)?(\d{1,3}(?:[½¾]|\s+\d\s*/\s*\d)?(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|eighteen|twenty\s*four|thirty)\s*(day|days|week|weeks|month|months|year|years|yr|yrs|hrs|hours)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                value = (int?)ParseWordOrNumber(m.Groups[1].Value);
                unit = m.Groups[2].Value;
            }
        }

        if (!value.HasValue || value <= 0 || string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return unit.Trim().ToLowerInvariant() switch
        {
            "day" or "days" => baseDate.AddDays(value.Value),
            "week" or "weeks" => baseDate.AddDays(value.Value * 7),
            "month" or "months" => baseDate.AddMonths(value.Value),
            "year" or "years" or "yr" or "yrs" => baseDate.AddYears(value.Value),
            "hrs" or "hours" => baseDate.AddDays(Math.Max(1, value.Value / 24)),
            _ => null
        };
    }

    private static DateOnly? TryDeriveExpiryFromBestBeforeText(string bestBeforeText, DateOnly? mfgDate)
    {
        var baseDate = mfgDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // "best before end of <month> <year>"
        var endOfMatch = Regex.Match(bestBeforeText,
            @"(?:best\s*before\s*)?(?:the\s*)?end\s*(?:of)?\s*(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)\w*\s*(\d{2,4})",
            RegexOptions.IgnoreCase);
        if (endOfMatch.Success)
        {
            var monthNum = ParseMonthAbbr(endOfMatch.Groups[1].Value);
            if (monthNum > 0 && int.TryParse(endOfMatch.Groups[2].Value, out var year))
            {
                if (year < 100) year += 2000;
                if (year is >= 2000 and <= 2100)
                {
                    return new DateOnly(year, monthNum, DateTime.DaysInMonth(year, monthNum));
                }
            }
        }

        // General duration pattern
        var m = Regex.Match(bestBeforeText,
            @"(\d{1,3}(?:[½¾]|\s+\d\s*/\s*\d)?(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|eighteen|twenty\s*four|thirty)\s*(day|days|week|weeks|month|months|mon|mons|year|years|yr|yrs|hrs|hours)",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var value = ParseWordOrNumber(m.Groups[1].Value);
            if (value.HasValue && value > 0)
            {
                var unit = m.Groups[2].Value.Trim().ToLowerInvariant();
                return unit switch
                {
                    "day" or "days" => baseDate.AddDays((int)value.Value),
                    "week" or "weeks" => baseDate.AddDays((int)(value.Value * 7)),
                    "month" or "months" or "mon" or "mons" => baseDate.AddMonths((int)value.Value),
                    "year" or "years" or "yr" or "yrs" => baseDate.AddYears((int)value.Value),
                    "hrs" or "hours" => baseDate.AddDays(Math.Max(1, (int)(value.Value / 24))),
                    _ => null
                };
            }
        }

        return null;
    }

    private static int ParseMonthAbbr(string token) => token.Trim().ToLowerInvariant() switch
    {
        "jan" or "january" => 1, "feb" or "february" => 2, "mar" or "march" => 3, "apr" or "april" => 4,
        "may" => 5, "jun" or "june" => 6, "jul" or "july" => 7, "aug" or "august" => 8,
        "sep" or "sept" or "september" => 9, "oct" or "october" => 10, "nov" or "november" => 11, "dec" or "december" => 12,
        _ => 0
    };

    private static double? ParseWordOrNumber(string token)
    {
        var t = token.Trim();
        t = t.Replace("½", ".5").Replace("¾", ".75").Replace("⅓", ".33").Replace("⅔", ".67");

        var fractionMatch = Regex.Match(t, @"^(\d+)\s+(\d+)\s*/\s*(\d+)$");
        if (fractionMatch.Success)
        {
            var whole = double.Parse(fractionMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var num = double.Parse(fractionMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var den = double.Parse(fractionMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            return den > 0 ? whole + num / den : null;
        }

        if (double.TryParse(t, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }

        return t.ToLowerInvariant() switch
        {
            "one" => 1, "two" => 2, "three" => 3, "four" => 4, "five" => 5, "six" => 6,
            "seven" => 7, "eight" => 8, "nine" => 9, "ten" => 10, "eleven" => 11, "twelve" => 12,
            "thirteen" => 13, "fourteen" => 14, "fifteen" => 15, "sixteen" => 16, "seventeen" => 17,
            "eighteen" => 18, "nineteen" => 19, "twenty" => 20,
            "twenty one" or "twentyone" => 21, "twenty two" or "twentytwo" => 22,
            "twenty three" or "twentythree" => 23, "twenty four" or "twentyfour" => 24,
            "thirty" => 30, "forty five" or "fortyfive" => 45, "sixty" => 60, "ninety" => 90,
            _ => null
        };
    }

    private static (int Score, FieldConfidenceDto FieldConf, bool NeedsReview) ComputeGeminiConfidenceDetailed(
        string? confidenceStr, GeminiFieldConfidence? geminiFieldConf, bool hasExpiry, bool hasProductName, int itemCount)
    {
        var score = 0;
        switch (confidenceStr?.ToLowerInvariant())
        {
            case "high": score += 40; break;
            case "medium": score += 25; break;
            default: score += 10; break;
        }
        if (hasExpiry) score += 30;
        else score -= 10; // Active penalty for missing expiry
        if (hasProductName) score += 20;
        if (itemCount > 0) score += 10;
        score = Math.Clamp(score, 0, 100);

        var nameConf = geminiFieldConf?.Name?.ToLowerInvariant() ?? (hasProductName ? "medium" : "low");
        var expiryConf = geminiFieldConf?.Expiry?.ToLowerInvariant() ?? (hasExpiry ? "medium" : "low");
        var categoryConf = geminiFieldConf?.Category?.ToLowerInvariant() ?? "medium";

        var fieldConfidence = new FieldConfidenceDto(nameConf, expiryConf, categoryConf);
        var needsReview = nameConf == "low" || expiryConf == "low" || categoryConf == "low" || score < 50;

        return (score, fieldConfidence, needsReview);
    }

    public sealed class GeminiOcrResponse
    {
        public string? ProductName { get; set; }

        [JsonPropertyName("detectedItems")]
        public GeminiDetectedItemDetail[]? DetectedItemDetails { get; set; }

        public string? CategoryName { get; set; }
        public string? ManufacturingDate { get; set; }
        public string? ExpiryDate { get; set; }
        public string? BestBeforeText { get; set; }
        public int? BestBeforeValue { get; set; }
        public string? BestBeforeUnit { get; set; }
        public string? Confidence { get; set; }
        public GeminiFieldConfidence? FieldConfidence { get; set; }
    }

    public sealed class GeminiDetectedItemDetail
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? ExpiryDate { get; set; }
    }

    public sealed class GeminiFieldConfidence
    {
        public string? Name { get; set; }
        public string? Expiry { get; set; }
        public string? Category { get; set; }
    }

    private static IReadOnlyList<string> NormalizeDetectedItemNames(GeminiDetectedItemDetail[]? items, string? productName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (items is not null)
        {
            foreach (var item in items)
            {
                AddTokenized(item.Name, set);
            }
        }

        AddTokenized(productName, set);

        // Remove near-duplicates: if one name is a substring of another, keep the shorter canonical name
        var list = set.ToList();
        var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];
                if (a.Contains(b, StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(a); // Remove the longer one
                else if (b.Contains(a, StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(b);
            }
        }

        return list.Where(n => !toRemove.Contains(n)).Take(12).ToArray();
    }

    private static void AddTokenized(string? value, HashSet<string> set)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var blocked = new Regex(@"(unknown product|best before|exp|expiry|mfg|date)", RegexOptions.IgnoreCase);
        foreach (var raw in value.Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = raw.Trim();
            if (candidate.Length < 2 || candidate.Length > 60)
            {
                continue;
            }

            if (blocked.IsMatch(candidate))
            {
                continue;
            }

            set.Add(candidate);
        }
    }

    private static IReadOnlyList<string> MergeCandidates(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (left is not null)
        {
            foreach (var l in left)
            {
                if (!string.IsNullOrWhiteSpace(l))
                {
                    set.Add(l.Trim());
                }
            }
        }

        if (right is not null)
        {
            foreach (var r in right)
            {
                if (!string.IsNullOrWhiteSpace(r))
                {
                    set.Add(r.Trim());
                }
            }
        }

        return set.Take(12).ToArray();
    }

    private static string InferCategoryFromItems(IReadOnlyList<string> items)
    {
        var text = string.Join(' ', items).ToLowerInvariant();

        if (Regex.IsMatch(text, @"\b(tomato|onion|potato|carrot|cucumber|broccoli|spinach|capsicum|brinjal|eggplant|lettuce|cauliflower)\b"))
            return "Vegetables";
        if (Regex.IsMatch(text, @"\b(apple|banana|orange|grape|mango|pear|papaya|pomegranate|kiwi)\b"))
            return "Fruits";
        if (Regex.IsMatch(text, @"\b(milk|cheese|butter|yogurt|yoghurt|cream|paneer|curd)\b"))
            return "Dairy";
        if (Regex.IsMatch(text, @"\b(chicken|beef|fish|mutton|pork|prawn|shrimp|meat)\b"))
            return "Meat";
        if (Regex.IsMatch(text, @"\b(bread|cake|bun|pastry|croissant)\b"))
            return "Bakery Item";
        if (Regex.IsMatch(text, @"\b(biscuit|cookie|chocolate|chips|wafer|namkeen|snack)\b"))
            return "Snacks";
        if (Regex.IsMatch(text, @"\b(rice|pasta|noodle|oats|cereal|wheat|flour|atta)\b"))
            return "Grains";
        if (Regex.IsMatch(text, @"\b(juice|soda|water|tea|coffee|drink)\b"))
            return "Beverages";

        return "General";
    }
}
