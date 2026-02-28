namespace FoodExpirationTracker.Application.Abstractions;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, string role);
    bool TryValidateToken(string token, out Guid userId);
}
