using FoodExpirationTracker.Application.DTOs;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IOcrService
{
    OcrScanResult ParseText(string rawText, byte[]? imageBytes = null);
    Task<string> ExtractTextFromImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
