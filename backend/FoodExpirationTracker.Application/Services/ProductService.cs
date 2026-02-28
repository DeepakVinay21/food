using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Application.Services;

public class ProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;

    public ProductService(IProductRepository productRepository, IUserRepository userRepository)
    {
        _productRepository = productRepository;
        _userRepository = userRepository;
    }

    public async Task<ProductDto> AddProductBatchAsync(Guid userId, AddProductRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found. Please log out and register again.");

        var category = await _productRepository.GetOrCreateCategoryAsync(request.CategoryName, cancellationToken);
        var product = await _productRepository.GetByUserAndNameAsync(userId, request.Name, cancellationToken);

        if (product is null)
        {
            product = await _productRepository.AddProductAsync(new Product
            {
                UserId = userId,
                CategoryId = category.Id,
                Name = request.Name.Trim(),
                NormalizedName = request.Name.Trim().ToLowerInvariant()
            }, cancellationToken);
        }

        await _productRepository.AddBatchAsync(new ProductBatch
        {
            ProductId = product.Id,
            ExpiryDate = request.ExpiryDate,
            Quantity = request.Quantity,
            Status = BatchStatus.Active
        }, cancellationToken);

        await _productRepository.SaveChangesAsync(cancellationToken);
        return await MapProductAsync(product, cancellationToken);
    }

    public async Task ConsumeBatchAsync(Guid userId, ConsumeBatchRequest request, CancellationToken cancellationToken = default)
    {
        var isOwned = await _productRepository.IsBatchOwnedByUserAsync(request.BatchId, userId, cancellationToken);
        if (!isOwned)
        {
            throw new UnauthorizedAccessException("This batch does not belong to the current user.");
        }

        var batch = await _productRepository.GetBatchByIdAsync(request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");

        if (request.QuantityUsed <= 0 || request.QuantityUsed > batch.Quantity)
        {
            throw new InvalidOperationException("Invalid quantity used.");
        }

        batch.Quantity -= request.QuantityUsed;
        batch.UpdatedAtUtc = DateTime.UtcNow;

        if (batch.Quantity == 0)
        {
            batch.Status = BatchStatus.Used;
        }

        await _productRepository.UpdateBatchAsync(batch, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductDto>> GetUserInventoryAsync(
        Guid userId,
        string? categoryName,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var paged = await _productRepository.GetUserProductsPagedAsync(userId, categoryName, safePage, safePageSize, cancellationToken);
        var items = new List<ProductDto>(paged.Items.Count);

        foreach (var product in paged.Items)
        {
            items.Add(await MapProductAsync(product, cancellationToken));
        }

        return new PagedResult<ProductDto>(safePage, safePageSize, paged.TotalCount, items);
    }

    public async Task<ProductBatch> GetOwnedBatchAsync(Guid userId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var isOwned = await _productRepository.IsBatchOwnedByUserAsync(batchId, userId, cancellationToken);
        if (!isOwned)
        {
            throw new UnauthorizedAccessException("This batch does not belong to the current user.");
        }

        return await _productRepository.GetBatchByIdAsync(batchId, cancellationToken)
            ?? throw new KeyNotFoundException("Batch not found.");
    }

    private async Task<ProductDto> MapProductAsync(Product product, CancellationToken cancellationToken)
    {
        var category = await _productRepository.GetCategoryByIdAsync(product.CategoryId, cancellationToken);
        var categoryName = category?.Name ?? "General";

        var batches = await _productRepository.GetProductBatchesAsync(product.Id, cancellationToken);
        var batchDtos = batches
            .Where(b => !b.IsDeleted)
            .Select(b => new BatchDto(b.Id, b.ExpiryDate, b.Quantity, b.Status))
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        var totalQuantity = batchDtos.Where(b => b.Status == BatchStatus.Active).Sum(b => b.Quantity);
        return new ProductDto(product.Id, product.Name, categoryName, totalQuantity, batchDtos);
    }
}
