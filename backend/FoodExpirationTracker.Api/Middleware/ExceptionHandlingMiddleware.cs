using System.Collections.Concurrent;
using System.Net;

namespace FoodExpirationTracker.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    // Rolling error window for rate monitoring
    private static readonly ConcurrentQueue<DateTime> ErrorTimestamps = new();
    private static readonly TimeSpan ErrorWindow = TimeSpan.FromMinutes(5);
    private const int ErrorThreshold = 50;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized: {Path}", context.Request.Path);
            await WriteError(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Path}", context.Request.Path);
            TrackError();
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Not found: {Path} - {Message}", context.Request.Path, ex.Message);
            await WriteError(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Path}", context.Request.Path);
            TrackError();
            await WriteError(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private void TrackError()
    {
        var now = DateTime.UtcNow;
        ErrorTimestamps.Enqueue(now);

        // Trim old entries
        while (ErrorTimestamps.TryPeek(out var oldest) && now - oldest > ErrorWindow)
        {
            ErrorTimestamps.TryDequeue(out _);
        }

        if (ErrorTimestamps.Count >= ErrorThreshold)
        {
            _logger.LogCritical("High error rate detected: {Count} errors in the last {Window} minutes", ErrorTimestamps.Count, ErrorWindow.TotalMinutes);
        }
    }

    private static async Task WriteError(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
