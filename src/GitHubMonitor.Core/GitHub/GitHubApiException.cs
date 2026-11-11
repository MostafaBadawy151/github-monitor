using System.Net;

namespace GitHubMonitor.Core.GitHub;

/// <summary>Raised by <see cref="IGitHubCommitsClient"/> for any failed GitHub call, already classified.</summary>
public sealed class GitHubApiException : Exception
{
    public GitHubApiException(GitHubErrorKind kind, string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public GitHubErrorKind Kind { get; }

    public HttpStatusCode? StatusCode { get; }
}
