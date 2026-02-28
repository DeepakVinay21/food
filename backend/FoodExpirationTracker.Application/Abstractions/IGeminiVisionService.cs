using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IGeminiVisionService
{
    Task<GeminiExtractionResult?> ExtractFieldsAsync(
        IReadOnlyList<byte[]> images,
        CancellationToken cancellationToken = default);

    Task<OcrScanResult?> RefineAsync(
        string rawText,
        IReadOnlyList<byte[]> images,
        OcrScanResult baseline,
        CancellationToken cancellationToken = default);

    Task<string?> ExtractTextAsync(
        IReadOnlyList<byte[]> images,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight visual-only classification when OCR text fails.
    /// Returns (productName, categoryName) or null.
    /// </summary>
    Task<(string ProductName, string CategoryName)?> ClassifyProductImageAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
