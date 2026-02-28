using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Infrastructure.Repositories;

public class InMemoryOcrCorrectionRepository : IOcrCorrectionRepository
{
    private static readonly List<OcrCorrectionLog> Logs = new();
    private static readonly Lock Sync = new();

    public Task AddAsync(OcrCorrectionLog log, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            Logs.Add(log);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetFrequentCorrectionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
