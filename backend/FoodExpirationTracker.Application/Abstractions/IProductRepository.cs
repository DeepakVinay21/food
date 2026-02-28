using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IProductRepository
{
    Task<Category> GetOrCreateCategoryAsync(string categoryName, CancellationToken cancellationToken = default);
    Task<Category?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<Product?> GetByUserAndNameAsync(Guid userId, string productName, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default);
    Task<ProductBatch> AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<bool> IsBatchOwnedByUserAsync(Guid batchId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<Product>> GetUserProductsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<(List<Product> Items, int TotalCount)> GetUserProductsPagedAsync(Guid userId, string? categoryName, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<List<ProductBatch>> GetProductBatchesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<List<ProductBatch>> GetUserActiveBatchesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<ProductBatch>> GetUserBatchesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task DeleteBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
