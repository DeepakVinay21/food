namespace FoodExpirationTracker.Application.DTOs;

public record RegisterRequest(string Email, string Password, string ConfirmPassword, string FirstName, string LastName, int? Age);
public record LoginRequest(string Email, string Password);
public record VerifyRequest(string Email, string Code);
public record ResendRequest(string Email);
public record AuthResponse(Guid UserId, string Email, string AccessToken);
public record MessageResponse(string Message);
