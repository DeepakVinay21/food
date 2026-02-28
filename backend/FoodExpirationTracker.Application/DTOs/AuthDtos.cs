namespace FoodExpirationTracker.Application.DTOs;

public record RegisterRequest(string Email, string Password, string ConfirmPassword, string FirstName, string LastName, int? Age);
public record LoginRequest(string Email, string Password);
public record AuthResponse(Guid UserId, string Email, string AccessToken);
