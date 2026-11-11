namespace GitHubMonitor.Api.Wcf;

/// <summary>Application-facing wrapper around the WCF proxy (mockable, hides channel management).</summary>
public interface IGitHubCommitServiceClient
{
    /// <exception cref="WcfServiceException">On transport failures.</exception>
    Task<CommitQueryResult> GetLiveCommitsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="WcfServiceException">On transport failures.</exception>
    Task<CommitQueryResult> GetScheduledCommitsAsync(CancellationToken cancellationToken = default);
}
