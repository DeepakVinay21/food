using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfOcrCorrectionRepository : IOcrCorrectionRepository
{
    private readonly AppDbContext _db;

    public EfOcrCorrectionRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(OcrCorrectionLog log, CancellationToken cancellationToken = default)
    {
        _db.OcrCorrectionLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetFrequentCorrectionsAsync(CancellationToken cancellationToken = default)
    {
        // Find product names that have been corrected 3+ times, and return the most common correction
        var corrections = await _db.OcrCorrectionLogs
            .Join(_db.ProductBatches, log => log.ProductBatchId, batch => batch.Id, (log, batch) => new { batch.ProductId, log.CorrectedExpiryDate })
            .Join(_db.Products, x => x.ProductId, product => product.Id, (x, product) => new { product.Name, product.CategoryId })
            .Join(_db.Categories, x => x.CategoryId, cat => cat.Id, (x, cat) => new { ProductName = x.Name, CategoryName = cat.Name })
            .GroupBy(x => x.ProductName.ToLower())
            .Where(g => g.Count() >= 3)
            .Select(g => new
            {
                ProductName = g.Key,
                CategoryName = g.GroupBy(x => x.CategoryName).OrderByDescending(cg => cg.Count()).Select(cg => cg.Key).First()
            })
            .ToListAsync(cancellationToken);

        return corrections.ToDictionary(c => c.ProductName, c => c.CategoryName, StringComparer.OrdinalIgnoreCase);
    }
}
