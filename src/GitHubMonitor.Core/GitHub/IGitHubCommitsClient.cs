using GitHubMonitor.Core.Models;

namespace GitHubMonitor.Core.GitHub;

public interface IGitHubCommitsClient
{
    /// <summary>Fetches the latest commits (newest first) of the configured repository.</summary>
    /// <exception cref="GitHubApiException">On any GitHub, network or timeout failure.</exception>
    Task<IReadOnlyList<CommitInfo>> GetLatestCommitsAsync(CancellationToken cancellationToken = default);
}
