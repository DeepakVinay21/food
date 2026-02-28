using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Abstractions;

public interface IEmailVerificationRepository
{
    Task<EmailVerification?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(EmailVerification verification, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmailVerification verification, CancellationToken cancellationToken = default);
    Task DeleteAsync(EmailVerification verification, CancellationToken cancellationToken = default);
}
