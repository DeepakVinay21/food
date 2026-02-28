using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            throw new InvalidOperationException("Password and confirm password do not match.");
        }

        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Age = request.Age
        };

        await _userRepository.AddAsync(user, cancellationToken);

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new AuthResponse(user.Id, user.Email, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new AuthResponse(user.Id, user.Email, token);
    }

    public async Task<ProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        return new ProfileDto(user.Id, user.Email, user.Role.ToString(), user.FirstName, user.LastName, user.Age, user.ProfilePhotoDataUrl);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Age = request.Age;
        user.ProfilePhotoDataUrl = request.ProfilePhotoDataUrl;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new InvalidOperationException("New password must be at least 6 characters.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User not found.");

        user.DeletedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
