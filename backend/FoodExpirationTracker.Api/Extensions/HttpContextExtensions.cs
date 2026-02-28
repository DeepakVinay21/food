namespace FoodExpirationTracker.Api.Extensions;

public static class HttpContextExtensions
{
    public static Guid GetRequiredUserId(this HttpContext context)
    {
        if (context.Items.TryGetValue("UserId", out var value) && value is Guid userId)
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Missing or invalid token.");
    }
}
