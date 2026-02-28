using FoodExpirationTracker.Domain.Common;
using FoodExpirationTracker.Domain.Enums;

namespace FoodExpirationTracker.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? ProfilePhotoDataUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}
