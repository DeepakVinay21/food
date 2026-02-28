using FoodExpirationTracker.Domain.Common;

namespace FoodExpirationTracker.Domain.Entities;

public class OcrCorrectionLog : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductBatchId { get; set; }
    public DateOnly OriginalExpiryDate { get; set; }
    public DateOnly CorrectedExpiryDate { get; set; }
    public string RawOcrText { get; set; } = string.Empty;
}
