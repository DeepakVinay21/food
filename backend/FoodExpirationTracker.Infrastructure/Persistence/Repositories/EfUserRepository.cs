using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence.Repositories;

public class EfUserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public EfUserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Users.FindAsync([userId], cancellationToken);
    }

    public async Task<List<User>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
