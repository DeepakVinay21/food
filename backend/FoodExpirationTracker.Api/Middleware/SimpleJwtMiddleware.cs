using FoodExpirationTracker.Application.Abstractions;

namespace FoodExpirationTracker.Api.Middleware;

public class SimpleJwtMiddleware
{
    private readonly RequestDelegate _next;

    public SimpleJwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITokenService tokenService)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader[7..].Trim();
            if (tokenService.TryValidateToken(token, out var userId))
            {
                context.Items["UserId"] = userId;
            }
        }

        await _next(context);
    }
}
