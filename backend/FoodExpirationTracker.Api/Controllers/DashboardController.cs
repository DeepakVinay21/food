using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var dashboard = await _dashboardService.GetDashboardAsync(userId, cancellationToken);
        return Ok(dashboard);
    }
}
