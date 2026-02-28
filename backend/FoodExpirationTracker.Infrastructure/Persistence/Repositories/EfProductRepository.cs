using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public EfProductRepository(AppDbContext db) => _db = db;

    public async Task<Category> GetOrCreateCategoryAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        var normalized = categoryName.Trim().ToLowerInvariant();
        var existing = await _db.Categories.FirstOrDefaultAsync(c => c.NormalizedName == normalized, cancellationToken);
        if (existing is not null)
            return existing;

        var created = new Category { Name = categoryName.Trim(), NormalizedName = normalized };
        _db.Categories.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<Category?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return await _db.Categories.FindAsync([categoryId], cancellationToken);
    }

    public async Task<Product?> GetByUserAndNameAsync(Guid userId, string productName, CancellationToken cancellationToken = default)
    {
        var normalized = productName.Trim().ToLowerInvariant();
        return await _db.Products.FirstOrDefaultAsync(
            p => p.UserId == userId && p.NormalizedName == normalized, cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _db.Products.FindAsync([productId], cancellationToken);
    }

    public async Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<ProductBatch> AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        _db.ProductBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<ProductBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches.FindAsync([batchId], cancellationToken);
    }

    public async Task<bool> IsBatchOwnedByUserAsync(Guid batchId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Include(b => b.Product)
            .AnyAsync(b => b.Id == batchId && b.Product!.UserId == userId, cancellationToken);
    }

    public async Task<List<Product>> GetUserProductsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<Product> Items, int TotalCount)> GetUserProductsPagedAsync(Guid userId, string? categoryName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.Where(p => p.UserId == userId);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var normalized = categoryName.Trim().ToLowerInvariant();
            query = query.Where(p => p.Category!.NormalizedName == normalized);
        }

        var ordered = query.OrderBy(p => p.Name);
        var totalCount = await ordered.CountAsync(cancellationToken);
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<List<ProductBatch>> GetProductBatchesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Where(b => b.ProductId == productId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductBatch>> GetUserActiveBatchesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.Product!.UserId == userId && b.Status == BatchStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ProductBatch>> GetUserBatchesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.Product!.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        batch.UpdatedAtUtc = DateTime.UtcNow;
        if (batch.Status == BatchStatus.Active && batch.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow) && batch.Quantity > 0)
        {
            batch.Status = BatchStatus.Expired;
        }

        _db.ProductBatches.Update(batch);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        batch.DeletedAtUtc = DateTime.UtcNow;
        _db.ProductBatches.Update(batch);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
