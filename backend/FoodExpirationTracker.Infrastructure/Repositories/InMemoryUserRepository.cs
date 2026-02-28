using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Infrastructure.Repositories;

public class InMemoryUserRepository : IUserRepository
{
    private static readonly List<User> Users = new();
    private static readonly Lock Sync = new();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        lock (Sync)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Email == normalized && !u.IsDeleted));
        }
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Id == userId && !u.IsDeleted));
        }
    }

    public Task<List<User>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            return Task.FromResult(Users.Where(u => !u.IsDeleted).ToList());
        }
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            Users.Add(user);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        lock (Sync)
        {
            var existing = Users.FirstOrDefault(u => u.Id == user.Id);
            if (existing is not null)
            {
                existing.Email = user.Email;
                existing.PasswordHash = user.PasswordHash;
                existing.FirstName = user.FirstName;
                existing.LastName = user.LastName;
                existing.Age = user.Age;
                existing.ProfilePhotoDataUrl = user.ProfilePhotoDataUrl;
                existing.Role = user.Role;
                existing.UpdatedAtUtc = user.UpdatedAtUtc;
                existing.DeletedAtUtc = user.DeletedAtUtc;
            }
        }

        return Task.CompletedTask;
    }
}
