using GitHubMonitor.Core.Caching;
using GitHubMonitor.Core.GitHub;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GitHubMonitor.Core.Scheduling;

/// <summary>
/// Quartz job, every minute: GitHub API, then the last 10 commits, then the cache, then LastSyncTime.
/// <see cref="DisallowConcurrentExecutionAttribute"/> stops overlapping runs when GitHub is slow.
/// </summary>
[DisallowConcurrentExecution]
public sealed class GitHubCommitSyncJob : IJob
{
    public static readonly JobKey Key = new("github-commit-sync", "github");

    private readonly IGitHubCommitsClient _gitHub;
    private readonly ICommitCache _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<GitHubCommitSyncJob> _logger;

    public GitHubCommitSyncJob(IGitHubCommitsClient gitHub, ICommitCache cache, TimeProvider clock, ILogger<GitHubCommitSyncJob> logger)
    {
        _gitHub = gitHub;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("Commit sync started (fire time {FireTime:u})", context.FireTimeUtc);

        try
        {
            var commits = await _gitHub.GetLatestCommitsAsync(ct);
            var now = _clock.GetUtcNow();
            await _cache.StoreAsync(commits, now, ct);
            _logger.LogInformation("Commit sync succeeded: cached {Count} commits, LastSyncTime={LastSyncTime:u}", commits.Count, now);
        }
        catch (GitHubApiException ex)
        {
            // Expected failure (401, timeout, rate limit, ...). Keep the old data and record the error;
            // the next trigger retries automatically.
            await _cache.RecordFailureAsync(ex.Kind, ex.Message, _clock.GetUtcNow(), CancellationToken.None);
            _logger.LogWarning("Commit sync failed: {Kind} - {Message}", ex.Kind, ex.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Commit sync cancelled (scheduler shutting down)");
        }
        catch (Exception ex)
        {
            await _cache.RecordFailureAsync(GitHubErrorKind.Upstream, "Unexpected error during sync.", _clock.GetUtcNow(), CancellationToken.None);
            _logger.LogError(ex, "Unexpected error during commit sync");
            // Not rethrown: a JobExecutionException would only be logged by Quartz too, and the trigger keeps firing.
        }
    }
}
