namespace GitHubMonitor.Core.GitHub;

/// <summary>Failure categories the rest of the system reacts to.</summary>
public enum GitHubErrorKind
{
    None = 0,

    /// <summary>No token configured (only reported if a token is mandatory).</summary>
    NotConfigured,

    /// <summary>HTTP 401: token missing, invalid, expired or revoked.</summary>
    Unauthorized,

    /// <summary>HTTP 403 that is not a rate limit: token lacks the required scope.</summary>
    Forbidden,

    /// <summary>HTTP 403/429 with an exhausted rate-limit budget.</summary>
    RateLimited,

    /// <summary>HTTP 404: repository does not exist, or the token cannot see it (GitHub hides private repos).</summary>
    NotFound,

    /// <summary>GitHub did not answer within the configured timeout.</summary>
    Timeout,

    /// <summary>DNS / connection / TLS failure.</summary>
    Network,

    /// <summary>5xx or an unparseable response from GitHub.</summary>
    Upstream
}
