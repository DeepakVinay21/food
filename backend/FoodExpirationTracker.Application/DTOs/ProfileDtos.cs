namespace FoodExpirationTracker.Application.DTOs;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UpdateProfileRequest(string FirstName, string LastName, int? Age, string? ProfilePhotoDataUrl);
public record ProfileDto(Guid UserId, string Email, string Role, string FirstName, string LastName, int? Age, string? ProfilePhotoDataUrl);
