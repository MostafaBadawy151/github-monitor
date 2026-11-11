namespace GitHubMonitor.Api.Models;

/// <summary>Unified response envelope for every endpoint: <c>{ success, message, data }</c>.</summary>
public sealed record ApiResponse<T>(bool Success, string Message, T? Data)
{
    public static ApiResponse<T> Ok(T data, string message = "OK") => new(true, message, data);

    public static ApiResponse<T> Fail(string message) => new(false, message, default);
}

/// <summary>Non-generic helper for errors produced outside controllers (exception handler, model validation).</summary>
public static class ApiResponse
{
    public static ApiResponse<object> Fail(string message) => ApiResponse<object>.Fail(message);
}
