using FoodExpirationTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>();

        // Database check
        try
        {
            await _db.Database.CanConnectAsync(cancellationToken);
            checks["database"] = new { status = "healthy" };
        }
        catch (Exception ex)
        {
            checks["database"] = new { status = "unhealthy", error = ex.Message };
        }

        // Gemini configuration check
        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var geminiEnabled = Environment.GetEnvironmentVariable("GEMINI_ENABLED");
        checks["gemini"] = new
        {
            status = !string.IsNullOrWhiteSpace(geminiKey) ? "configured" : "missing",
            enabled = !string.Equals(geminiEnabled, "false", StringComparison.OrdinalIgnoreCase)
        };

        // PaddleOCR check
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetAsync("http://localhost:8090/health", cancellationToken);
            checks["paddleocr"] = new { status = response.IsSuccessStatusCode ? "healthy" : "unhealthy" };
        }
        catch
        {
            checks["paddleocr"] = new { status = "unavailable" };
        }

        var allHealthy = checks.Values.All(v =>
        {
            var statusProp = v.GetType().GetProperty("status");
            var val = statusProp?.GetValue(v)?.ToString();
            return val is "healthy" or "configured";
        });

        return allHealthy ? Ok(checks) : StatusCode(503, checks);
    }
}
