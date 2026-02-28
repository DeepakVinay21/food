using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FoodExpirationTracker.Infrastructure.Ocr;

public class RegexOcrService : IOcrService
{
    private static readonly HttpClient SharedHttpClient = new();
    private static readonly Regex DatePattern = new(@"\b(\d{1,2}[./|\-]\d{1,2}[./|\-]\d{2,4}|\d{4}[./|\-]\d{1,2}[./|\-]\d{1,2})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TextualDatePattern = new(@"\b(\d{1,2}\s*(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s*\d{2,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MonthYearDatePattern = new(@"\b((jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*[.\s/-]*\d{2,4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ExpiryLabelPattern = new(@"(?:exp(?:iry|ire)?|expires?|best\s*(?:if\s+used\s+)?before|use\s*by|bb|use\s*before|consume\s*before)\s*[:\-]?\s*(\d{1,2}[./|\-]\d{1,2}[./|\-]\d{2,4}|\d{4}[./|\-]\d{1,2}[./|\-]\d{1,2}|\d{1,2}\s*(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s*\d{2,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MfgLabelPattern = new(@"(?:mfg|mfd|mfg\.?\s*date|manufactured|manufacturing|packed\s*on|pkd|pkg\.?\s*date|pack(?:ed|ing)\s*date|prod(?:uction)?\s*date)\s*[:\-]?\s*(\d{1,2}[./|\-]\d{1,2}[./|\-]\d{2,4}|\d{4}[./|\-]\d{1,2}[./|\-]\d{1,2}|\d{1,2}\s*(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s*\d{2,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Expanded duration pattern: covers many real-world phrasings
    private static readonly Regex BestBeforeDurationPattern = new(
        @"(?:best\s*(?:if\s+used\s+)?before|use\s*(?:with)?in|consume\s*(?:with)?in|shelf\s*life\s*(?:of|is|:)?|has\s+a\s+shelf\s+life\s+of|valid\s*(?:for|upto)|good\s*for|keeps?\s*(?:for|up\s*to)|store\s*(?:for|up\s*to)|stays?\s*fresh\s*(?:for|up\s*to)?|lasts?\s*(?:for|up\s*to)?|not\s+to\s+be\s+used\s+after|expir(?:y|es?)\s*(?:in)?)\s*(?:within\s*|up\s*to\s*)?(\d{1,3}(?:[½¾]|\s+\d\s*/\s*\d)?(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|twenty\s*one|twenty\s*two|twenty\s*three|twenty\s*four|thirty|forty\s*five|sixty|ninety)\s*(week|weeks|month|months|mon|mons|day|days|year|years|yr|yrs|hrs|hours)(?:\s*(?:to|-)\s*\d{1,3}\s*\2)?\s*(?:from|after|of|since|post)?\s*(?:mfg|mfd|manufacture|manufacturing|packed|packaging|packing|pkg|pkd|production|opening|date\s*of\s*(?:mfg|manufacture|packing|packaging|production))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Year-only expiry pattern: "expires 2027", "use by 2027"
    private static readonly Regex YearOnlyExpiryPattern = new(
        @"(?:exp(?:iry|ire)?|expires?|best\s*before|use\s*by)\s*[:\-]?\s*(20[2-9]\d)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern: "best before end of <month> <year>"
    private static readonly Regex BestBeforeEndOfPattern = new(
        @"best\s*before\s*(?:the\s*)?end\s*(?:of)?\s*(jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\s*(\d{2,4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public RegexOcrService()
    {
    }

    public OcrScanResult ParseText(string rawText, byte[]? imageBytes = null)
    {
        var normalizedText = NormalizeOcrText(rawText);
        var lower = normalizedText.ToLowerInvariant();

        var productName = GuessProductName(normalizedText);
        var category = GuessCategory(lower);

        var modelPrediction = imageBytes is not null ? PredictProductFromImage(imageBytes) : null;

        if (productName == "Unknown Product" && modelPrediction is not null)
        {
            productName = modelPrediction.Value.ProductName;
        }

        if ((category == "General" || (productName == "Unknown Product" && category == "Produce")) && modelPrediction is not null)
        {
            category = modelPrediction.Value.CategoryName;
        }

        var allDates = ExtractDates(normalizedText);
        var expiryByLabel = ExtractLabeledDate(ExpiryLabelPattern, normalizedText, preferFuture: true);
        var mfgByLabel = ExtractLabeledDate(MfgLabelPattern, normalizedText);
        var manufacturingDate = mfgByLabel ?? (allDates.Count > 1 ? allDates.Min() : default);
        var expiryDate = expiryByLabel
            ?? (allDates.Count > 1 ? allDates.Max() : allDates.FirstOrDefault());

        // Try "best before end of <month> <year>" pattern
        if (expiryDate == default)
        {
            expiryDate = TryParseBestBeforeEndOf(normalizedText) ?? default;
        }

        // Try year-only expiry: "expires 2027" → Dec 31, 2027
        if (expiryDate == default)
        {
            var yearMatch = YearOnlyExpiryPattern.Match(normalizedText);
            if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var expiryYear)
                && expiryYear is >= 2020 and <= 2100)
            {
                expiryDate = new DateOnly(expiryYear, 12, 31);
            }
        }

        if (expiryDate == default && manufacturingDate != default)
        {
            var derived = DeriveExpiryFromBestBefore(normalizedText, manufacturingDate);
            if (derived.HasValue)
            {
                expiryDate = derived.Value;
            }
        }

        // Scenario: "best before three months from packaging" with missing mfg/pack date.
        if (expiryDate == default && manufacturingDate == default)
        {
            var fallbackMfg = DateOnly.FromDateTime(DateTime.UtcNow);
            var derived = DeriveExpiryFromBestBefore(normalizedText, fallbackMfg);
            if (derived.HasValue)
            {
                manufacturingDate = fallbackMfg;
                expiryDate = derived.Value;
            }
        }

        var hasDateEvidence = allDates.Count > 0 || expiryByLabel.HasValue || mfgByLabel.HasValue;

        // Smart category-based fallback with product name awareness
        if (expiryDate == default)
        {
            expiryDate = GetCategoryFallbackExpiry(category, productName);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysLeft = expiryDate.DayNumber - today.DayNumber;

        // Detailed confidence scoring with per-field confidence
        var (confidenceScore, fieldConfidence, needsReview) = ComputeDetailedConfidence(
            productName, hasDateEvidence, expiryByLabel.HasValue, modelPrediction is not null, allDates.Count, category);
        var lowConfidence = confidenceScore < 50;

        return new OcrScanResult(productName, manufacturingDate == default ? null : manufacturingDate,
            expiryDate, daysLeft, category, lowConfidence, null, confidenceScore, fieldConfidence, needsReview);
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var paddleText = await TryExtractWithPaddleOcrAsync(imageBytes, cancellationToken);
        if (!string.IsNullOrWhiteSpace(paddleText))
        {
            return paddleText;
        }

        var originalPath = Path.Combine(Path.GetTempPath(), $"ocr_original_{Guid.NewGuid():N}.jpg");
        var enhancedPath = Path.Combine(Path.GetTempPath(), $"ocr_enhanced_{Guid.NewGuid():N}.png");
        var thresholdPath = Path.Combine(Path.GetTempPath(), $"ocr_threshold_{Guid.NewGuid():N}.png");
        var aggressivePath = Path.Combine(Path.GetTempPath(), $"ocr_aggressive_{Guid.NewGuid():N}.png");

        try
        {
            await File.WriteAllBytesAsync(originalPath, imageBytes, cancellationToken);

            await using (var inputStream = new MemoryStream(imageBytes))
            using (var image = await Image.LoadAsync<Rgb24>(inputStream, cancellationToken))
            {
                image.Mutate(ctx =>
                {
                    ctx.AutoOrient();
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(Math.Max(1800, image.Width), Math.Max(1200, image.Height)),
                        Mode = ResizeMode.Max
                    });
                    ctx.Grayscale();
                    ctx.Contrast(1.35f);
                    ctx.GaussianSharpen(1.5f);
                });

                await image.SaveAsPngAsync(enhancedPath, cancellationToken);
            }

            await using (var inputStream = new MemoryStream(imageBytes))
            using (var thresholdImage = await Image.LoadAsync<Rgb24>(inputStream, cancellationToken))
            {
                thresholdImage.Mutate(ctx =>
                {
                    ctx.AutoOrient();
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(Math.Max(1800, thresholdImage.Width), Math.Max(1200, thresholdImage.Height)),
                        Mode = ResizeMode.Max
                    });
                    ctx.Grayscale();
                    ctx.Contrast(1.8f);
                    ctx.BinaryThreshold(0.56f);
                });

                await thresholdImage.SaveAsPngAsync(thresholdPath, cancellationToken);
            }

            // Aggressive threshold for light text on light backgrounds
            await using (var inputStream3 = new MemoryStream(imageBytes))
            using (var aggressiveImage = await Image.LoadAsync<Rgb24>(inputStream3, cancellationToken))
            {
                aggressiveImage.Mutate(ctx =>
                {
                    ctx.AutoOrient();
                    ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(Math.Max(1800, aggressiveImage.Width), Math.Max(1200, aggressiveImage.Height)),
                        Mode = ResizeMode.Max
                    });
                    ctx.Grayscale();
                    ctx.Contrast(2.0f);
                    ctx.BinaryThreshold(0.45f);
                });

                await aggressiveImage.SaveAsPngAsync(aggressivePath, cancellationToken);
            }

            var primary = await RunTesseractAsync(enhancedPath, "6", cancellationToken);
            var secondary = await RunTesseractAsync(enhancedPath, "11", cancellationToken);
            var thresholdPrimary = await RunTesseractAsync(thresholdPath, "6", cancellationToken);
            var thresholdSecondary = await RunTesseractAsync(thresholdPath, "11", cancellationToken);
            // Additional PSM modes for different label layouts
            var columnMode = await RunTesseractAsync(enhancedPath, "4", cancellationToken);
            var autoMode = await RunTesseractAsync(aggressivePath, "3", cancellationToken);

            return $"{primary}\n{secondary}\n{thresholdPrimary}\n{thresholdSecondary}\n{columnMode}\n{autoMode}".Trim();
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("Tesseract is not installed or not available in PATH.");
        }
        finally
        {
            if (File.Exists(originalPath)) File.Delete(originalPath);
            if (File.Exists(enhancedPath)) File.Delete(enhancedPath);
            if (File.Exists(thresholdPath)) File.Delete(thresholdPath);
            if (File.Exists(aggressivePath)) File.Delete(aggressivePath);
        }
    }

    private async Task<string> RunTesseractAsync(string imagePath, string psm, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tesseract",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("stdout");
        psi.ArgumentList.Add("--oem");
        psi.ArgumentList.Add("3");
        psi.ArgumentList.Add("--psm");
        psi.ArgumentList.Add(psm);
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add("eng");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("preserve_interword_spaces=1");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("user_defined_dpi=300");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Unable to start tesseract process.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Tesseract failed: {error}");
        }

        return output;
    }

    private static async Task<string?> TryExtractWithPaddleOcrAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        var endpoint = Environment.GetEnvironmentVariable("PADDLE_OCR_URL");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:8090/ocr/extract";
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "image", "scan.jpg");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("text", out var textElement))
            {
                return null;
            }

            var text = textElement.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static (string ProductName, string CategoryName)? PredictProductFromImage(byte[] imageBytes)
    {
        // ONNX model removed to reduce memory usage on free-tier hosting
        return null;
    }

    private static (string ProductName, string CategoryName)? MapLabelToProduct(string label)
    {
        if (label.Contains("milk") || label.Contains("carton")) return ("Milk", "Dairy");
        if (label.Contains("cheese") || label.Contains("yogurt") || label.Contains("butter") || label.Contains("cream")) return ("Dairy Product", "Dairy");
        if (label.Contains("egg")) return ("Eggs", "Dairy");
        if (label.Contains("bread") || label.Contains("bagel") || label.Contains("loaf") || label.Contains("toast")) return ("Bread", "Bakery Item");
        if (label.Contains("cake") || label.Contains("croissant") || label.Contains("muffin") || label.Contains("pretzel") || label.Contains("dough")) return ("Bakery Product", "Bakery Item");
        if (label.Contains("tomato")) return ("Tomato", "Vegetables");
        if (label.Contains("onion")) return ("Onion", "Vegetables");
        if (label.Contains("potato")) return ("Potato", "Vegetables");
        if (label.Contains("carrot")) return ("Carrot", "Vegetables");
        if (label.Contains("broccoli")) return ("Broccoli", "Vegetables");
        if (label.Contains("cucumber")) return ("Cucumber", "Vegetables");
        if (label.Contains("pepper") || label.Contains("capsicum")) return ("Pepper", "Vegetables");
        if (label.Contains("cauliflower")) return ("Cauliflower", "Vegetables");
        if (label.Contains("mushroom")) return ("Mushroom", "Vegetables");
        if (label.Contains("cabbage")) return ("Cabbage", "Vegetables");
        if (label.Contains("lettuce")) return ("Lettuce", "Vegetables");
        if (label.Contains("banana")) return ("Banana", "Fruits");
        if (label.Contains("apple")) return ("Apple", "Fruits");
        if (label.Contains("orange")) return ("Orange", "Fruits");
        if (label.Contains("lemon")) return ("Lemon", "Fruits");
        if (label.Contains("strawberry")) return ("Strawberry", "Fruits");
        if (label.Contains("pineapple")) return ("Pineapple", "Fruits");
        if (label.Contains("grape")) return ("Grapes", "Fruits");
        if (label.Contains("watermelon") || label.Contains("melon")) return ("Melon", "Fruits");
        if (label.Contains("peach")) return ("Peach", "Fruits");
        if (label.Contains("pear")) return ("Pear", "Fruits");
        if (label.Contains("mango")) return ("Mango", "Fruits");
        if (label.Contains("coconut")) return ("Coconut", "Fruits");
        if (label.Contains("chocolate")) return ("Chocolate", "Snacks");
        if (label.Contains("biscuit") || label.Contains("cookie") || label.Contains("cracker")) return ("Biscuit", "Snacks");
        if (label.Contains("chips") || label.Contains("crisp")) return ("Chips", "Snacks");
        if (label.Contains("candy") || label.Contains("toffee") || label.Contains("gummy")) return ("Candy", "Snacks");
        if (label.Contains("ice cream") || label.Contains("popsicle")) return ("Ice Cream", "Frozen");
        if (label.Contains("chicken") || label.Contains("hen")) return ("Chicken", "Meat");
        if (label.Contains("beef") || label.Contains("steak")) return ("Beef", "Meat");
        if (label.Contains("fish") || label.Contains("tuna") || label.Contains("salmon")) return ("Fish", "Meat");
        if (label.Contains("pork") || label.Contains("ham") || label.Contains("bacon") || label.Contains("sausage")) return ("Pork", "Meat");
        if (label.Contains("meat") || label.Contains("lamb") || label.Contains("mutton")) return ("Meat Product", "Meat");
        if (label.Contains("shrimp") || label.Contains("prawn") || label.Contains("lobster")) return ("Seafood", "Meat");
        if (label.Contains("rice")) return ("Rice", "Grains");
        if (label.Contains("pasta") || label.Contains("spaghetti") || label.Contains("noodle")) return ("Pasta", "Grains");
        if (label.Contains("cereal") || label.Contains("oat") || label.Contains("granola")) return ("Cereal", "Grains");
        if (label.Contains("juice")) return ("Juice", "Beverages");
        if (label.Contains("soda") || label.Contains("cola")) return ("Soda", "Beverages");
        if (label.Contains("water") || label.Contains("bottle")) return ("Water", "Beverages");
        if (label.Contains("coffee")) return ("Coffee", "Beverages");
        if (label.Contains("tea")) return ("Tea", "Beverages");
        if (label.Contains("sauce") || label.Contains("ketchup") || label.Contains("mustard") || label.Contains("mayonnaise")) return ("Condiment", "Condiments");
        if (label.Contains("honey")) return ("Honey", "Condiments");
        if (label.Contains("jam") || label.Contains("jelly")) return ("Jam", "Condiments");

        return null;
    }

    private static DateOnly? ExtractLabeledDate(Regex pattern, string text, bool preferFuture = false)
    {
        var match = pattern.Match(text);
        if (!match.Success) return null;
        return preferFuture ? ParseDatePreferFuture(match.Groups[1].Value) : ParseDate(match.Groups[1].Value);
    }

    /// <summary>
    /// Parses a date string, preferring an interpretation that gives a future date (for expiry labels).
    /// </summary>
    private static DateOnly? ParseDatePreferFuture(string value)
    {
        value = value.Trim().Replace('|', '/');
        value = Regex.Replace(value, @"(?<=\d)\.(?=\d)", "/");
        value = value.Replace('.', ' ');

        var slashLike = Regex.Match(value, @"^(?<a>\d{1,2})[/-](?<b>\d{1,2})[/-](?<y>\d{2,4})$");
        if (slashLike.Success)
        {
            var a = int.Parse(slashLike.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(slashLike.Groups["b"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(slashLike.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (year < 100) year += year > 70 ? 1900 : 2000;
            if (a > 31 || b > 31) return null;
            return DisambiguateDayMonthPreferFuture(a, b, year);
        }

        // Fall back to standard parsing for non-ambiguous formats
        return ParseDate(value);
    }

    private static List<DateOnly> ExtractDates(string text)
    {
        var results = new List<DateOnly>();

        foreach (Match match in DatePattern.Matches(text))
        {
            var parsed = ParseDate(match.Value);
            if (parsed.HasValue) results.Add(parsed.Value);
        }

        foreach (Match match in TextualDatePattern.Matches(text))
        {
            var parsed = ParseDate(match.Value);
            if (parsed.HasValue) results.Add(parsed.Value);
        }

        foreach (Match match in MonthYearDatePattern.Matches(text))
        {
            var parsed = ParseDate(match.Value);
            if (parsed.HasValue) results.Add(parsed.Value);
        }

        return results.Distinct().ToList();
    }

    private static DateOnly? ParseDate(string value)
    {
        value = value.Trim();
        value = value.Replace('|', '/');

        // Only convert dots between digit sequences (not textual dates like "Mar.2026")
        value = Regex.Replace(value, @"(?<=\d)\.(?=\d)", "/");
        value = value.Replace('.', ' ');

        // Prioritize ISO YYYY-MM-DD first to avoid ambiguity
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-M-d", "yyyy/MM/d",
            "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy",
            "dd-MM-yyyy", "d-M-yyyy", "MM-dd-yyyy", "M-d-yyyy",
            "dd/MM/yy", "d/M/yy", "MM/dd/yy", "M/d/yy",
            "dd-MM-yy", "d-M-yy", "MM-dd-yy", "M-d-yy",
            "d MMM yyyy", "dd MMM yyyy", "d MMM yy", "dd MMM yy",
            "d MMMM yyyy", "dd MMMM yyyy", "d MMMM yy", "dd MMMM yy",
            "MMM yyyy", "MMMM yyyy", "MMM yy", "MMMM yy",
            "MMM-yyyy", "MMMM-yyyy", "MMM-yy", "MMMM-yy",
            "MMM/yyyy", "MMMM/yyyy", "MMM/yy", "MMMM/yy"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            var candidate = new DateOnly(dt.Year, dt.Month, Math.Max(1, dt.Day));
            return IsReasonableDate(candidate) ? candidate : null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            var candidate = DateOnly.FromDateTime(dt);
            return IsReasonableDate(candidate) ? candidate : null;
        }

        var slashLike = Regex.Match(value, @"^(?<a>\d{1,2})[/-](?<b>\d{1,2})[/-](?<y>\d{2,4})$");
        if (slashLike.Success)
        {
            var a = int.Parse(slashLike.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(slashLike.Groups["b"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(slashLike.Groups["y"].Value, CultureInfo.InvariantCulture);

            // Smart two-digit year disambiguation
            if (year < 100)
            {
                year += year > 70 ? 1900 : 2000;
            }

            // Reject clearly invalid values
            if (a > 31 || b > 31) return null;

            return DisambiguateDayMonth(a, b, year);
        }

        return null;
    }

    private static DateOnly? DisambiguateDayMonth(int a, int b, int year)
    {
        if (!IsReasonableDate(new DateOnly(Math.Clamp(year, 2000, 2100), 1, 1))) return null;

        if (a > 12 && b is >= 1 and <= 12)
        {
            var maxDay = DateTime.DaysInMonth(year, b);
            var day = Math.Clamp(a, 1, maxDay);
            var candidate = new DateOnly(year, b, day);
            return IsReasonableDate(candidate) ? candidate : null;
        }

        if (b > 12 && a is >= 1 and <= 12)
        {
            var maxDay = DateTime.DaysInMonth(year, a);
            var day = Math.Clamp(b, 1, maxDay);
            var candidate = new DateOnly(year, a, day);
            return IsReasonableDate(candidate) ? candidate : null;
        }

        if (a is >= 1 and <= 12 && b is >= 1 and <= 12)
        {
            var maxDay = DateTime.DaysInMonth(year, b);
            var day = Math.Clamp(a, 1, maxDay);
            var candidate = new DateOnly(year, b, day);
            return IsReasonableDate(candidate) ? candidate : null;
        }

        if (b is >= 1 and <= 12)
        {
            var maxDay = DateTime.DaysInMonth(year, b);
            var day = Math.Clamp(a, 1, maxDay);
            var candidate = new DateOnly(year, b, day);
            return IsReasonableDate(candidate) ? candidate : null;
        }

        return null;
    }

    /// <summary>
    /// For expiry-labeled dates, prefer the interpretation that gives a future date.
    /// Falls back to standard DD/MM preference when both interpretations are either past or future.
    /// </summary>
    private static DateOnly? DisambiguateDayMonthPreferFuture(int a, int b, int year)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ddMm = DisambiguateDayMonth(a, b, year);

        // Only attempt swapped interpretation if both values could be either day or month
        if (a is >= 1 and <= 12 && b is >= 1 and <= 12 && a != b)
        {
            var mmDd = TryBuildDate(year, a, b);
            if (ddMm.HasValue && mmDd.HasValue)
            {
                // If DD/MM is past but MM/DD is future, prefer future for expiry
                if (ddMm.Value < today && mmDd.Value >= today)
                    return mmDd;
            }
            else if (!ddMm.HasValue && mmDd.HasValue)
            {
                return mmDd;
            }
        }

        return ddMm;
    }

    private static DateOnly? TryBuildDate(int year, int month, int day)
    {
        if (month < 1 || month > 12) return null;
        var maxDay = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > maxDay) return null;
        var d = new DateOnly(year, month, day);
        return IsReasonableDate(d) ? d : null;
    }

    private static string GuessProductName(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("milk")) return "Milk";
        if (lower.Contains("bread")) return "Bread";
        if (lower.Contains("tomato")) return "Tomato";
        if (lower.Contains("onion")) return "Onion";
        if (lower.Contains("egg")) return "Eggs";
        if (lower.Contains("yogurt") || lower.Contains("yoghurt")) return "Yogurt";
        if (lower.Contains("cheese")) return "Cheese";
        if (lower.Contains("butter")) return "Butter";
        if (lower.Contains("chicken")) return "Chicken";
        if (lower.Contains("rice")) return "Rice";
        if (lower.Contains("juice")) return "Juice";

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = lines.FirstOrDefault(l => l.Length >= 3 && l.Any(char.IsLetter));
        return string.IsNullOrWhiteSpace(candidate) ? "Unknown Product" : candidate[..Math.Min(candidate.Length, 80)];
    }

    private static string GuessCategory(string text)
    {
        if (text.Contains("milk") || text.Contains("cheese") || text.Contains("butter") || text.Contains("yogurt") || text.Contains("yoghurt") || text.Contains("cream") || text.Contains("paneer") || text.Contains("curd")) return "Dairy";
        if (text.Contains("bread") || text.Contains("bun") || text.Contains("cake") || text.Contains("pastry") || text.Contains("croissant")) return "Bakery Item";
        if (text.Contains("biscuit") || text.Contains("cookie") || text.Contains("chocolate") || text.Contains("chips") || text.Contains("namkeen") || text.Contains("snack") || text.Contains("wafer")) return "Snacks";
        if (text.Contains("banana") || text.Contains("apple") || text.Contains("orange") || text.Contains("mango") || text.Contains("grape") || text.Contains("papaya") || text.Contains("kiwi") || text.Contains("pear")) return "Fruits";
        if (text.Contains("chicken") || text.Contains("beef") || text.Contains("fish") || text.Contains("mutton") || text.Contains("pork") || text.Contains("prawn") || text.Contains("shrimp") || text.Contains("meat")) return "Meat";
        if (text.Contains("tomato") || text.Contains("onion") || text.Contains("potato") || text.Contains("carrot") || text.Contains("spinach") || text.Contains("broccoli") || text.Contains("capsicum") || text.Contains("cucumber") || text.Contains("lettuce")) return "Vegetables";
        if (text.Contains("rice") || text.Contains("pasta") || text.Contains("noodle") || text.Contains("oats") || text.Contains("cereal") || text.Contains("wheat") || text.Contains("flour") || text.Contains("atta")) return "Grains";
        if (text.Contains("juice") || text.Contains("soda") || text.Contains("water") || text.Contains("tea") || text.Contains("coffee") || text.Contains("drink")) return "Beverages";
        if (text.Contains("sauce") || text.Contains("ketchup") || text.Contains("pickle") || text.Contains("jam") || text.Contains("honey")) return "Condiments";
        if (text.Contains("frozen") || text.Contains("ice cream")) return "Frozen";
        return "General";
    }

    private static DateOnly? DeriveExpiryFromBestBefore(string text, DateOnly mfgDate)
    {
        var match = BestBeforeDurationPattern.Match(text);
        if (!match.Success) return null;

        var valueToken = match.Groups[1].Value;
        var value = ParseWordOrNumber(valueToken);
        if (!value.HasValue || value.Value <= 0) return null;

        var unit = match.Groups[2].Value.ToLowerInvariant();
        return unit switch
        {
            "day" or "days" => mfgDate.AddDays((int)Math.Ceiling(value.Value)),
            "week" or "weeks" => mfgDate.AddDays((int)Math.Ceiling(value.Value * 7)),
            "month" or "months" or "mon" or "mons" => mfgDate.AddMonths((int)Math.Ceiling(value.Value)),
            "year" or "years" or "yr" or "yrs" => mfgDate.AddYears((int)Math.Ceiling(value.Value)),
            "hrs" or "hours" => mfgDate.AddDays(Math.Max(1, (int)Math.Ceiling(value.Value / 24))),
            _ => null
        };
    }

    private static DateOnly? TryParseBestBeforeEndOf(string text)
    {
        var match = BestBeforeEndOfPattern.Match(text);
        if (!match.Success) return null;

        var monthNum = ParseMonthAbbr(match.Groups[1].Value);
        if (monthNum == 0) return null;

        if (!int.TryParse(match.Groups[2].Value, out var year)) return null;

        if (year < 100)
        {
            year += year > 70 ? 1900 : 2000;
        }

        if (year < 2000 || year > 2100) return null;

        var lastDay = DateTime.DaysInMonth(year, monthNum);
        return new DateOnly(year, monthNum, lastDay);
    }

    private static int ParseMonthAbbr(string token) => token.Trim().ToLowerInvariant() switch
    {
        "jan" or "january" => 1, "feb" or "february" => 2, "mar" or "march" => 3, "apr" or "april" => 4,
        "may" => 5, "jun" or "june" => 6, "jul" or "july" => 7, "aug" or "august" => 8,
        "sep" or "sept" or "september" => 9, "oct" or "october" => 10, "nov" or "november" => 11, "dec" or "december" => 12,
        _ => 0
    };

    /// <summary>
    /// Product-name-aware category fallback expiry.
    /// </summary>
    public static DateOnly GetCategoryFallbackExpiry(string category, string? productName = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lower = productName?.ToLowerInvariant() ?? "";

        return category switch
        {
            "Dairy" when lower.Contains("uht") || lower.Contains("long life") => today.AddDays(90),
            "Dairy" when lower.Contains("parmesan") => today.AddDays(60),
            "Dairy" when lower.Contains("yogurt") || lower.Contains("curd") => today.AddDays(14),
            "Dairy" => today.AddDays(14),
            "Meat" when lower.Contains("frozen") => today.AddDays(90),
            "Meat" when lower.Contains("canned") => today.AddDays(365),
            "Meat" when lower.Contains("dried") || lower.Contains("jerky") => today.AddDays(180),
            "Meat" => today.AddDays(3),
            "Fruits" when lower.Contains("dried") || lower.Contains("raisin") => today.AddDays(180),
            "Fruits" when lower.Contains("canned") => today.AddDays(365),
            "Fruits" => today.AddDays(5),
            "Vegetables" when lower.Contains("canned") => today.AddDays(365),
            "Vegetables" when lower.Contains("frozen") => today.AddDays(90),
            "Vegetables" => today.AddDays(7),
            "Bakery Item" when lower.Contains("frozen") => today.AddDays(90),
            "Bakery Item" => today.AddDays(5),
            "Snacks" => today.AddDays(90),
            "Grains" => today.AddDays(180),
            "Beverages" when lower.Contains("fresh") => today.AddDays(7),
            "Beverages" => today.AddDays(90),
            "Condiments" => today.AddDays(180),
            "Frozen" => today.AddDays(90),
            _ => today.AddDays(30)
        };
    }

    /// <summary>
    /// Computes detailed confidence with per-field scores and human review flag.
    /// </summary>
    private static (int Score, FieldConfidenceDto FieldConf, bool NeedsReview) ComputeDetailedConfidence(
        string productName, bool hasDateEvidence, bool hasLabeledExpiry, bool hasModelPrediction, int dateCount, string category)
    {
        var score = 0;

        string nameConf;
        if (productName != "Unknown Product")
        {
            score += 25;
            nameConf = "high";
        }
        else if (hasModelPrediction)
        {
            score += 15;
            nameConf = "medium";
        }
        else
        {
            nameConf = "low";
        }

        string expiryConf;
        if (hasLabeledExpiry)
        {
            score += 30;
            expiryConf = "high";
        }
        else if (hasDateEvidence)
        {
            score += 20;
            expiryConf = "medium";
        }
        else
        {
            score -= 15; // Penalize missing date evidence
            expiryConf = "low";
        }

        string catConf;
        if (category != "General")
        {
            score += 15;
            catConf = "high";
        }
        else if (hasModelPrediction)
        {
            score += 10;
            catConf = "medium";
        }
        else
        {
            catConf = "low";
        }

        if (dateCount >= 2) score += 10;

        // Cap score when both product name and date are unknown
        if (productName == "Unknown Product" && !hasDateEvidence)
            score = Math.Min(score, 25);

        score = Math.Clamp(score, 0, 100);
        var fieldConf = new FieldConfidenceDto(nameConf, expiryConf, catConf);
        var needsReview = nameConf == "low" || expiryConf == "low" || score < 50;

        return (score, fieldConf, needsReview);
    }

    private static string NormalizeOcrText(string text)
    {
        var normalized = text.Replace("\r", "\n");
        normalized = normalized.Replace("O/", "0/").Replace("/O", "/0").Replace("O-", "0-").Replace("-O", "-0");
        normalized = normalized.Replace("l/", "1/").Replace("/l", "/1").Replace("I/", "1/").Replace("/I", "/1");
        normalized = Regex.Replace(normalized, @"(?<=\d)S(?=\d)", "5");
        normalized = Regex.Replace(normalized, @"(?<=\d)B(?=\d)", "8");
        // Fix letter-O/zero confusion in year context: 2O26 -> 2026
        normalized = Regex.Replace(normalized, @"(?<=2)O(?=2[0-9])", "0");
        normalized = Regex.Replace(normalized, @"(?<=20)O(?=[0-9])", "0");
        // Z -> 2 in year context: 20Z6 -> 2026, Z026 -> 2026
        normalized = Regex.Replace(normalized, @"(?<=20)Z(?=[0-9])", "2");
        normalized = Regex.Replace(normalized, @"(?<=2)Z(?=2[0-9])", "0");
        // G -> 6 in numeric context
        normalized = Regex.Replace(normalized, @"(?<=\d)G(?=\d)", "6");
        // D -> 0 in numeric context (e.g., 2D26 -> 2026)
        normalized = Regex.Replace(normalized, @"(?<=\d)D(?=\d)", "0");
        // I/l -> 1 in numeric context (additional patterns)
        normalized = Regex.Replace(normalized, @"(?<=\d)[Il](?=\d)", "1");
        return normalized;
    }

    private static bool IsReasonableDate(DateOnly date) => date.Year is >= 2000 and <= 2100;

    private static double? ParseWordOrNumber(string token)
    {
        var t = token.Trim();
        // Handle Unicode fractions
        t = t.Replace("½", ".5").Replace("¾", ".75").Replace("⅓", ".33").Replace("⅔", ".67");

        // Handle "X 1/2" or "X 1/4" style fractions (e.g., "1 1/2" → 1.5)
        var fractionMatch = Regex.Match(t, @"^(\d+)\s+(\d+)\s*/\s*(\d+)$");
        if (fractionMatch.Success)
        {
            var whole = double.Parse(fractionMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var num = double.Parse(fractionMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var den = double.Parse(fractionMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            return den > 0 ? whole + num / den : null;
        }

        if (double.TryParse(t, CultureInfo.InvariantCulture, out var n)) return n;

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
}
