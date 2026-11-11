using GitHubMonitor.Core.GitHub;
using GitHubMonitor.Core.Models;

namespace GitHubMonitor.Core.Caching;

/// <summary>
/// Immutable view of the cache. A new instance replaces the old one on every update,
/// so readers never see a half-written state.
/// </summary>
public sealed record CommitCacheSnapshot
{
    public static readonly CommitCacheSnapshot Empty = new();

    public IReadOnlyList<CommitInfo> Commits { get; init; } = [];

    /// <summary>"LastSyncTime": when the cached commits were last fetched successfully.</summary>
    public DateTimeOffset? LastSyncTime { get; init; }

    /// <summary>When the job last ran, whether or not it succeeded.</summary>
    public DateTimeOffset? LastAttemptTime { get; init; }

    /// <summary>Set when the most recent attempt failed. Cleared on the next success.</summary>
    public GitHubErrorKind LastErrorKind { get; init; } = GitHubErrorKind.None;

    public string? LastErrorMessage { get; init; }

    public bool HasEverSynced => LastSyncTime is not null;

    public bool LastAttemptFailed => LastErrorKind != GitHubErrorKind.None;
}
