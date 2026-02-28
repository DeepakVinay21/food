using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IOcrCorrectionRepository
{
    Task AddAsync(OcrCorrectionLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most frequently corrected product-to-category mappings
    /// to apply as post-processing overrides.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetFrequentCorrectionsAsync(CancellationToken cancellationToken = default);
}
