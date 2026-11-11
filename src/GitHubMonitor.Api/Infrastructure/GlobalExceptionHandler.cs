using GitHubMonitor.Api.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace GitHubMonitor.Api.Infrastructure;

/// <summary>Last-resort handler: unhandled exceptions still come back in the unified envelope.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client went away. Nothing useful to send back.
            logger.LogInformation("Request {Path} cancelled by the client", httpContext.Request.Path);
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail("An unexpected server error occurred. Please try again later."),
            cancellationToken);
        return true;
    }
}
