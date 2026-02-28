using System.Security.Cryptography;
using FoodExpirationTracker.Application.Abstractions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Domain.Entities;

namespace FoodExpirationTracker.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IEmailVerificationRepository _verificationRepository;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailService emailService,
        IEmailVerificationRepository verificationRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailService = emailService;
        _verificationRepository = verificationRepository;
    }

    public async Task<MessageResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            throw new InvalidOperationException("Password and confirm password do not match.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        // Remove any previous pending verification for this email
        var oldVerification = await _verificationRepository.GetByEmailAsync(email, cancellationToken);
        if (oldVerification is not null)
        {
            await _verificationRepository.DeleteAsync(oldVerification, cancellationToken);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var verification = new EmailVerification
        {
            Email = email,
            Code = code,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Age = request.Age,
            ExpiryTime = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow,
        };

        await _verificationRepository.AddAsync(verification, cancellationToken);
        await _emailService.SendVerificationEmailAsync(email, code);

        return new MessageResponse("Verification code sent to your email.");
    }

    public async Task<AuthResponse> VerifyAsync(VerifyRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var verification = await _verificationRepository.GetByEmailAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("No pending verification found for this email.");

        if (DateTime.UtcNow > verification.ExpiryTime)
        {
            await _verificationRepository.DeleteAsync(verification, cancellationToken);
            throw new InvalidOperationException("Verification code has expired. Please register again.");
        }

        if (verification.AttemptCount >= 5)
        {
            throw new InvalidOperationException("Too many attempts. Please request a new code.");
        }

        if (verification.Code != request.Code.Trim())
        {
            verification.AttemptCount++;
            await _verificationRepository.UpdateAsync(verification, cancellationToken);
            throw new InvalidOperationException("Invalid verification code.");
        }

        // Code is correct — create the user
        var user = new User
        {
            Email = verification.Email,
            PasswordHash = verification.PasswordHash,
            FirstName = verification.FirstName,
            LastName = verification.LastName,
            Age = verification.Age,
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _verificationRepository.DeleteAsync(verification, cancellationToken);

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new AuthResponse(user.Id, user.Email, token);
    }

    public async Task<MessageResponse> ResendAsync(ResendRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var verification = await _verificationRepository.GetByEmailAsync(email, cancellationToken)
            ?? throw new InvalidOperationException("No pending verification found for this email.");

        var secondsSinceLastSend = (DateTime.UtcNow - verification.LastSentAt).TotalSeconds;
        if (secondsSinceLastSend < 60)
        {
            var wait = (int)Math.Ceiling(60 - secondsSinceLastSend);
            throw new InvalidOperationException($"Please wait {wait} seconds before requesting a new code.");
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        verification.Code = code;
        verification.AttemptCount = 0;
        verification.ExpiryTime = DateTime.UtcNow.AddMinutes(5);
        verification.LastSentAt = DateTime.UtcNow;

        await _verificationRepository.UpdateAsync(verification, cancellationToken);
        await _emailService.SendVerificationEmailAsync(email, code);

        return new MessageResponse("New verification code sent to your email.");
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
