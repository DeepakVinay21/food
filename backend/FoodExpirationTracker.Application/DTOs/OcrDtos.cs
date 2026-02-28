namespace FoodExpirationTracker.Application.DTOs;

public record OcrScanRequest(string RawText, int Quantity = 1);

public record FieldConfidenceDto(
    string NameConfidence,
    string ExpiryConfidence,
    string CategoryConfidence);

public record OcrScanResult(
    string ProductName,
    DateOnly? ManufacturingDate,
    DateOnly ExpiryDate,
    int DaysLeftToExpire,
    string CategoryName,
    bool IsConfidenceLow,
    IReadOnlyList<string>? ProductCandidates = null,
    int ConfidenceScore = 0,
    FieldConfidenceDto? FieldConfidence = null,
    bool NeedsHumanReview = false);

/// <summary>
/// Represents a single detected item in a multi-product scan, with its own name, category, and expiry.
/// </summary>
public record DetectedItemResult(
    string ProductName,
    string CategoryName,
    DateOnly ExpiryDate,
    int DaysLeftToExpire,
    int ConfidenceScore,
    bool NeedsHumanReview = false);

public record OcrImagePreviewResponse(
    OcrScanResult Extracted,
    string RawText,
    IReadOnlyList<DetectedItemResult>? DetectedItems = null);

public record OcrImageScanResponse(
    OcrScanResult Extracted,
    ProductDto InventoryProduct,
    string RawText);

/// <summary>
/// Response for multi-product scan-and-add, returning all added products.
/// </summary>
public record OcrMultiAddResponse(
    OcrScanResult Extracted,
    IReadOnlyList<ProductDto> AddedProducts,
    string RawText);

public record CorrectOcrDateRequest(Guid BatchId, DateOnly CorrectedExpiryDate, DateOnly OriginalExpiryDate, string RawOcrText);

/// <summary>
/// Wraps both the scan result and per-item details from Gemini extraction.
/// </summary>
public record GeminiExtractionResult(
    OcrScanResult ScanResult,
    IReadOnlyList<DetectedItemResult>? PerItemDetails);
