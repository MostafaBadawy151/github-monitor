using System.Net;

namespace GitHubMonitor.Api.Services;

/// <summary>Outcome of an application-service call. The HTTP status is decided here, so controllers stay thin.</summary>
public sealed record ServiceResult<T>(bool Success, string Message, T? Data, HttpStatusCode StatusCode)
{
    public static ServiceResult<T> Ok(T data, string message) => new(true, message, data, HttpStatusCode.OK);

    public static ServiceResult<T> Fail(HttpStatusCode status, string message) => new(false, message, default, status);
}
