using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;

namespace GitHubMonitor.Core.Caching;

public interface ICommitCache
{
    CommitCacheSnapshot Current { get; }

    /// <summary>Stores a successful sync result and stamps LastSyncTime.</summary>
    Task StoreAsync(IReadOnlyList<CommitInfo> commits, DateTimeOffset syncTime, CancellationToken cancellationToken = default);

    /// <summary>Records a failed attempt and keeps the previously cached commits.</summary>
    Task RecordFailureAsync(GitHubErrorKind kind, string message, DateTimeOffset attemptTime, CancellationToken cancellationToken = default);
}
