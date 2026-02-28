using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfEmailVerificationRepository : IEmailVerificationRepository
{
    private readonly AppDbContext _db;

    public EfEmailVerificationRepository(AppDbContext db) => _db = db;

    public async Task<EmailVerification?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _db.EmailVerifications.FirstOrDefaultAsync(v => v.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        _db.EmailVerifications.Add(verification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        _db.EmailVerifications.Update(verification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(EmailVerification verification, CancellationToken cancellationToken = default)
    {
        _db.EmailVerifications.Remove(verification);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
