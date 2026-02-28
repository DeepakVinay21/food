using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<MessageResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("verify")]
    public async Task<ActionResult<AuthResponse>> Verify([FromBody] VerifyRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("resend")]
    public async Task<ActionResult<MessageResponse>> Resend([FromBody] ResendRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.ResendAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }
}
