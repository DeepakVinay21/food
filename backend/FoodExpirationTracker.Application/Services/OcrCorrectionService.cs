using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Services;

public class OcrCorrectionService
{
    private readonly ProductService _productService;
    private readonly IProductRepository _productRepository;
    private readonly IOcrCorrectionRepository _ocrCorrectionRepository;

    public OcrCorrectionService(
        ProductService productService,
        IProductRepository productRepository,
        IOcrCorrectionRepository ocrCorrectionRepository)
    {
        _productService = productService;
        _productRepository = productRepository;
        _ocrCorrectionRepository = ocrCorrectionRepository;
    }

    public async Task CorrectExpiryDateAsync(Guid userId, CorrectOcrDateRequest request, CancellationToken cancellationToken = default)
    {
        var batch = await _productService.GetOwnedBatchAsync(userId, request.BatchId, cancellationToken);

        batch.ExpiryDate = request.CorrectedExpiryDate;
        batch.UpdatedAtUtc = DateTime.UtcNow;

        await _productRepository.UpdateBatchAsync(batch, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        await _ocrCorrectionRepository.AddAsync(new OcrCorrectionLog
        {
            UserId = userId,
            ProductBatchId = batch.Id,
            OriginalExpiryDate = request.OriginalExpiryDate,
            CorrectedExpiryDate = request.CorrectedExpiryDate,
            RawOcrText = request.RawOcrText
        }, cancellationToken);
    }
}
