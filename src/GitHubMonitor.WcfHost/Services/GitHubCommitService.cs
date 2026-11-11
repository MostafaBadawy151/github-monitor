using CoreWCF;
using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;
using GitHubMonitor.WcfHost.Contracts;

namespace GitHubMonitor.WcfHost.Services;

[ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
public sealed class GitHubCommitService : IGitHubCommitService
{
    private readonly IGitHubCommitsClient _gitHub;
    private readonly ICommitCache _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<GitHubCommitService> _logger;

    public GitHubCommitService(IGitHubCommitsClient gitHub, ICommitCache cache, TimeProvider clock, ILogger<GitHubCommitService> logger)
    {
        _gitHub = gitHub;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CommitQueryResult> GetLiveCommitsAsync()
    {
        _logger.LogInformation("WCF GetLiveCommits called");
        try
        {
            var commits = await _gitHub.GetLatestCommitsAsync();
            return new CommitQueryResult
            {
                Success = true,
                Source = CommitSources.Live,
                Message = $"Retrieved {commits.Count} commits live from GitHub.",
                Commits = Map(commits, CommitSources.Live),
                LastSyncTime = _clock.GetUtcNow().UtcDateTime
            };
        }
        catch (GitHubApiException ex)
        {
            return Failure(CommitSources.Live, ToErrorCode(ex.Kind), ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetLiveCommits");
            return Failure(CommitSources.Live, ServiceErrorCodes.InternalError, "An unexpected error occurred while fetching live commits.");
        }
    }

    public Task<CommitQueryResult> GetScheduledCommitsAsync()
    {
        _logger.LogInformation("WCF GetScheduledCommits called");
        var snapshot = _cache.Current;

        if (!snapshot.HasEverSynced)
        {
            // Nothing cached yet. Tell "still starting up" apart from "the job keeps failing".
            var result = snapshot.LastAttemptFailed
                ? Failure(CommitSources.Scheduled, ToErrorCode(snapshot.LastErrorKind),
                    $"The scheduled sync has not succeeded yet. Last error: {snapshot.LastErrorMessage}")
                : new CommitQueryResult
                {
                    Success = true,
                    Source = CommitSources.Scheduled,
                    ErrorCode = ServiceErrorCodes.NotYetSynced,
                    Message = "The scheduler has not completed its first sync yet. Try again shortly."
                };
            result.LastAttemptTime = snapshot.LastAttemptTime?.UtcDateTime;
            return Task.FromResult(result);
        }

        return Task.FromResult(new CommitQueryResult
        {
            Success = true,
            Source = CommitSources.Scheduled,
            Commits = Map(snapshot.Commits, CommitSources.Scheduled),
            LastSyncTime = snapshot.LastSyncTime?.UtcDateTime,
            LastAttemptTime = snapshot.LastAttemptTime?.UtcDateTime,
            IsStale = snapshot.LastAttemptFailed,
            ErrorCode = snapshot.LastAttemptFailed ? ToErrorCode(snapshot.LastErrorKind) : ServiceErrorCodes.None,
            Message = snapshot.LastAttemptFailed
                ? $"Showing cached data. The latest sync attempt failed: {snapshot.LastErrorMessage}"
                : $"Returned {snapshot.Commits.Count} cached commits."
        });
    }

    private static List<CommitData> Map(IEnumerable<CommitInfo> commits, string source) =>
        commits.Select(c => new CommitData
        {
            Sha = c.Sha,
            Message = c.Message,
            Author = c.Author,
            AuthorLogin = c.AuthorLogin,
            Date = c.Date.UtcDateTime,
            Url = c.HtmlUrl,
            Source = source
        }).ToList();

    private static CommitQueryResult Failure(string source, string errorCode, string message) => new()
    {
        Success = false,
        Source = source,
        ErrorCode = errorCode,
        Message = message
    };

    internal static string ToErrorCode(GitHubErrorKind kind) => kind switch
    {
        GitHubErrorKind.None => ServiceErrorCodes.None,
        GitHubErrorKind.NotConfigured => ServiceErrorCodes.NotConfigured,
        GitHubErrorKind.Unauthorized => ServiceErrorCodes.Unauthorized,
        GitHubErrorKind.Forbidden => ServiceErrorCodes.Forbidden,
        GitHubErrorKind.RateLimited => ServiceErrorCodes.RateLimited,
        GitHubErrorKind.NotFound => ServiceErrorCodes.NotFound,
        GitHubErrorKind.Timeout => ServiceErrorCodes.Timeout,
        GitHubErrorKind.Network => ServiceErrorCodes.Network,
        _ => ServiceErrorCodes.Upstream
    };
}
