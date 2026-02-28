using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
public class ProfileController : ControllerBase
{
    private readonly AuthService _authService;

    public ProfileController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> Me(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var profile = await _authService.GetProfileAsync(userId, cancellationToken);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _authService.UpdateProfileAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _authService.DeleteAccountAsync(userId, cancellationToken);
        return NoContent();
    }
}
