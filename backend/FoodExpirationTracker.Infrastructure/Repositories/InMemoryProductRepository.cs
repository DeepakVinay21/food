using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Infrastructure.Repositories;

public class InMemoryProductRepository : IProductRepository
{
    private static readonly List<Category> Categories = new();
    private static readonly List<Product> Products = new();
    private static readonly List<ProductBatch> Batches = new();
    private static readonly Lock Sync = new();

    public Task<Category> GetOrCreateCategoryAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        var normalized = categoryName.Trim().ToLowerInvariant();

        lock (Sync)
        {
            var existing = Categories.FirstOrDefault(c => c.Name.ToLowerInvariant() == normalized && !c.IsDeleted);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            var created = new Category { Name = categoryName.Trim() };
            Categories.Add(created);
            return Task.FromResult(created);
        }
    }

    public Task<Category?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Categories.FirstOrDefault(c => c.Id == categoryId && !c.IsDeleted));
        }
    }

    public Task<Product?> GetByUserAndNameAsync(Guid userId, string productName, CancellationToken cancellationToken = default)
    {
        var normalized = productName.Trim().ToLowerInvariant();
        lock (Sync)
        {
            return Task.FromResult(Products.FirstOrDefault(p => p.UserId == userId && p.Name.ToLowerInvariant() == normalized && !p.IsDeleted));
        }
    }

    public Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted));
        }
    }

    public Task<Product> AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            Products.Add(product);
            return Task.FromResult(product);
        }
    }

    public Task<ProductBatch> AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            Batches.Add(batch);
            return Task.FromResult(batch);
        }
    }

    public Task<ProductBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Batches.FirstOrDefault(b => b.Id == batchId && !b.IsDeleted));
        }
    }

    public Task<bool> IsBatchOwnedByUserAsync(Guid batchId, Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            var batch = Batches.FirstOrDefault(b => b.Id == batchId && !b.IsDeleted);
            if (batch is null)
            {
                return Task.FromResult(false);
            }

            var product = Products.FirstOrDefault(p => p.Id == batch.ProductId && !p.IsDeleted);
            return Task.FromResult(product?.UserId == userId);
        }
    }

    public Task<List<Product>> GetUserProductsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Products.Where(p => p.UserId == userId && !p.IsDeleted).OrderBy(p => p.Name).ToList());
        }
    }

    public Task<(List<Product> Items, int TotalCount)> GetUserProductsPagedAsync(Guid userId, string? categoryName, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            var query = Products.Where(p => p.UserId == userId && !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var normalized = categoryName.Trim().ToLowerInvariant();
                var matchingCategoryIds = Categories
                    .Where(c => !c.IsDeleted && c.Name.ToLowerInvariant() == normalized)
                    .Select(c => c.Id)
                    .ToHashSet();

                query = query.Where(p => matchingCategoryIds.Contains(p.CategoryId));
            }

            var ordered = query.OrderBy(p => p.Name);
            var totalCount = ordered.Count();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((items, totalCount));
        }
    }

    public Task<List<ProductBatch>> GetProductBatchesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Batches.Where(b => b.ProductId == productId && !b.IsDeleted).ToList());
        }
    }

    public Task<List<ProductBatch>> GetUserActiveBatchesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            var userProductIds = Products.Where(p => p.UserId == userId && !p.IsDeleted).Select(p => p.Id).ToHashSet();
            return Task.FromResult(Batches
                .Where(b => userProductIds.Contains(b.ProductId) && !b.IsDeleted && b.Status == BatchStatus.Active)
                .ToList());
        }
    }

    public Task<List<ProductBatch>> GetUserBatchesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            var userProductIds = Products.Where(p => p.UserId == userId && !p.IsDeleted).Select(p => p.Id).ToHashSet();
            return Task.FromResult(Batches.Where(b => userProductIds.Contains(b.ProductId) && !b.IsDeleted).ToList());
        }
    }

    public Task UpdateBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            batch.UpdatedAtUtc = DateTime.UtcNow;
            if (batch.Status == BatchStatus.Active && batch.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow) && batch.Quantity > 0)
            {
                batch.Status = BatchStatus.Expired;
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            batch.DeletedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
